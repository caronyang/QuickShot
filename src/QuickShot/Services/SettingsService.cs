using System.Text.Json;
using System.Text.Json.Serialization;
using QuickShot.Models;

namespace QuickShot.Services;

public sealed class SettingsService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never,
    };

    private readonly string _file;

    public SettingsService(string? file = null)
    {
        _file = file ?? Path.Combine(ConfigDirectory, "settings.json");
        Current = new AppSettings();
    }

    public AppSettings Current { get; private set; }

    public event EventHandler? Changed;

    public static string ConfigDirectory => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "QuickShot");

    public void Load()
    {
        try
        {
            if (File.Exists(_file))
            {
                var json = File.ReadAllText(_file);
                var loaded = JsonSerializer.Deserialize<AppSettings>(json, JsonOptions);
                if (loaded is not null) Current = loaded;
            }
        }
        catch (Exception ex)
        {
            Log.Warn($"Could not read settings ({_file}): {ex.Message}");
        }

        Current.Normalize();
    }

    public void Save()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_file)!);
            File.WriteAllText(_file, JsonSerializer.Serialize(Current, JsonOptions));
            Changed?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex)
        {
            Log.Warn($"Could not write settings ({_file}): {ex.Message}");
        }
    }

    /// <summary>Replaces the whole settings object and persists it.</summary>
    public void Update(AppSettings settings)
    {
        settings.Normalize();
        Current = settings;
        Save();
    }
}
