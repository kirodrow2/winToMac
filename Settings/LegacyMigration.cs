using System.IO;
using Microsoft.Win32;

namespace WinToMac.Settings;

/// <summary>
/// Переезд со старого названия программы (MacShell): настройки, папка обоев и автозапуск.
/// Всё делается один раз и молча — если что-то не вышло, программа просто работает с нуля.
/// </summary>
public static class LegacyMigration
{
    private const string OldName = "MacShell";
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";

    /// <summary>До загрузки настроек: скопировать старый settings.json, если нового ещё нет.</summary>
    public static void MigrateSettingsFile()
    {
        try
        {
            string oldFile = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), OldName, "settings.json");
            if (!File.Exists(AppSettings.FilePath) && File.Exists(oldFile))
            {
                Directory.CreateDirectory(AppSettings.Folder);
                File.Copy(oldFile, AppSettings.FilePath);
            }
        }
        catch
        {
            // Начнём с настроек по умолчанию
        }
    }

    /// <summary>После загрузки настроек: перенести папку обоев и запись автозапуска.</summary>
    public static void MigrateAfterLoad()
    {
        MigrateWallpaperFolder();
        MigrateAutostart();
    }

    private static void MigrateWallpaperFolder()
    {
        string oldFolder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyVideos), OldName + " Wallpapers");
        string newFolder = AppSettings.WallpaperFolder;
        if (!Directory.Exists(oldFolder) || Directory.Exists(newFolder))
        {
            return;
        }

        try
        {
            Directory.Move(oldFolder, newFolder);
        }
        catch
        {
            // Файл занят — останется в старой папке, пути в настройках не трогаем
            return;
        }

        var settings = AppSettings.Current;
        settings.WallpaperSource = Rebase(settings.WallpaperSource, oldFolder, newFolder);
        settings.CustomWallpapers = settings.CustomWallpapers.Select(path => Rebase(path, oldFolder, newFolder)!).ToList();
        AppSettings.Save();
    }

    /// <summary>Если автозапуск был включён под старым именем — переписываем на эту программу.</summary>
    private static void MigrateAutostart()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: true);
            if (key?.GetValue(OldName) == null)
            {
                return;
            }

            key.DeleteValue(OldName, throwOnMissingValue: false);
            if (Environment.ProcessPath is { } exePath)
            {
                key.SetValue(AppSettings.ProductName, $"\"{exePath}\"");
            }
        }
        catch
        {
            // Нет доступа к реестру — автозапуск можно включить в настройках
        }
    }

    private static string? Rebase(string? path, string oldFolder, string newFolder) =>
        path != null && path.StartsWith(oldFolder, StringComparison.OrdinalIgnoreCase)
            ? newFolder + path[oldFolder.Length..]
            : path;
}
