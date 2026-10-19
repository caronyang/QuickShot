using System.Windows;
using System.Windows.Interop;
using QuickShot.Interop;

namespace QuickShot.Views;

internal static class WindowStyleHelper
{
    /// <summary>Keeps utility windows out of Alt+Tab and the task switcher.</summary>
    public static void HideFromAltTab(Window window)
    {
        try
        {
            var handle = new WindowInteropHelper(window).Handle;
            if (handle == IntPtr.Zero) return;

            var style = NativeMethods.GetWindowLongPtr(handle, NativeMethods.GWL_EXSTYLE).ToInt64();
            NativeMethods.SetWindowLongPtr(
                handle,
                NativeMethods.GWL_EXSTYLE,
                new IntPtr(style | NativeMethods.WS_EX_TOOLWINDOW));
        }
        catch (Exception ex)
        {
            Services.Log.Warn($"Could not set WS_EX_TOOLWINDOW: {ex.Message}");
        }
    }
}
