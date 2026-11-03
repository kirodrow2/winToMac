using System.IO;
using WinToMac.Settings;

namespace WinToMac.Services;

/// <summary>
/// Короткий журнал запусков и выходов в %APPDATA%\winToMac\startup.log — чтобы понять,
/// почему программа не поднялась после перезагрузки или не встала на место при обновлении.
/// Хранятся только последние строки, никаких личных данных.
/// </summary>
public static class StartupLog
{
    private const int MaxLines = 300;
    private static readonly object Sync = new();

    public static string FilePath => Path.Combine(AppSettings.Folder, "startup.log");

    public static void Write(string message)
    {
        try
        {
            lock (Sync)
            {
                Directory.CreateDirectory(AppSettings.Folder);
                var lines = File.Exists(FilePath) ? File.ReadAllLines(FilePath).ToList() : new List<string>();
                lines.Add($"{DateTime.Now:yyyy-MM-dd HH:mm:ss} [{Environment.ProcessId}] {message}");
                if (lines.Count > MaxLines)
                {
                    lines.RemoveRange(0, lines.Count - MaxLines);
                }

                File.WriteAllLines(FilePath, lines);
            }
        }
        catch
        {
            // Журнал не должен мешать работе
        }
    }
}
