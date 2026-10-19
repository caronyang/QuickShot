using System.Runtime.InteropServices;
using System.Windows.Interop;
using QuickShot.Interop;
using QuickShot.Models;

namespace QuickShot.Services;

/// <summary>
/// Registers the global hotkey on a hidden message-only window and raises
/// <see cref="Pressed"/> when it fires.
/// </summary>
public sealed class HotkeyService : IDisposable
{
    private const int HotkeyId = 0x5153; // "QS"

    private HwndSource? _sink;
    private bool _disposed;

    public event EventHandler? Pressed;

    public HotkeyGesture? Current { get; private set; }

    /// <summary>
    /// Registers <paramref name="gesture"/>. On failure the previously working hotkey is
    /// restored, so a bad edit in the settings dialog can never leave the app deaf.
    /// </summary>
    public bool TryRegister(HotkeyGesture gesture, out string? error)
    {
        error = null;
        var previous = Current;

        try
        {
            var hwnd = EnsureSink();
            UnregisterCore(hwnd);

            if (NativeMethods.RegisterHotKey(hwnd, HotkeyId, gesture.Modifiers | NativeMethods.MOD_NOREPEAT, gesture.VirtualKey))
            {
                Current = gesture;
                Log.Info($"Hotkey registered: {gesture.Display}");
                return true;
            }

            var code = Marshal.GetLastWin32Error();
            error = code == NativeMethods.ERROR_HOTKEY_ALREADY_REGISTERED
                ? LocalizationService.Instance.Format("Error_HotkeyTaken", gesture.Display)
                : LocalizationService.Instance.Format("Error_HotkeyRegister", gesture.Display, code);
            Log.Warn($"Hotkey registration failed for {gesture.Display} (win32 {code})");
        }
        catch (Exception ex)
        {
            error = ex.Message;
            Log.Error("Hotkey registration threw", ex);
        }

        if (previous is not null)
        {
            try
            {
                var hwnd = EnsureSink();
                if (NativeMethods.RegisterHotKey(hwnd, HotkeyId, previous.Modifiers | NativeMethods.MOD_NOREPEAT, previous.VirtualKey))
                {
                    Current = previous;
                }
            }
            catch (Exception ex)
            {
                Log.Error("Could not restore the previous hotkey", ex);
            }
        }

        return false;
    }

    public void Unregister()
    {
        if (_sink is null) return;
        UnregisterCore(_sink.Handle);
        Current = null;
    }

    private void UnregisterCore(IntPtr hwnd)
    {
        try
        {
            NativeMethods.UnregisterHotKey(hwnd, HotkeyId);
        }
        catch (Exception ex)
        {
            Log.Warn($"UnregisterHotKey failed: {ex.Message}");
        }
    }

    private IntPtr EnsureSink()
    {
        if (_sink is not null) return _sink.Handle;

        var parameters = new HwndSourceParameters("QuickShot.HotkeySink")
        {
            Width = 0,
            Height = 0,
            PositionX = 0,
            PositionY = 0,
            WindowStyle = 0,
            ExtendedWindowStyle = 0,
            ParentWindow = NativeMethods.HWND_MESSAGE,
        };

        _sink = new HwndSource(parameters);
        _sink.AddHook(SinkHook);
        Log.Debug($"Hotkey sink created: hwnd=0x{_sink.Handle.ToInt64():X}");
        return _sink.Handle;
    }

    private IntPtr SinkHook(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == NativeMethods.WM_HOTKEY && wParam.ToInt32() == HotkeyId)
        {
            handled = true;
            Log.Debug($"WM_HOTKEY received on hwnd=0x{hwnd.ToInt64():X}");
            try
            {
                Pressed?.Invoke(this, EventArgs.Empty);
            }
            catch (Exception ex)
            {
                Log.Error("Hotkey handler threw", ex);
            }
        }

        return IntPtr.Zero;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        try
        {
            if (_sink is not null)
            {
                UnregisterCore(_sink.Handle);
                _sink.RemoveHook(SinkHook);
                _sink.Dispose();
                _sink = null;
            }
        }
        catch (Exception ex)
        {
            Log.Warn($"Hotkey sink dispose failed: {ex.Message}");
        }
    }
}
