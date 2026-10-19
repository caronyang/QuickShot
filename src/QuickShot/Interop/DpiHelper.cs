using QuickShot.Interop;
using Drawing = System.Drawing;
using WinForms = System.Windows.Forms;

namespace QuickShot.Services;

/// <summary>
/// Physical-pixel / DIP conversion helpers.
/// The app is PerMonitorV2 aware, so every coordinate that comes out of the OS is a
/// physical pixel and every coordinate WPF gives us is a DIP local to the window.
/// </summary>
internal static class DpiHelper
{
    /// <summary>Whole virtual desktop (all monitors) in physical pixels.</summary>
    public static Drawing.Rectangle GetVirtualScreenPhysical() => new(
        NativeMethods.GetSystemMetrics(NativeMethods.SM_XVIRTUALSCREEN),
        NativeMethods.GetSystemMetrics(NativeMethods.SM_YVIRTUALSCREEN),
        NativeMethods.GetSystemMetrics(NativeMethods.SM_CXVIRTUALSCREEN),
        NativeMethods.GetSystemMetrics(NativeMethods.SM_CYVIRTUALSCREEN));

    public static Drawing.Rectangle ToDrawingRect(System.Windows.Rect r) =>
        new((int)Math.Round(r.X), (int)Math.Round(r.Y), (int)Math.Round(r.Width), (int)Math.Round(r.Height));

    /// <summary>Effective DPI scale (1.0 == 96 dpi) of the monitor that owns <paramref name="pt"/>.</summary>
    public static double GetScaleForPoint(Drawing.Point pt)
    {
        try
        {
            // Screen.Handle is not public on .NET Core, so ask the OS for the HMONITOR.
            var monitor = NativeMethods.MonitorFromPoint(
                new NativeMethods.POINT { X = pt.X, Y = pt.Y },
                NativeMethods.MONITOR_DEFAULTTONEAREST);

            if (monitor != IntPtr.Zero &&
                NativeMethods.GetDpiForMonitor(monitor, NativeMethods.MDT_EFFECTIVE_DPI, out var dpiX, out _) == 0 &&
                dpiX > 0)
            {
                return dpiX / 96.0;
            }
        }
        catch
        {
            // shcore.dll is missing (pre Windows 8.1) - fall through.
        }

        return 1.0;
    }

    public static double GetScaleForScreen(WinForms.Screen screen)
    {
        var bounds = screen.Bounds;
        return GetScaleForPoint(new Drawing.Point(
            bounds.Left + Math.Max(1, bounds.Width / 2),
            bounds.Top + Math.Max(1, bounds.Height / 2)));
    }

    /// <summary>Physical cursor position.</summary>
    public static Drawing.Point GetCursorPhysical() =>
        NativeMethods.GetCursorPos(out var pt) ? new Drawing.Point(pt.X, pt.Y) : Drawing.Point.Empty;

    /// <summary>
    /// The monitor that contains <paramref name="pt"/>, or the primary monitor when the
    /// point is outside every display.
    /// </summary>
    public static WinForms.Screen ScreenFromPoint(Drawing.Point pt) => WinForms.Screen.FromPoint(pt);
}
