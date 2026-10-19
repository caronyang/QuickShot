using System.Diagnostics;
using Microsoft.Win32;

namespace QuickShot.Services;

/// <summary>
/// "Start with Windows" via the per-user Run key.
/// The installer writes the same value, so both paths stay consistent.
/// </summary>
public static class StartupService
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "QuickShot";

    public static string ExecutablePath =>
        Environment.ProcessPath
        ?? Process.GetCurrentProcess().MainModule?.FileName
        ?? string.Empty;

    /// <summary>
    /// True when running straight out of a build output folder. In that case we do not
    /// touch the Run key automatically (otherwise `dotnet run` would register the
    /// debug binary to start at every logon).
    /// </summary>
    public static bool IsDevelopmentBuild =>
        ExecutablePath.Contains(@"\bin\", StringComparison.OrdinalIgnoreCase);

    public static bool IsEnabled()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: false);
            var value = key?.GetValue(ValueName) as string;
            return !string.IsNullOrWhiteSpace(value) &&
                   string.Equals(Unquote(value), Unquote(ExecutablePath), StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception ex)
        {
            Log.Warn($"Could not read the Run key: {ex.Message}");
            return false;
        }
    }

    public static bool SetEnabled(bool enabled)
    {
        try
        {
            using var key = Registry.CurrentUser.CreateSubKey(RunKeyPath, writable: true);
            if (key is null) return false;

            if (enabled)
            {
                key.SetValue(ValueName, $"\"{ExecutablePath}\"", RegistryValueKind.String);
                Log.Info($"Start with Windows enabled -> {ExecutablePath}");
            }
            else
            {
                if (key.GetValue(ValueName) is not null) key.DeleteValue(ValueName, throwOnMissingValue: false);
                Log.Info("Start with Windows disabled");
            }

            return true;
        }
        catch (Exception ex)
        {
            Log.Warn($"Could not write the Run key: {ex.Message}");
            return false;
        }
    }

    private static string Unquote(string value) => value.Trim().Trim('"');
}
