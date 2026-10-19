using QuickShot.Models;
using Drawing = System.Drawing;
using Forms = System.Windows.Forms;

namespace QuickShot.Services;

/// <summary>Tray icon, its (localised) context menu and balloon notifications.</summary>
public sealed class TrayService : IDisposable
{
    private readonly Forms.NotifyIcon _icon;
    private readonly Forms.ContextMenuStrip _menu = new() { ShowImageMargin = false };
    private readonly Forms.ToolStripMenuItem _captureItem = new();
    private readonly Forms.ToolStripMenuItem _settingsItem = new();
    private readonly Forms.ToolStripMenuItem _collapseAllItem = new();
    private readonly Forms.ToolStripMenuItem _closeAllItem = new();
    private readonly Forms.ToolStripMenuItem _exitItem = new();

    private SettingsService? _settings;
    private SnapshotManager? _snapshots;
    private bool _disposed;

    public TrayService()
    {
        _captureItem.Click += (_, _) => CaptureRequested?.Invoke(this, EventArgs.Empty);
        _settingsItem.Click += (_, _) => SettingsRequested?.Invoke(this, EventArgs.Empty);
        _collapseAllItem.Click += (_, _) => CollapseAllRequested?.Invoke(this, EventArgs.Empty);
        _closeAllItem.Click += (_, _) => CloseAllRequested?.Invoke(this, EventArgs.Empty);
        _exitItem.Click += (_, _) => ExitRequested?.Invoke(this, EventArgs.Empty);

        _menu.Items.AddRange(new Forms.ToolStripItem[]
        {
            _captureItem,
            new Forms.ToolStripSeparator(),
            _collapseAllItem,
            _closeAllItem,
            _settingsItem,
            new Forms.ToolStripSeparator(),
            _exitItem,
        });

        _menu.Opening += (_, _) => RefreshTexts();

        _icon = new Forms.NotifyIcon
        {
            Icon = LoadTrayIcon(),
            ContextMenuStrip = _menu,
            Visible = true,
        };
        _icon.DoubleClick += (_, _) => CaptureRequested?.Invoke(this, EventArgs.Empty);

        RefreshTexts();
    }

    public event EventHandler? CaptureRequested;

    public event EventHandler? SettingsRequested;

    public event EventHandler? CloseAllRequested;

    /// <summary>Toggles "collapse all shortcuts" / "restore all snapshots".</summary>
    public event EventHandler? CollapseAllRequested;

    public event EventHandler? ExitRequested;

    public void Attach(SettingsService settings, SnapshotManager snapshots)
    {
        _settings = settings;
        _snapshots = snapshots;
        RefreshTexts();
    }

    /// <summary>Re-applies the localised texts (call after a language change).</summary>
    public void RefreshTexts()
    {
        var loc = LocalizationService.Instance;
        var hotkey = FormatHotkey();

        _icon.Text = Truncate(loc.Format("Tray_Tooltip", hotkey), 127);
        _captureItem.Text = loc.Format("Tray_Capture", hotkey);
        _settingsItem.Text = loc["Tray_Settings"];
        _closeAllItem.Text = loc["Tray_CloseAll"];
        _closeAllItem.Enabled = _snapshots is { Count: > 0 };

        var anyExpanded = _snapshots?.AnyExpanded == true;
        _collapseAllItem.Text = loc[anyExpanded ? "Tray_CollapseAll" : "Tray_ExpandAll"];
        _collapseAllItem.Enabled = _snapshots is { Count: > 0 };

        _exitItem.Text = loc["Tray_Exit"];
    }

    public void Notify(string message) => Notify(message, Forms.ToolTipIcon.Info);

    public void NotifyWarning(string message) => Notify(message, Forms.ToolTipIcon.Warning);

    private void Notify(string message, Forms.ToolTipIcon icon)
    {
        if (_settings is { Current.Notifications: false }) return;

        try
        {
            _icon.BalloonTipTitle = LocalizationService.Instance["App_Name"];
            _icon.BalloonTipText = Truncate(message, 255);
            _icon.BalloonTipIcon = icon;
            _icon.ShowBalloonTip(4000);
        }
        catch (Exception ex)
        {
            Log.Warn($"Notification failed: {ex.Message}");
        }
    }

    private string FormatHotkey()
    {
        if (_settings is null) return string.Empty;
        return HotkeyGesture.TryParse(_settings.Current.Hotkey, out var gesture, out _) && gesture is not null
            ? gesture.Display
            : _settings.Current.Hotkey;
    }

    private static string Truncate(string value, int max) =>
        value.Length <= max ? value : value[..max];

    private static Drawing.Icon LoadTrayIcon()
    {
        try
        {
            var stream = System.Windows.Application.GetResourceStream(
                new Uri("pack://application:,,,/Assets/quickshot.ico"))?.Stream;
            if (stream is not null)
            {
                using (stream)
                {
                    return new Drawing.Icon(stream, Forms.SystemInformation.SmallIconSize);
                }
            }
        }
        catch (Exception ex)
        {
            Log.Warn($"Could not load the tray icon: {ex.Message}");
        }

        return Drawing.SystemIcons.Application;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        try
        {
            _icon.Visible = false;
            _icon.Dispose();
            _menu.Dispose();
        }
        catch (Exception ex)
        {
            Log.Warn($"Tray dispose failed: {ex.Message}");
        }
    }
}
