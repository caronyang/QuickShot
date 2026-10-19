using System.Diagnostics;
using System.IO;
using System.Text;

namespace QuickShot.Services;

/// <summary>Minimal file logger: %AppData%\QuickShot\quickshot.log.</summary>
internal static class Log
{
    private static readonly object Gate = new();

    public static string LogFile => Path.Combine(SettingsService.ConfigDirectory, "quickshot.log");

    public static void Info(string message) => Write("INFO ", message);

    public static void Warn(string message) => Write("WARN ", message);

    public static void Error(string message) => Write("ERROR", message);

    public static void Error(string message, Exception ex) => Write("ERROR", $"{message}{Environment.NewLine}{ex}");

    [Conditional("DEBUG")]
    public static void Debug(string message) => Write("DEBUG", message);

    private static void Write(string level, string message)
    {
        try
        {
            lock (Gate)
            {
                Directory.CreateDirectory(SettingsService.ConfigDirectory);
                var line = new StringBuilder()
                    .Append(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff"))
                    .Append(" [").Append(level).Append("] ")
                    .Append(message)
                    .AppendLine()
                    .ToString();
                File.AppendAllText(LogFile, line, Encoding.UTF8);
            }
        }
        catch
        {
            // Logging must never take the app down.
        }
    }
}
