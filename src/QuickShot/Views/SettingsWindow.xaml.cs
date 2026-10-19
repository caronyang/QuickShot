using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using QuickShot.Interop;
using QuickShot.Models;
using QuickShot.Services;

namespace QuickShot.Views;

/// <summary>Settings dialog: language, hotkey, start with Windows, save folder, limits.</summary>
public partial class SettingsWindow : Window
{
    private readonly SettingsService _settings;
    private readonly App _app;
    private readonly string _originalLanguage;

    private HotkeyGesture _gesture;
    private bool _initialised;
    private bool _applied;

    public SettingsWindow(SettingsService settings, App app)
    {
        InitializeComponent();

        _settings = settings;
        _app = app;
        var current = settings.Current;
        _originalLanguage = current.Language;

        _gesture = HotkeyGesture.TryParse(current.Hotkey, out var parsed, out _) && parsed is not null
            ? parsed
            : new HotkeyGesture(NativeMethods.MOD_WIN | NativeMethods.MOD_SHIFT, 0x51);

        LanguageCombo.ItemsSource = LocalizationService.Supported.Select(language => language.NativeName).ToList();
        var languageIndex = Array.FindIndex(LocalizationService.Supported, l => l.Code == current.Language);
        LanguageCombo.SelectedIndex = languageIndex < 0 ? 0 : languageIndex;

        HotkeyBox.Text = _gesture.Display;
        AutoStartCheck.IsChecked = current.AutoStart;
        SaveFolderBox.Text = current.SaveFolder;
        MaxSlider.Value = Math.Clamp(current.MaxSnapshots, (int)MaxSlider.Minimum, (int)MaxSlider.Maximum);
        OpacitySlider.Value = Math.Clamp(current.SnapshotOpacity, OpacitySlider.Minimum, OpacitySlider.Maximum);
        NotifyCheck.IsChecked = current.Notifications;
        PinCheck.IsChecked = current.PinNewSnapshots;

        UpdateMaxLabel();
        UpdateOpacityLabel();

        _initialised = true;
    }

    // ------------------------------------------------------------ language

    private void OnLanguageChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_initialised) return;
        var index = LanguageCombo.SelectedIndex;
        if (index < 0 || index >= LocalizationService.Supported.Length) return;
        LocalizationService.Instance.SetLanguage(LocalizationService.Supported[index].Code);
    }

    // ------------------------------------------------------------- hotkey

    private void OnHotkeyKeyDown(object sender, KeyEventArgs e)
    {
        var key = e.Key == Key.System ? e.SystemKey : e.Key;
        e.Handled = true;

        if (key == Key.Escape)
        {
            HotkeyBox.Text = _gesture.Display;
            HideHotkeyWarning();
            return;
        }

        if (HotkeyGesture.IsModifierKey(key)) return;

        var virtualKey = (uint)KeyInterop.VirtualKeyFromKey(key);
        if (virtualKey == 0) return;

        uint modifiers = 0;
        var pressed = Keyboard.Modifiers;
        if (pressed.HasFlag(ModifierKeys.Control)) modifiers |= NativeMethods.MOD_CONTROL;
        if (pressed.HasFlag(ModifierKeys.Shift)) modifiers |= NativeMethods.MOD_SHIFT;
        if (pressed.HasFlag(ModifierKeys.Alt)) modifiers |= NativeMethods.MOD_ALT;
        if (pressed.HasFlag(ModifierKeys.Windows)) modifiers |= NativeMethods.MOD_WIN;

        if (modifiers == 0)
        {
            ShowHotkeyWarning(LocalizationService.Instance["Error_HotkeyNeedsModifier"]);
            return;
        }

        HideHotkeyWarning();
        _gesture = new HotkeyGesture(modifiers, virtualKey);
        HotkeyBox.Text = _gesture.Display;
    }

    private void OnHotkeyResetClick(object sender, RoutedEventArgs e)
    {
        if (HotkeyGesture.TryParse(AppSettings.DefaultHotkey, out var gesture, out _) && gesture is not null)
        {
            _gesture = gesture;
            HotkeyBox.Text = gesture.Display;
            HideHotkeyWarning();
        }
    }

    private void ShowHotkeyWarning(string message)
    {
        HotkeyWarning.Text = message;
        HotkeyWarning.Visibility = Visibility.Visible;
    }

    private void HideHotkeyWarning()
    {
        HotkeyWarning.Visibility = Visibility.Collapsed;
        HotkeyWarning.Text = string.Empty;
    }

    // --------------------------------------------------------------- misc

    private void OnBrowseClick(object sender, RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.OpenFolderDialog
        {
            Title = LocalizationService.Instance["Settings_SaveFolder"],
            InitialDirectory = Directory.Exists(SaveFolderBox.Text) ? SaveFolderBox.Text : AppSettings.DefaultSaveFolder,
        };

        if (dialog.ShowDialog(this) == true)
        {
            SaveFolderBox.Text = dialog.FolderName;
        }
    }

    private void OnMaxChanged(object sender, RoutedPropertyChangedEventArgs<double> e) => UpdateMaxLabel();

    private void OnOpacityChanged(object sender, RoutedPropertyChangedEventArgs<double> e) => UpdateOpacityLabel();

    private void UpdateMaxLabel()
    {
        if (MaxValueText is null) return;
        MaxValueText.Text = ((int)Math.Round(MaxSlider.Value)).ToString();
    }

    private void UpdateOpacityLabel()
    {
        if (OpacityValueText is null) return;
        OpacityValueText.Text = $"{(int)Math.Round(OpacitySlider.Value * 100)}%";
    }

    // --------------------------------------------------------------- save

    private void OnSaveClick(object sender, RoutedEventArgs e)
    {
        var candidate = _settings.Current.Clone();
        candidate.Hotkey = _gesture.Display;
        candidate.AutoStart = AutoStartCheck.IsChecked == true;
        candidate.SaveFolder = string.IsNullOrWhiteSpace(SaveFolderBox.Text)
            ? AppSettings.DefaultSaveFolder
            : SaveFolderBox.Text.Trim();
        candidate.MaxSnapshots = (int)Math.Round(MaxSlider.Value);
        candidate.SnapshotOpacity = Math.Round(OpacitySlider.Value, 2);
        candidate.Notifications = NotifyCheck.IsChecked == true;
        candidate.PinNewSnapshots = PinCheck.IsChecked == true;
        candidate.Language = LanguageCombo.SelectedIndex >= 0
            ? LocalizationService.Supported[LanguageCombo.SelectedIndex].Code
            : _originalLanguage;

        if (!_app.ApplySettings(candidate)) return;

        // This window is opened with Show(), not ShowDialog(), so it must not rely on
        // DialogResult (WPF throws "DialogResult can be set only after Window is created
        // and shown as dialog" - which made both buttons look dead).
        _applied = true;
        Close();
    }

    private void OnCancelClick(object sender, RoutedEventArgs e) => Close();

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);

        // Esc closes the window (the hotkey box handles Esc itself to revert an edit).
        if (e.Key == Key.Escape)
        {
            e.Handled = true;
            Close();
        }
    }

    protected override void OnClosed(EventArgs e)
    {
        base.OnClosed(e);

        // Cancelling must undo the live language preview.
        if (!_applied)
        {
            LocalizationService.Instance.SetLanguage(_originalLanguage);
        }
    }
}
