using System.Windows.Input;
using QuickShot.Interop;
using QuickShot.Services;
using WinForms = System.Windows.Forms;

namespace QuickShot.Models;

/// <summary>
/// A parsed global hotkey such as <c>Win+Shift+Q</c>.
/// Round-trips through <see cref="Display"/> so it can be stored in settings.json.
/// </summary>
public sealed class HotkeyGesture
{
    private const uint ModifierMask = NativeMethods.MOD_ALT | NativeMethods.MOD_CONTROL | NativeMethods.MOD_SHIFT | NativeMethods.MOD_WIN;

    public HotkeyGesture(uint modifiers, uint virtualKey)
    {
        Modifiers = modifiers & ModifierMask;
        VirtualKey = virtualKey;
    }

    public uint Modifiers { get; }

    public uint VirtualKey { get; }

    /// <summary>Canonical, parseable representation, e.g. "Ctrl+Shift+Q".</summary>
    public string Display
    {
        get
        {
            var parts = new List<string>(5);
            if ((Modifiers & NativeMethods.MOD_CONTROL) != 0) parts.Add("Ctrl");
            if ((Modifiers & NativeMethods.MOD_SHIFT) != 0) parts.Add("Shift");
            if ((Modifiers & NativeMethods.MOD_ALT) != 0) parts.Add("Alt");
            if ((Modifiers & NativeMethods.MOD_WIN) != 0) parts.Add("Win");
            parts.Add(KeyName(VirtualKey));
            return string.Join("+", parts);
        }
    }

    public override string ToString() => Display;

    public static bool IsModifierKey(Key key) => key
        is Key.LeftCtrl or Key.RightCtrl
        or Key.LeftShift or Key.RightShift
        or Key.LeftAlt or Key.RightAlt
        or Key.LWin or Key.RWin
        or Key.System;

    public static string KeyName(uint vk) => vk switch
    {
        >= 0x41 and <= 0x5A => ((char)vk).ToString(),   // A-Z
        >= 0x30 and <= 0x39 => ((char)vk).ToString(),   // 0-9
        _ => KeyInterop.KeyFromVirtualKey((int)vk).ToString(),
    };

    public static bool TryParse(string? text, out HotkeyGesture? gesture, out string? error)
    {
        gesture = null;
        error = null;
        var loc = LocalizationService.Instance;

        if (string.IsNullOrWhiteSpace(text))
        {
            error = loc["Error_InvalidHotkey"];
            return false;
        }

        uint modifiers = 0;
        uint? virtualKey = null;

        foreach (var raw in text.Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var token = raw.ToLowerInvariant();
            switch (token)
            {
                case "ctrl" or "control":
                    modifiers |= NativeMethods.MOD_CONTROL;
                    continue;
                case "shift":
                    modifiers |= NativeMethods.MOD_SHIFT;
                    continue;
                case "alt":
                    modifiers |= NativeMethods.MOD_ALT;
                    continue;
                case "win" or "windows" or "super" or "meta":
                    modifiers |= NativeMethods.MOD_WIN;
                    continue;
            }

            if (virtualKey is not null)
            {
                error = loc["Error_InvalidHotkey"];
                return false;
            }

            if (!TryParseKey(raw, out var vk))
            {
                error = loc["Error_InvalidHotkey"];
                return false;
            }

            virtualKey = vk;
        }

        if (virtualKey is null)
        {
            error = loc["Error.HotkeyNeedsKey"];
            return false;
        }

        if (modifiers == 0)
        {
            error = loc["Error.HotkeyNeedsModifier"];
            return false;
        }

        gesture = new HotkeyGesture(modifiers, virtualKey.Value);
        return true;
    }

    private static readonly Dictionary<string, uint> KeyAliases = new(StringComparer.OrdinalIgnoreCase)
    {
        ["up"] = 0x26, ["arrowup"] = 0x26, ["上"] = 0x26,
        ["down"] = 0x28, ["arrowdown"] = 0x28, ["下"] = 0x28,
        ["left"] = 0x25, ["arrowleft"] = 0x25, ["左"] = 0x25,
        ["right"] = 0x27, ["arrowright"] = 0x27, ["右"] = 0x27,
        ["pageup"] = 0x21, ["pagedown"] = 0x22,
        ["space"] = 0x20, ["空白"] = 0x20, ["空白鍵"] = 0x20,
        ["printscreen"] = 0x2C, ["prtsc"] = 0x2C, ["螢幕截取"] = 0x2C,
        ["enter"] = 0x0D, ["esc"] = 0x1B, ["escape"] = 0x1B,
        ["insert"] = 0x2D, ["ins"] = 0x2D, ["delete"] = 0x2E, ["del"] = 0x2E,
        ["home"] = 0x24, ["end"] = 0x23, ["tab"] = 0x09, ["backspace"] = 0x08,
    };

    private static bool TryParseKey(string token, out uint virtualKey)
    {
        virtualKey = 0;

        if (KeyAliases.TryGetValue(token, out var alias))
        {
            virtualKey = alias;
            return true;
        }

        if (token.Length == 1 && char.IsAsciiLetter(token[0]))
        {
            virtualKey = char.ToUpperInvariant(token[0]);
            return true;
        }

        if (token.Length == 1 && char.IsAsciiDigit(token[0]))
        {
            virtualKey = token[0];
            return true;
        }

        // Fall back to the WinForms key names: F1-F24, NumPad0, OemPlus, ...
        foreach (var name in Enum.GetNames<WinForms.Keys>())
        {
            if (!string.Equals(name, token, StringComparison.OrdinalIgnoreCase)) continue;
            var value = (uint)Enum.Parse<WinForms.Keys>(name);
            if (value == 0) return false;
            virtualKey = value;
            return true;
        }

        return false;
    }
}
