namespace QuickShot.Models;

/// <summary>User settings, persisted as JSON in %AppData%\QuickShot\settings.json.</summary>
public sealed class AppSettings
{
    public const string DefaultHotkey = "Win+Shift+Q";

    /// <summary>Start QuickShot with Windows (on by default, per spec).</summary>
    public bool AutoStart { get; set; } = true;

    /// <summary>"en" (default) or "zh-TW".</summary>
    public string Language { get; set; } = "en";

    /// <summary>Global capture hotkey, e.g. "Win+Shift+Q".</summary>
    public string Hotkey { get; set; } = DefaultHotkey;

    public string SaveFolder { get; set; } = DefaultSaveFolder;

    /// <summary>Maximum number of simultaneously floating snapshots.</summary>
    public int MaxSnapshots { get; set; } = 20;

    /// <summary>Opacity of the floating snapshots (0.3 - 1.0).</summary>
    public double SnapshotOpacity { get; set; } = 0.96;

    /// <summary>New snapshots float above every other window.</summary>
    public bool PinNewSnapshots { get; set; } = true;

    public bool Notifications { get; set; } = true;

    public static string DefaultSaveFolder =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyPictures), "QuickShot");

    public AppSettings Clone() => (AppSettings)MemberwiseClone();

    public void Normalize()
    {
        if (MaxSnapshots < 1) MaxSnapshots = 1;
        if (MaxSnapshots > 100) MaxSnapshots = 100;
        SnapshotOpacity = Math.Clamp(SnapshotOpacity, 0.3, 1.0);
        if (string.IsNullOrWhiteSpace(SaveFolder)) SaveFolder = DefaultSaveFolder;
        if (string.IsNullOrWhiteSpace(Hotkey)) Hotkey = DefaultHotkey;
    }
}
