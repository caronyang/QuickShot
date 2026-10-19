using System.Windows;
using System.Windows.Media.Imaging;
using QuickShot.Services;
using Drawing = System.Drawing;
using Forms = System.Windows.Forms;

namespace QuickShot.Views;

/// <summary>
/// Shows one <see cref="OverlayWindow"/> per monitor and waits for the user to finish a
/// selection (or to cancel).
/// </summary>
internal sealed class SelectionSession
{
    private readonly BitmapSource _desktop;
    private readonly Drawing.Rectangle _virtualScreen;
    private readonly List<OverlayWindow> _overlays = new();
    private readonly TaskCompletionSource<Int32Rect?> _completion =
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    private bool _finished;

    private SelectionSession(BitmapSource desktop, Drawing.Rectangle virtualScreen)
    {
        _desktop = desktop;
        _virtualScreen = virtualScreen;
    }

    /// <summary>
    /// <paramref name="desktop"/> must be the whole virtual desktop in physical pixels;
    /// the returned rect is relative to it.
    /// </summary>
    public static async Task<Int32Rect?> RunAsync(BitmapSource desktop, Drawing.Rectangle virtualScreen, Drawing.Point cursor)
    {
        var session = new SelectionSession(desktop, virtualScreen);
        return await session.StartAsync(cursor);
    }

    private async Task<Int32Rect?> StartAsync(Drawing.Point cursor)
    {
        var screens = Forms.Screen.AllScreens;

        foreach (var screen in screens)
        {
            var monitor = new Drawing.Rectangle(
                screen.Bounds.X, screen.Bounds.Y, screen.Bounds.Width, screen.Bounds.Height);

            var cropX = monitor.Left - _virtualScreen.Left;
            var cropY = monitor.Top - _virtualScreen.Top;
            var cropW = Math.Min(monitor.Width, _desktop.PixelWidth - cropX);
            var cropH = Math.Min(monitor.Height, _desktop.PixelHeight - cropY);
            if (cropW < 1 || cropH < 1) continue;

            var monitorImage = ScreenCaptureService.Crop(_desktop, new Int32Rect(cropX, cropY, cropW, cropH));

            OverlayWindow overlay;
            try
            {
                overlay = new OverlayWindow(_desktop, _virtualScreen, monitor, this, monitorImage);
                _overlays.Add(overlay);
                overlay.Show();
            }
            catch (Exception ex)
            {
                Log.Error($"Could not show the overlay for monitor {screen.DeviceName}", ex);
            }
        }

        if (_overlays.Count == 0)
        {
            _completion.TrySetResult(null);
            return await _completion.Task;
        }

        // The overlay under the cursor takes focus, so Esc and the mouse work where the
        // user is looking.
        var active = _overlays.FirstOrDefault(o => o.ContainsPhysical(cursor)) ?? _overlays[0];
        try
        {
            active.Activate();
            active.Focus();
        }
        catch (Exception ex)
        {
            Log.Warn($"Overlay activation failed: {ex.Message}");
        }

        return await _completion.Task;
    }

    public void Complete(Int32Rect? selection)
    {
        if (_finished) return;
        _finished = true;
        CloseAll();
        _completion.TrySetResult(selection);
    }

    internal void MarkDismissed(OverlayWindow overlay) => _overlays.Remove(overlay);

    private void CloseAll()
    {
        foreach (var overlay in _overlays.ToList())
        {
            try
            {
                overlay.Close();
            }
            catch (Exception ex)
            {
                Log.Warn($"Overlay close failed: {ex.Message}");
            }
        }

        _overlays.Clear();
    }
}
