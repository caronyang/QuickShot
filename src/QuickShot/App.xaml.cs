using System.Windows;
using System.Windows.Threading;
using QuickShot.Interop;
using QuickShot.Models;
using QuickShot.Services;
using QuickShot.Views;
using Drawing = System.Drawing;

namespace QuickShot;

/// <summary>
/// Tray resident application: no main window, one instance, hotkey driven capture.
/// </summary>
public partial class App : System.Windows.Application
{
    private const string InstanceMutexName = @"Local\QuickShot.SingleInstance.v1";
    private const string ActivateEventName = @"Local\QuickShot.Activate.v1";
    private const string SettingsEventName = @"Local\QuickShot.Settings.v1";
    private const string ToggleEventName = @"Local\QuickShot.ToggleCollapse.v1";

    private readonly Dictionary<Snapshot, SnapshotWindow> _windows = new();
    private readonly SettingsService _settings = new();

    private Mutex? _mutex;
    private EventWaitHandle? _activateSignal;
    private EventWaitHandle? _settingsSignal;
    private EventWaitHandle? _toggleSignal;
    private RegisteredWaitHandle? _activateWait;
    private RegisteredWaitHandle? _settingsWait;
    private RegisteredWaitHandle? _toggleWait;
    private SnapshotManager _snapshots = null!;
    private TrayService _tray = null!;
    private HotkeyService _hotkeys = null!;
    private SettingsWindow? _settingsWindow;
    private bool _capturing;

    public static App Instance => (App)Current;

    public SettingsService Settings => _settings;

    public SnapshotManager Snapshots => _snapshots;

    public TrayService Tray => _tray;

    public HotkeyService Hotkeys => _hotkeys;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Only one QuickShot may own the hotkey. A second launch asks the running
        // instance to start a capture (or to show the settings) and exits.
        _mutex = new Mutex(initiallyOwned: true, InstanceMutexName, out var isFirstInstance);
        if (!isFirstInstance)
        {
            SignalRunningInstance(e.Args);
            Shutdown();
            return;
        }

        ShutdownMode = ShutdownMode.OnExplicitShutdown;
        DispatcherUnhandledException += OnDispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
            Log.Error("Unhandled exception", args.ExceptionObject as Exception ?? new Exception("unknown"));

        _settings.Load();
        LocalizationService.Instance.Initialize(_settings.Current.Language);

        _hotkeys = new HotkeyService();
        _snapshots = new SnapshotManager(_settings);
        _tray = new TrayService();

        _tray.Attach(_settings, _snapshots);
        _hotkeys.Pressed += (_, _) => _ = BeginCaptureAsync();
        _tray.CaptureRequested += (_, _) => _ = BeginCaptureAsync();
        _tray.SettingsRequested += (_, _) => OpenSettings();
        _tray.CloseAllRequested += (_, _) => CloseAllSnapshots();
        _tray.CollapseAllRequested += (_, _) => ToggleCollapseAll();
        _tray.ExitRequested += (_, _) => ExitApp();
        _snapshots.Changed += (_, _) => _tray.RefreshTexts();

        RegisterHotkey(notifyOnFailure: true);
        ApplyStartupEntry();
        StartActivateListener();

        // Optional start actions: "--capture" opens the selection overlay straight away,
        // "--settings" opens the settings dialog, "--collapse" rolls every snapshot up.
        // Handy for shortcuts, scripts and support.
        if (HasFlag(e.Args, "--capture")) _ = BeginCaptureAsync();
        if (HasFlag(e.Args, "--settings")) OpenSettings();
        if (HasFlag(e.Args, "--collapse")) ToggleCollapseAll();

        Log.Info($"QuickShot {typeof(App).Assembly.GetName().Version} started " +
                 $"(pid {Environment.ProcessId}) from {StartupService.ExecutablePath}");
    }

    // ------------------------------------------------------------- capture

    /// <summary>
    /// Full capture flow: freeze the desktop, let the user select a region on top of that
    /// frozen frame, encode the crop, then float it (asking which snapshot to replace when
    /// the limit is reached).
    /// </summary>
    private async Task BeginCaptureAsync()
    {
        Log.Debug($"Capture requested (capturing={_capturing})");
        if (_capturing) return;
        _capturing = true;

        try
        {
            var virtualScreen = DpiHelper.GetVirtualScreenPhysical();
            if (virtualScreen.Width < 2 || virtualScreen.Height < 2) return;

            var frozen = await Task.Run(() =>
            {
                var pixels = ScreenCaptureService.CaptureBgra(virtualScreen, out var width, out var height);
                return (Pixels: pixels, Width: width, Height: height);
            });

            var desktop = ScreenCaptureService.ToBitmapSource(frozen.Pixels, frozen.Width, frozen.Height);

            var cursor = DpiHelper.GetCursorPhysical();
            var selection = await SelectionSession.RunAsync(desktop, virtualScreen, cursor);
            if (selection is not { } region || region.Width < 2 || region.Height < 2)
            {
                Log.Debug("Capture cancelled");
                return;
            }

            var cropped = ScreenCaptureService.Crop(desktop, region);
            var png = await Task.Run(() => ScreenCaptureService.EncodePng(cropped));

            var previewWidth = Math.Min(region.Width, SnapshotWindow.PreviewMaxWidth);
            var preview = ScreenCaptureService.DecodePng(png, previewWidth);

            var bounds = new Drawing.Rectangle(
                virtualScreen.Left + region.X, virtualScreen.Top + region.Y, region.Width, region.Height);

            var snapshot = new Snapshot(png, region.Width, region.Height, bounds, preview)
            {
                IsPinned = _settings.Current.PinNewSnapshots,
            };

            ShowSnapshot(snapshot);
        }
        catch (Exception ex)
        {
            Log.Error("Capture failed", ex);
            _tray.NotifyWarning(ex.Message);
        }
        finally
        {
            _capturing = false;

            // A capture allocates a handful of one-shot full-desktop buffers (raw BGRA,
            // the frozen frame, the per-monitor crops). They are dead the moment the
            // floating snapshot is on screen, so collect them here instead of letting a
            // resident process sit on tens of megabytes until the next gen2 collection.
            GC.Collect(2, GCCollectionMode.Optimized);
        }
    }

    private void ShowSnapshot(Snapshot snapshot)
    {
        if (_snapshots.IsFull)
        {
            var victim = ReplaceSnapshotDialog.Prompt(_snapshots);
            if (victim is null)
            {
                Log.Info("Snapshot discarded: limit reached and nothing was replaced");
                _tray.Notify(LocalizationService.Instance.Format("Toast_Discarded", _snapshots.MaxCount));
                return;
            }

            if (_windows.TryGetValue(victim, out var victimWindow)) victimWindow.Close();
            _snapshots.Remove(victim);
        }

        var window = new SnapshotWindow(snapshot, _settings.Current);
        _windows[snapshot] = window;
        window.Closed += (_, _) =>
        {
            _windows.Remove(snapshot);
            _snapshots.Remove(snapshot);
        };

        _snapshots.Add(snapshot);
        window.Show();

        Log.Info($"Snapshot #{snapshot.Sequence} floated ({snapshot.SizeText}, {_snapshots.Count}/{_snapshots.MaxCount})");
    }

    // ------------------------------------------------------------ settings

    /// <summary>
    /// Validates and applies a settings candidate. Returns false (and leaves everything
    /// untouched) when the hotkey cannot be registered.
    /// </summary>
    internal bool ApplySettings(AppSettings candidate)
    {
        candidate.Normalize();

        if (!HotkeyGesture.TryParse(candidate.Hotkey, out var gesture, out var parseError) || gesture is null)
        {
            ShowError(parseError ?? LocalizationService.Instance["Error_InvalidHotkey"]);
            return false;
        }

        if (!_hotkeys.TryRegister(gesture, out var registerError))
        {
            ShowError(registerError ?? LocalizationService.Instance["Error_InvalidHotkey"]);
            return false;
        }

        _settings.Update(candidate);
        LocalizationService.Instance.SetLanguage(candidate.Language);
        ApplyStartupEntry();
        _tray.RefreshTexts();

        foreach (var window in _windows.Values) window.ApplySettings(_settings.Current);

        Log.Info($"Settings applied (hotkey {gesture.Display}, language {candidate.Language})");
        return true;
    }

    internal void OpenSettings()
    {
        if (_settingsWindow is { IsLoaded: true })
        {
            _settingsWindow.Activate();
            return;
        }

        _settingsWindow = new SettingsWindow(_settings, this);
        _settingsWindow.Closed += (_, _) => _settingsWindow = null;
        _settingsWindow.Show();
    }

    internal void CloseAllSnapshots()
    {
        foreach (var window in _windows.Values.ToList())
        {
            window.Close();
        }

        _windows.Clear();
        _snapshots.Clear();
        _tray.RefreshTexts();
    }

    /// <summary>
    /// One action for "these snapshots are eating my screen": if any snapshot is showing its
    /// full size they are all rolled up into thumbnails, otherwise (all already collapsed)
    /// they are all restored.
    /// </summary>
    internal void ToggleCollapseAll()
    {
        var collapse = _snapshots.AnyExpanded;

        foreach (var window in _windows.Values) window.SetCollapsed(collapse);

        _tray.RefreshTexts();
        Log.Info(collapse ? "All snapshots collapsed" : "All snapshots restored");
    }

    private void RegisterHotkey(bool notifyOnFailure)
    {
        var text = _settings.Current.Hotkey;

        if (!HotkeyGesture.TryParse(text, out var gesture, out var parseError) || gesture is null)
        {
            Log.Warn($"Invalid hotkey in settings: {text}");
            if (notifyOnFailure) _tray.NotifyWarning(parseError ?? LocalizationService.Instance["Error_InvalidHotkey"]);
            return;
        }

        if (_hotkeys.TryRegister(gesture, out var registerError)) return;

        Log.Warn($"Hotkey {gesture.Display} could not be registered");
        if (notifyOnFailure)
        {
            _tray.NotifyWarning(registerError ?? LocalizationService.Instance.Format("Error_HotkeyRegister", gesture.Display, 0));
        }
    }

    private void ApplyStartupEntry()
    {
        try
        {
            if (_settings.Current.AutoStart)
            {
                if (StartupService.IsEnabled()) return;

                if (StartupService.IsDevelopmentBuild)
                {
                    Log.Info("Development build: leaving the Windows startup entry untouched");
                    _tray.Notify(LocalizationService.Instance["Toast_StartupDev"]);
                    return;
                }

                StartupService.SetEnabled(true);
                return;
            }

            if (StartupService.IsEnabled()) StartupService.SetEnabled(false);
        }
        catch (Exception ex)
        {
            Log.Warn($"Startup entry handling failed: {ex.Message}");
        }
    }

    private void ShowError(string message)
    {
        try
        {
            if (_settingsWindow is { IsLoaded: true }) System.Windows.MessageBox.Show(_settingsWindow, message, LocalizationService.Instance["App_Name"], MessageBoxButton.OK, MessageBoxImage.Warning);
            else System.Windows.MessageBox.Show(message, LocalizationService.Instance["App_Name"], MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        catch (Exception ex)
        {
            Log.Warn($"Could not show the message box: {ex.Message}");
        }
    }

    // ------------------------------------------------------ single instance

    private void StartActivateListener()
    {
        try
        {
            _activateSignal = new EventWaitHandle(false, EventResetMode.AutoReset, ActivateEventName);
            _activateWait = ThreadPool.RegisterWaitForSingleObject(
                _activateSignal,
                (_, _) => Dispatch(() => _ = BeginCaptureAsync()),
                state: null,
                millisecondsTimeOutInterval: Timeout.Infinite,
                executeOnlyOnce: false);

            _settingsSignal = new EventWaitHandle(false, EventResetMode.AutoReset, SettingsEventName);
            _settingsWait = ThreadPool.RegisterWaitForSingleObject(
                _settingsSignal,
                (_, _) => Dispatch(OpenSettings),
                state: null,
                millisecondsTimeOutInterval: Timeout.Infinite,
                executeOnlyOnce: false);

            _toggleSignal = new EventWaitHandle(false, EventResetMode.AutoReset, ToggleEventName);
            _toggleWait = ThreadPool.RegisterWaitForSingleObject(
                _toggleSignal,
                (_, _) => Dispatch(ToggleCollapseAll),
                state: null,
                millisecondsTimeOutInterval: Timeout.Infinite,
                executeOnlyOnce: false);
        }
        catch (Exception ex)
        {
            Log.Warn($"Could not listen for a second instance: {ex.Message}");
        }
    }

    private void Dispatch(Action action)
    {
        try
        {
            Dispatcher.BeginInvoke(action);
        }
        catch (Exception ex)
        {
            Log.Warn($"Could not dispatch to the UI thread: {ex.Message}");
        }
    }

    private static void SignalRunningInstance(string[] args)
    {
        var eventName = ActivateEventName;
        if (HasFlag(args, "--settings")) eventName = SettingsEventName;
        else if (HasFlag(args, "--collapse")) eventName = ToggleEventName;

        try
        {
            if (EventWaitHandle.TryOpenExisting(eventName, out var handle))
            {
                using (handle) handle.Set();
            }
        }
        catch (Exception ex)
        {
            Log.Warn($"Could not signal the running instance: {ex.Message}");
        }
    }

    private static bool HasFlag(string[] args, string flag) =>
        args.Any(arg => string.Equals(arg, flag, StringComparison.OrdinalIgnoreCase));

    // --------------------------------------------------------------- exit

    private void ExitApp()
    {
        Log.Info("Exiting");
        Shutdown();
    }

    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        Log.Error("Unhandled dispatcher exception", e.Exception);
        e.Handled = true;
    }

    protected override void OnExit(ExitEventArgs e)
    {
        try
        {
            _activateWait?.Unregister(null);
            _settingsWait?.Unregister(null);
            _toggleWait?.Unregister(null);
            _activateSignal?.Dispose();
            _settingsSignal?.Dispose();
            _toggleSignal?.Dispose();
            _hotkeys?.Dispose();
            _tray?.Dispose();
            _mutex?.Dispose();
        }
        catch (Exception ex)
        {
            Log.Warn($"Shutdown cleanup failed: {ex.Message}");
        }

        base.OnExit(e);
    }
}
