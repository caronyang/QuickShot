using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using QuickShot.Interop;
using QuickShot.Models;
using QuickShot.Services;
using Drawing = System.Drawing;

namespace QuickShot.Views;

/// <summary>
/// One floating snapshot: draggable, always-on-top by choice, with four very small
/// controls (keep on top / save / collapse / close) that only exist while the pointer
/// is inside the card, so they never cover the screenshot.
/// </summary>
public partial class SnapshotWindow : Window
{
    /// <summary>Longest edge of the preview bitmap that lives in memory per snapshot.</summary>
    public const int PreviewMaxWidth = 1600;

    private const double ShadowMarginDip = 8;
    private const double MaxScreenFraction = 0.7;
    private const int MaxPlacementAttempts = 3;

    // Rolled-up thumbnail box, in physical pixels. The maximum keeps a screen full of
    // snapshots tidy; the minimum keeps the card big enough to drag and hit its buttons.
    private const int ThumbnailMaxWidth = 180;
    private const int ThumbnailMaxHeight = 135;
    private const int ThumbnailMinWidth = 108;
    private const int ThumbnailMinHeight = 72;

    private readonly Snapshot _snapshot;
    private readonly Drawing.Point _anchorPoint;

    private IntPtr _hwnd;
    private int _placementAttempts;
    private bool _zoomedToFull;
    private bool _collapsed;
    private double _fitScale = 1.0;
    private int _displayWidth;
    private int _displayHeight;

    public SnapshotWindow(Snapshot snapshot, AppSettings settings)
    {
        _snapshot = snapshot;

        InitializeComponent();

        _anchorPoint = new Drawing.Point(
            snapshot.PhysicalBounds.Left + Math.Max(1, snapshot.PhysicalBounds.Width / 2),
            snapshot.PhysicalBounds.Top + Math.Max(1, snapshot.PhysicalBounds.Height / 2));

        ShotImage.Source = snapshot.Preview;
        Card.MouseLeftButtonDown += OnCardMouseLeftButtonDown;
        Card.MouseRightButtonUp += OnCardMouseRightButtonUp;

        // Clicks on the toolbar's own padding must not start a drag of the snapshot.
        Toolbar.MouseLeftButtonDown += (_, e) => e.Handled = true;

        // The toolbar is tied to the pointer: it appears the moment the pointer is over
        // the card and disappears the moment it leaves, so the image stays unobstructed.
        Card.MouseEnter += (_, _) => ShowToolbar();
        Card.MouseLeave += (_, _) => HideToolbar();
        Card.MouseMove += (_, _) => ShowToolbar();

        Loaded += OnLoaded;
        DpiChanged += OnDpiChanged;

        ApplySettings(settings);
        ComputeDisplaySize();
        UpdateCollapseVisual();
    }

    public Snapshot Snapshot => _snapshot;

    /// <summary>Re-applies opacity / pinning after the settings dialog was saved.</summary>
    public void ApplySettings(AppSettings settings)
    {
        Opacity = Math.Clamp(settings.SnapshotOpacity, 0.3, 1.0);
        if (!_settingsApplied)
        {
            Topmost = settings.PinNewSnapshots;
            _snapshot.IsPinned = Topmost;
            _settingsApplied = true;
        }

        UpdatePinVisual();
        UpdateScaleBadge();
        UpdateCollapseVisual();
    }

    private bool _settingsApplied;

    // ------------------------------------------------------------- placement

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        _hwnd = new WindowInteropHelper(this).Handle;
        WindowStyleHelper.HideFromAltTab(this);
        EnsurePhysicalPlacement();
        UpdateScaleBadge();

        // Usually the pointer is still where the region was dragged out, i.e. on the
        // card - show the controls then; otherwise they stay hidden until it arrives.
        Dispatcher.BeginInvoke(new Action(SyncToolbarWithPointer), DispatcherPriority.Input);
    }

    /// <summary>
    /// Shows or hides the controls to match the pointer's current position. Used once the
    /// card has been placed, because a window that simply appears under a resting pointer
    /// receives no enter / move event to react to.
    /// </summary>
    private void SyncToolbarWithPointer()
    {
        if (!IsLoaded) return;

        var position = Mouse.GetPosition(Card);
        var inside = position.X >= 0 && position.Y >= 0 &&
                     position.X <= Card.ActualWidth && position.Y <= Card.ActualHeight;

        if (inside) ShowToolbar();
        else HideToolbar();
    }

    private void OnDpiChanged(object sender, DpiChangedEventArgs e)
    {
        Dispatcher.BeginInvoke(new Action(() =>
        {
            ComputeDisplaySize();
            if (_placementAttempts < MaxPlacementAttempts) EnsurePhysicalPlacement();
        }), DispatcherPriority.Loaded);
    }

    /// <summary>
    /// Sizes the bitmap on screen. Small regions are shown 1:1 (the floating snapshot is
    /// then exactly as large as the captured area); large ones are scaled to at most 70%
    /// of the monitor, with a "%" badge and a double click to see it at 100%.
    /// Rolled-up snapshots show a thumbnail box instead.
    /// </summary>
    private void ComputeDisplaySize()
    {
        if (_collapsed)
        {
            var (boxWidth, boxHeight) = ThumbnailBox();
            _displayWidth = boxWidth;
            _displayHeight = boxHeight;

            // Letterbox rather than stretch, so a very wide or very narrow capture still
            // reads as a picture inside a uniform small card.
            ShotImage.Stretch = Stretch.Uniform;
        }
        else
        {
            var bounds = DpiHelper.ScreenFromPoint(_anchorPoint).Bounds;

            var scale = Math.Min(1.0, Math.Min(
                bounds.Width * MaxScreenFraction / _snapshot.PixelWidth,
                bounds.Height * MaxScreenFraction / _snapshot.PixelHeight));

            _fitScale = double.IsFinite(scale) && scale > 0 ? scale : 1.0;

            var factor = _zoomedToFull ? 1.0 : _fitScale;
            _displayWidth = Math.Max(1, (int)Math.Round(_snapshot.PixelWidth * factor));
            _displayHeight = Math.Max(1, (int)Math.Round(_snapshot.PixelHeight * factor));

            ShotImage.Stretch = Stretch.Fill;
        }

        var dpi = VisualTreeHelper.GetDpi(this);
        var dipScaleX = dpi.DpiScaleX > 0 ? dpi.DpiScaleX : 1.0;
        var dipScaleY = dpi.DpiScaleY > 0 ? dpi.DpiScaleY : 1.0;

        ShotImage.Width = _displayWidth / dipScaleX;
        ShotImage.Height = _displayHeight / dipScaleY;

        UpdateScaleBadge();
    }

    /// <summary>
    /// Aspect-preserving thumbnail box. Each dimension is clamped into [min, max]
    /// independently, so an extreme aspect ratio (a one-line strip, a tall column) still
    /// lands in a usable card; the image letterboxes itself inside.
    /// </summary>
    private (int Width, int Height) ThumbnailBox()
    {
        var width = Math.Max(1, _snapshot.PixelWidth);
        var height = Math.Max(1, _snapshot.PixelHeight);

        var factor = Math.Min(ThumbnailMaxWidth / (double)width, ThumbnailMaxHeight / (double)height);

        var boxWidth = Math.Clamp(width * factor, ThumbnailMinWidth, ThumbnailMaxWidth);
        var boxHeight = Math.Clamp(height * factor, ThumbnailMinHeight, ThumbnailMaxHeight);

        return (Math.Max(1, (int)Math.Round(boxWidth)), Math.Max(1, (int)Math.Round(boxHeight)));
    }

    private void EnsurePhysicalPlacement()
    {
        if (_hwnd == IntPtr.Zero) return;
        _placementAttempts++;

        NativeMethods.GetWindowRect(_hwnd, out var rect);
        var scale = DpiHelper.GetScaleForPoint(_anchorPoint);
        var margin = (int)Math.Round(ShadowMarginDip * scale);

        var x = _snapshot.PhysicalBounds.Left - margin;
        var y = _snapshot.PhysicalBounds.Top - margin;

        // Keep it reachable if the region was at the far edge of a monitor.
        var monitor = DpiHelper.ScreenFromPoint(_anchorPoint).Bounds;
        var width = rect.Width > 0 ? rect.Width : _displayWidth;
        var height = rect.Height > 0 ? rect.Height : _displayHeight;
        x = Math.Clamp(x, monitor.Left, Math.Max(monitor.Left, monitor.Right - width));
        y = Math.Clamp(y, monitor.Top, Math.Max(monitor.Top, monitor.Bottom - height));

        NativeMethods.SetWindowPos(
            _hwnd, Topmost ? NativeMethods.HWND_TOPMOST : IntPtr.Zero,
            x, y, 0, 0,
            NativeMethods.SWP_NOSIZE | NativeMethods.SWP_NOACTIVATE | NativeMethods.SWP_SHOWWINDOW);
    }

    // ------------------------------------------------------------- interaction

    private void OnCardMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState != MouseButtonState.Pressed) return;

        if (e.ClickCount == 2)
        {
            ToggleZoom();
            e.Handled = true;
            return;
        }

        try
        {
            DragMove();
        }
        catch (InvalidOperationException)
        {
            // The button was released before the drag loop started - nothing to do.
        }
    }

    private void OnCardMouseRightButtonUp(object sender, MouseButtonEventArgs e)
    {
        // Rebuilt every time so the texts follow the current language and pin state.
        var menu = BuildContextMenu();
        menu.PlacementTarget = Card;
        menu.Placement = System.Windows.Controls.Primitives.PlacementMode.MousePoint;
        menu.IsOpen = true;
        e.Handled = true;
    }

    private void ToggleZoom()
    {
        _zoomedToFull = !_zoomedToFull;

        if (_zoomedToFull)
        {
            // 1:1 needs the real pixels; the preview stays around for going back.
            try
            {
                ShotImage.Source = _snapshot.GetFullImage();
            }
            catch (Exception ex)
            {
                Log.Warn($"Could not decode the full size snapshot: {ex.Message}");
                _zoomedToFull = false;
            }
        }
        else
        {
            ShotImage.Source = _snapshot.Preview;
            _snapshot.ReleaseFullImage();
        }

        ComputeDisplaySize();
        EnsurePhysicalPlacement();
    }

    private void ShowToolbar()
    {
        if (Toolbar.Visibility == Visibility.Visible) return;
        Toolbar.Visibility = Visibility.Visible;
    }

    private void HideToolbar()
    {
        Toolbar.Visibility = Visibility.Collapsed;
    }

    private void UpdatePinVisual()
    {
        // State, not action: a highlighted solid pin means "kept on top", a muted
        // crossed-out pin means "normal window". The tooltip names the action.
        PinButton.Content = Topmost ? "\uE718" : "\uE77A";
        PinButton.Foreground = (TryFindResource(Topmost ? "AccentBrush" : "CardTextBrush") as System.Windows.Media.Brush)
                               ?? System.Windows.Media.Brushes.White;
        PinButton.ToolTip = LocalizationService.Instance[Topmost ? "Snapshot_Unpin" : "Snapshot_Pin"];
    }

    private void UpdateScaleBadge()
    {
        if (_collapsed)
        {
            // A scale read-out is meaningless on a thumbnail.
            ScaleBadge.Visibility = Visibility.Collapsed;
            return;
        }

        var percent = (int)Math.Round((_zoomedToFull ? 1.0 : _fitScale) * 100);
        if (percent >= 100)
        {
            ScaleBadge.Visibility = Visibility.Collapsed;
            return;
        }

        ScaleText.Text = LocalizationService.Instance.Format("Snapshot_Scale", percent);
        ScaleBadge.Visibility = Visibility.Visible;
    }

    // ------------------------------------------------------------- commands

    private void OnPinClick(object sender, RoutedEventArgs e)
    {
        TogglePin();
        e.Handled = true;
    }

    private void TogglePin()
    {
        Topmost = !Topmost;
        _snapshot.IsPinned = Topmost;
        UpdatePinVisual();
        ApplyTopmost();
    }

    /// <summary>
    /// Re-asserts the z-order. Deliberately does not reposition: the user may have dragged
    /// the window away from where it was captured.
    /// </summary>
    private void ApplyTopmost()
    {
        if (_hwnd == IntPtr.Zero) return;

        NativeMethods.SetWindowPos(
            _hwnd,
            Topmost ? NativeMethods.HWND_TOPMOST : NativeMethods.HWND_NOTOPMOST,
            0, 0, 0, 0,
            NativeMethods.SWP_NOMOVE | NativeMethods.SWP_NOSIZE | NativeMethods.SWP_NOACTIVATE);
    }

    // ------------------------------------------------------------ collapse

    /// <summary>Rolls the window up (or back down); used by the tray's collapse-all action.</summary>
    internal void SetCollapsed(bool collapsed)
    {
        if (_collapsed == collapsed) return;
        ToggleCollapse();
    }

    private void OnCollapseClick(object sender, RoutedEventArgs e)
    {
        ToggleCollapse();
        e.Handled = true;
    }

    /// <summary>
    /// Shrinks the floating snapshot to a small thumbnail, or restores it to exactly the
    /// size it had before. The window keeps its top-left corner, so a collapsed snapshot
    /// stays where the user put it.
    /// </summary>
    private void ToggleCollapse()
    {
        var (originX, originY) = CurrentPhysicalOrigin();

        _collapsed = !_collapsed;
        _snapshot.IsCollapsed = _collapsed;

        if (_collapsed && _zoomedToFull)
        {
            // 1:1 pixels are pointless inside a thumbnail - hand the memory back.
            _zoomedToFull = false;
            ShotImage.Source = _snapshot.Preview;
            _snapshot.ReleaseFullImage();
        }

        ComputeDisplaySize();
        UpdateCollapseVisual();

        // Re-anchor once the SizeToContent pass has run, so the corner really stays put.
        UpdateLayout();
        RestorePhysicalOrigin(originX, originY);
    }

    private (int X, int Y) CurrentPhysicalOrigin()
    {
        if (_hwnd != IntPtr.Zero && NativeMethods.GetWindowRect(_hwnd, out var rect))
        {
            return (rect.Left, rect.Top);
        }

        return (_snapshot.PhysicalBounds.Left, _snapshot.PhysicalBounds.Top);
    }

    private void RestorePhysicalOrigin(int x, int y)
    {
        if (_hwnd == IntPtr.Zero) return;

        NativeMethods.SetWindowPos(
            _hwnd, IntPtr.Zero, x, y, 0, 0,
            NativeMethods.SWP_NOSIZE | NativeMethods.SWP_NOACTIVATE | NativeMethods.SWP_NOZORDER);
    }

    private void UpdateCollapseVisual()
    {
        var loc = LocalizationService.Instance;
        CollapseButton.Content = _collapsed ? "\uE70D" : "\uE70E";
        CollapseButton.ToolTip = loc[_collapsed ? "Snapshot_Expand" : "Snapshot_Collapse"];
    }

    private void OnSaveClick(object sender, RoutedEventArgs e)
    {
        SaveToDefaultFolder();
        e.Handled = true;
    }

    private void OnSaveAsClick(object sender, MouseButtonEventArgs e)
    {
        SaveAs();
        e.Handled = true;
    }

    private void OnCloseClick(object sender, RoutedEventArgs e)
    {
        Close();
        e.Handled = true;
    }

    private ContextMenu BuildContextMenu()
    {
        var loc = LocalizationService.Instance;
        var menu = new ContextMenu { Style = (Style)FindResource("SnapshotContextMenu") };

        menu.Items.Add(MenuItem(loc["Menu_Save"], (_, _) => SaveToDefaultFolder()));
        menu.Items.Add(MenuItem(loc["Menu_SaveAs"], (_, _) => SaveAs()));
        menu.Items.Add(MenuItem(loc["Menu_Copy"], (_, _) => CopyToClipboard()));
        menu.Items.Add(MenuItem(loc["Menu_OpenFolder"], (_, _) => OpenSaveFolder()));
        menu.Items.Add(new Separator());
        menu.Items.Add(MenuItem(loc[_collapsed ? "Menu_Expand" : "Menu_Collapse"], (_, _) => ToggleCollapse()));
        menu.Items.Add(MenuItem(loc[Topmost ? "Menu_Unpin" : "Menu_Pin"], (_, _) => TogglePin()));
        menu.Items.Add(MenuItem(loc["Menu_Close"], (_, _) => Close()));
        return menu;
    }

    private static MenuItem MenuItem(string header, RoutedEventHandler handler)
    {
        var item = new MenuItem
        {
            Header = header,
            Style = (Style)System.Windows.Application.Current.FindResource("SnapshotMenuItem"),
        };
        item.Click += handler;
        return item;
    }

    private void SaveToDefaultFolder()
    {
        try
        {
            var folder = SettingsFolder();
            Directory.CreateDirectory(folder);

            var name = $"QuickShot_{_snapshot.CreatedLocal:yyyyMMdd_HHmmss}.png";
            var path = MakeUnique(Path.Combine(folder, name));
            File.WriteAllBytes(path, _snapshot.Png);

            Log.Info($"Snapshot saved: {path}");
            Notify(LocalizationService.Instance.Format("Toast_Saved", path), NotificationKind.Success);
        }
        catch (Exception ex)
        {
            Log.Error("Saving the snapshot failed", ex);
            Notify(LocalizationService.Instance.Format("Toast_SaveFailed", ex.Message), NotificationKind.Warning);
        }
    }

    private string SettingsFolder() =>
        App.Instance?.Settings.Current.SaveFolder ?? AppSettings.DefaultSaveFolder;

    internal void SaveAs()
    {
        var loc = LocalizationService.Instance;
        var dialog = new Microsoft.Win32.SaveFileDialog
        {
            Title = loc["Menu_SaveAs"],
            Filter = "PNG image (*.png)|*.png",
            DefaultExt = ".png",
            FileName = $"QuickShot_{_snapshot.CreatedLocal:yyyyMMdd_HHmmss}.png",
            InitialDirectory = Directory.Exists(SettingsFolder()) ? SettingsFolder() : AppSettings.DefaultSaveFolder,
        };

        try
        {
            if (dialog.ShowDialog(this) != true) return;
            File.WriteAllBytes(dialog.FileName, _snapshot.Png);
            Log.Info($"Snapshot saved as: {dialog.FileName}");
            Notify(loc.Format("Toast_Saved", dialog.FileName), NotificationKind.Success);
        }
        catch (Exception ex)
        {
            Log.Error("Save as failed", ex);
            Notify(loc.Format("Toast_SaveFailed", ex.Message), NotificationKind.Warning);
        }
    }

    internal void CopyToClipboard()
    {
        var loc = LocalizationService.Instance;
        try
        {
            Clipboard.SetImage(_snapshot.GetFullImage());
            Notify(loc["Toast_Copied"], NotificationKind.Success);
        }
        catch (Exception ex)
        {
            Log.Error("Copying to the clipboard failed", ex);
        }
        finally
        {
            _snapshot.ReleaseFullImage();
        }
    }

    internal void OpenSaveFolder()
    {
        try
        {
            var folder = SettingsFolder();
            Directory.CreateDirectory(folder);
            Process.Start(new ProcessStartInfo("explorer.exe", $"\"{folder}\"") { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            Log.Warn($"Could not open the save folder: {ex.Message}");
        }
    }

    private static void Notify(string message, NotificationKind kind)
    {
        var tray = App.Instance?.Tray;
        if (tray is null) return;
        if (kind == NotificationKind.Warning) tray.NotifyWarning(message);
        else tray.Notify(message);
    }

    private static string MakeUnique(string path)
    {
        if (!File.Exists(path)) return path;

        var directory = Path.GetDirectoryName(path)!;
        var name = Path.GetFileNameWithoutExtension(path);
        var extension = Path.GetExtension(path);

        for (var i = 2; i < 1000; i++)
        {
            var candidate = Path.Combine(directory, $"{name}_{i}{extension}");
            if (!File.Exists(candidate)) return candidate;
        }

        return path;
    }
}

internal enum NotificationKind
{
    Info,
    Success,
    Warning,
}
