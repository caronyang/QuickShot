using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using QuickShot.Interop;
using QuickShot.Services;
using Drawing = System.Drawing;
using Forms = System.Windows.Forms;

namespace QuickShot.Views;

/// <summary>
/// The dimmed, full-screen selection overlay for one monitor.
///
/// The whole virtual desktop was captured *before* this window appeared, so we can show
/// the frozen desktop underneath the dim layer. That gives a flicker-free overlay, a live
/// magnifier and - most importantly - guarantees the overlay itself can never end up in
/// the snapshot.
/// </summary>
public partial class OverlayWindow : Window
{
    private const int LoupeSourceWidth = 24;
    private const int LoupeSourceHeight = 18;
    private const int LoupeZoomDip = 120;          // rendered width of the magnified image
    private const int LoupeZoomDipHeight = 90;
    private const double LoupePanelHeight = 112;   // image + caption row
    private const double CrosshairGapDip = 5;      // keeps the target pixel visible
    private const double CrosshairArmDip = 15;

    private readonly BitmapSource _desktop;
    private readonly Drawing.Rectangle _virtualScreen;
    private readonly Drawing.Rectangle _monitor;
    private readonly SelectionSession _session;

    private IntPtr _hwnd;
    private bool _dragging;
    private Point _anchorDip;
    private Int32Rect? _selectionDip;
    private bool _sizeCheckQueued;

    internal OverlayWindow(BitmapSource desktop, Drawing.Rectangle virtualScreen, Drawing.Rectangle monitor, SelectionSession session, BitmapSource monitorImage)
    {
        _desktop = desktop;
        _virtualScreen = virtualScreen;
        _monitor = monitor;
        _session = session;

        InitializeComponent();
        ScreenImage.Source = monitorImage;

        var scale = DpiHelper.GetScaleForScreen(DpiHelper.ScreenFromPoint(new Drawing.Point(monitor.Left + 1, monitor.Top + 1)));
        Left = monitor.Left / scale;
        Top = monitor.Top / scale;
        Width = Math.Max(1, monitor.Width / scale);
        Height = Math.Max(1, monitor.Height / scale);

        Loaded += OnLoaded;
        DpiChanged += OnDpiChanged;
    }

    internal Drawing.Rectangle Monitor => _monitor;

    internal bool ContainsPhysical(Drawing.Point point) =>
        point.X >= _monitor.Left && point.X < _monitor.Right && point.Y >= _monitor.Top && point.Y < _monitor.Bottom;

    /// <summary>Closes without reporting a selection (used when another overlay won the drag).</summary>
    internal void DismissSilently()
    {
        _session.MarkDismissed(this);
        Close();
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        _hwnd = new WindowInteropHelper(this).Handle;
        PlaceExactly();
        UpdateDimGeometry();

        // The pointer is already over this monitor, so place the hand drawn crosshair
        // immediately - otherwise the hidden system cursor would leave a blank moment.
        var cursor = DpiHelper.GetCursorPhysical();
        UpdateAiming(PhysicalToDip(cursor));
    }

    /// <summary>Physical placement is authoritative; WPF's DIP size is synced to match.</summary>
    private void PlaceExactly()
    {
        if (_hwnd == IntPtr.Zero) return;

        NativeMethods.SetWindowPos(
            _hwnd, NativeMethods.HWND_TOPMOST,
            _monitor.Left, _monitor.Top, _monitor.Width, _monitor.Height,
            NativeMethods.SWP_NOACTIVATE | NativeMethods.SWP_SHOWWINDOW);

        var dpi = VisualTreeHelper.GetDpi(this);
        var width = _monitor.Width / dpi.DpiScaleX;
        var height = _monitor.Height / dpi.DpiScaleY;

        if (Math.Abs(Width - width) > 0.5 || Math.Abs(Height - height) > 0.5)
        {
            Width = width;
            Height = height;
        }
    }

    private void OnDpiChanged(object sender, DpiChangedEventArgs e)
    {
        // The monitor layout can change while an overlay is up; re-assert the placement
        // and the geometry once WPF finished its own re-layout pass.
        Dispatcher.BeginInvoke(new Action(() =>
        {
            PlaceExactly();
            UpdateDimGeometry();
        }), System.Windows.Threading.DispatcherPriority.Loaded);
    }

    private void OnSizeChangedQueue(object? sender, EventArgs e)
    {
        if (_sizeCheckQueued) return;
        _sizeCheckQueued = true;
        Dispatcher.BeginInvoke(new Action(() =>
        {
            _sizeCheckQueued = false;
            PlaceExactly();
        }), System.Windows.Threading.DispatcherPriority.Loaded);
    }

    // ---------------------------------------------------------------- geometry

    private double ScaleX
    {
        get
        {
            if (_hwnd != IntPtr.Zero && NativeMethods.GetWindowRect(_hwnd, out var rect) && rect.Width > 0 && ActualWidth > 0)
            {
                return rect.Width / ActualWidth;
            }

            var dpi = VisualTreeHelper.GetDpi(this);
            return dpi.DpiScaleX <= 0 ? 1.0 : dpi.DpiScaleX;
        }
    }

    private double ScaleY
    {
        get
        {
            if (_hwnd != IntPtr.Zero && NativeMethods.GetWindowRect(_hwnd, out var rect) && rect.Height > 0 && ActualHeight > 0)
            {
                return rect.Height / ActualHeight;
            }

            var dpi = VisualTreeHelper.GetDpi(this);
            return dpi.DpiScaleY <= 0 ? 1.0 : dpi.DpiScaleY;
        }
    }

    /// <summary>DIP (window local) -> physical pixel on the virtual desktop.</summary>
    private Drawing.Point DipToPhysical(Point dip)
    {
        var (originX, originY) = WindowOriginPhysical();

        return new Drawing.Point(
            (int)Math.Round(originX + dip.X * ScaleX),
            (int)Math.Round(originY + dip.Y * ScaleY));
    }

    /// <summary>Physical pixel on the virtual desktop -> DIP (window local).</summary>
    private Point PhysicalToDip(Drawing.Point physical)
    {
        var (originX, originY) = WindowOriginPhysical();
        var scaleX = ScaleX <= 0 ? 1.0 : ScaleX;
        var scaleY = ScaleY <= 0 ? 1.0 : ScaleY;

        return new Point((physical.X - originX) / scaleX, (physical.Y - originY) / scaleY);
    }

    private (int X, int Y) WindowOriginPhysical()
    {
        if (_hwnd != IntPtr.Zero && NativeMethods.GetWindowRect(_hwnd, out var rect))
        {
            return (rect.Left, rect.Top);
        }

        return (_monitor.Left, _monitor.Top);
    }

    /// <summary>
    /// Draws the aiming aids: full screen guide lines, a two-tone crosshair with a gap in
    /// the middle (so the pixel being aimed at stays visible) and a coordinate readout.
    /// </summary>
    private void UpdateAiming(Point positionDip)
    {
        var width = ActualWidth;
        var height = ActualHeight;
        if (width <= 0 || height <= 0) return;

        var x = Math.Round(positionDip.X) + 0.5;
        var y = Math.Round(positionDip.Y) + 0.5;

        CursorLayer.Visibility = Visibility.Visible;
        CursorCoordBadge.Visibility = Visibility.Visible;

        GuideVertical.Height = height;
        Canvas.SetLeft(GuideVertical, x);
        Canvas.SetTop(GuideVertical, 0);

        GuideHorizontal.Width = width;
        Canvas.SetLeft(GuideHorizontal, 0);
        Canvas.SetTop(GuideHorizontal, y);

        var geometry = new StreamGeometry();
        using (var context = geometry.Open())
        {
            AddStroke(context, x - CrosshairGapDip - CrosshairArmDip, y, x - CrosshairGapDip, y);
            AddStroke(context, x + CrosshairGapDip, y, x + CrosshairGapDip + CrosshairArmDip, y);
            AddStroke(context, x, y - CrosshairGapDip - CrosshairArmDip, x, y - CrosshairGapDip);
            AddStroke(context, x, y + CrosshairGapDip, x, y + CrosshairGapDip + CrosshairArmDip);
        }

        geometry.Freeze();
        CrosshairLine.Data = geometry;
        CrosshairOutline.Data = geometry;

        var physical = DipToPhysical(positionDip);
        CursorCoordText.Text = $"{physical.X}, {physical.Y}";
        PositionBadge(CursorCoordBadge, positionDip, 16, 86, 22);
    }

    private static void AddStroke(StreamGeometryContext context, double x1, double y1, double x2, double y2)
    {
        context.BeginFigure(new Point(x1, y1), isFilled: false, isClosed: false);
        context.LineTo(new Point(x2, y2), isStroked: true, isSmoothJoin: false);
    }

    /// <summary>Places a floating badge next to the pointer, flipping it near screen edges.</summary>
    private void PositionBadge(FrameworkElement badge, Point anchor, double gap, double badgeWidth, double badgeHeight)
    {
        var left = anchor.X + gap;
        var top = anchor.Y + gap;

        if (left + badgeWidth + 6 > ActualWidth) left = anchor.X - badgeWidth - gap;
        if (top + badgeHeight + 6 > ActualHeight) top = anchor.Y - badgeHeight - gap;

        left = Math.Clamp(left, 4, Math.Max(4, ActualWidth - badgeWidth - 4));
        top = Math.Clamp(top, 4, Math.Max(4, ActualHeight - badgeHeight - 4));

        Canvas.SetLeft(badge, left);
        Canvas.SetTop(badge, top);
    }

    private Rect SelectionRectDip => _selectionDip is { } r ? new Rect(r.X, r.Y, r.Width, r.Height) : Rect.Empty;

    private void UpdateDimGeometry()
    {
        var width = ActualWidth;
        var height = ActualHeight;
        if (width <= 0 || height <= 0) return;

        var outer = new RectangleGeometry(new Rect(0, 0, width, height));
        var selection = SelectionRectDip;

        if (selection.IsEmpty || selection.Width < 1 || selection.Height < 1)
        {
            DimPath.Data = outer;
            SelectionBorder.Visibility = Visibility.Collapsed;
            SizeLabel.Visibility = Visibility.Collapsed;
            return;
        }

        DimPath.Data = new CombinedGeometry(
            GeometryCombineMode.Exclude, outer, new RectangleGeometry(selection));

        SelectionBorder.Visibility = Visibility.Visible;
        SelectionBorder.Width = selection.Width;
        SelectionBorder.Height = selection.Height;
        SelectionBorder.Margin = new Thickness(selection.X, selection.Y, 0, 0);
        SelectionBorder.HorizontalAlignment = System.Windows.HorizontalAlignment.Left;
        SelectionBorder.VerticalAlignment = System.Windows.VerticalAlignment.Top;

        var physical = SelectionPhysical();
        if (physical is { } p)
        {
            SizeText.Text = $"{p.Width} × {p.Height}";
            SizeLabel.Visibility = Visibility.Visible;

            var labelWidth = 82.0;
            var x = selection.X;
            var y = selection.Y - 26;
            if (y < 4) y = selection.Y + selection.Height + 6;
            x = Math.Clamp(x, 4, Math.Max(4, width - labelWidth - 4));
            SizeLabel.Margin = new Thickness(x, y, 0, 0);
        }
    }

    private Int32Rect? SelectionPhysical()
    {
        if (_selectionDip is not { } selection || selection.Width < 1 || selection.Height < 1) return null;

        var topLeft = DipToPhysical(new Point(selection.X, selection.Y));
        var bottomRight = DipToPhysical(new Point(selection.X + selection.Width, selection.Y + selection.Height));

        var x = Math.Max(topLeft.X, _virtualScreen.Left);
        var y = Math.Max(topLeft.Y, _virtualScreen.Top);
        var right = Math.Min(bottomRight.X, _virtualScreen.Right);
        var bottom = Math.Min(bottomRight.Y, _virtualScreen.Bottom);

        var width = right - x;
        var height = bottom - y;
        if (width < 1 || height < 1) return null;

        return new Int32Rect(x - _virtualScreen.Left, y - _virtualScreen.Top, width, height);
    }

    // ---------------------------------------------------------------- input

    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonDown(e);
        if (_dragging) return;

        _dragging = true;
        _anchorDip = e.GetPosition(this);
        _selectionDip = new Int32Rect((int)_anchorDip.X, (int)_anchorDip.Y, 0, 0);
        HintPanel.Visibility = Visibility.Collapsed;
        CaptureMouse();
        e.Handled = true;
        UpdateDimGeometry();
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);

        var position = e.GetPosition(this);

        if (_dragging)
        {
            // Track the selection first so the read-outs below show the current position,
            // not the position of the previous mouse event.
            var x = (int)Math.Round(Math.Min(_anchorDip.X, position.X));
            var y = (int)Math.Round(Math.Min(_anchorDip.Y, position.Y));
            var width = (int)Math.Round(Math.Abs(position.X - _anchorDip.X));
            var height = (int)Math.Round(Math.Abs(position.Y - _anchorDip.Y));
            _selectionDip = new Int32Rect(x, y, width, height);
        }

        UpdateAiming(position);
        UpdateLoupe(position);

        if (_dragging) UpdateDimGeometry();
    }

    protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonUp(e);
        if (!_dragging) return;

        _dragging = false;
        ReleaseMouseCapture();

        var physical = SelectionPhysical();
        if (physical is not { } selection || selection.Width < 2 || selection.Height < 2)
        {
            // A click without a real drag: keep the overlay open so the user can retry.
            _selectionDip = null;
            HintPanel.Visibility = Visibility.Visible;
            LoupePanel.Visibility = Visibility.Collapsed;
            UpdateDimGeometry();
            return;
        }

        e.Handled = true;
        _session.Complete(selection);
    }

    protected override void OnMouseRightButtonUp(MouseButtonEventArgs e)
    {
        base.OnMouseRightButtonUp(e);
        _session.Complete(null);
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Key == Key.Escape)
        {
            e.Handled = true;
            _session.Complete(null);
        }
    }

    private void UpdateLoupe(Point positionDip)
    {
        if (!_dragging || _desktop.PixelWidth <= 0)
        {
            LoupePanel.Visibility = Visibility.Collapsed;
            return;
        }

        var physical = DipToPhysical(positionDip);
        var relativeX = physical.X - _virtualScreen.Left - LoupeSourceWidth / 2;
        var relativeY = physical.Y - _virtualScreen.Top - LoupeSourceHeight / 2;

        var cropX = (int)Math.Clamp(relativeX, 0, Math.Max(0, _desktop.PixelWidth - LoupeSourceWidth));
        var cropY = (int)Math.Clamp(relativeY, 0, Math.Max(0, _desktop.PixelHeight - LoupeSourceHeight));
        var cropWidth = Math.Min(LoupeSourceWidth, _desktop.PixelWidth - cropX);
        var cropHeight = Math.Min(LoupeSourceHeight, _desktop.PixelHeight - cropY);
        if (cropWidth < 1 || cropHeight < 1)
        {
            LoupePanel.Visibility = Visibility.Collapsed;
            return;
        }

        LoupeImage.Source = ScreenCaptureService.Crop(_desktop, new Int32Rect(cropX, cropY, cropWidth, cropHeight));
        LoupeImage.Width = LoupeZoomDip;
        LoupeImage.Height = LoupeZoomDipHeight;

        var selection = SelectionPhysical();
        LoupeCaption.Text = selection is { } bounds && _dragging
            ? $"{physical.X}, {physical.Y}   {bounds.Width} × {bounds.Height}"
            : $"{physical.X}, {physical.Y}";

        // Keep the loupe on screen: flip it when it would run off the edge.
        var offset = 22.0;
        var left = positionDip.X + offset;
        var top = positionDip.Y + offset;
        if (left + LoupeZoomDip + 8 > ActualWidth) left = positionDip.X - LoupeZoomDip - offset;
        if (top + LoupePanelHeight + 8 > ActualHeight) top = positionDip.Y - LoupePanelHeight - offset;
        left = Math.Clamp(left, 4, Math.Max(4, ActualWidth - LoupeZoomDip - 4));
        top = Math.Clamp(top, 4, Math.Max(4, ActualHeight - LoupePanelHeight - 4));

        LoupePanel.Margin = new Thickness(left, top, 0, 0);
        LoupePanel.Visibility = Visibility.Visible;

        // The magnifier carries the read-out while dragging, so the small coordinate
        // badge would only overlap it.
        CursorCoordBadge.Visibility = Visibility.Collapsed;
    }
}
