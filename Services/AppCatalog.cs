using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using WinToMac.Native;
using WinToMac.Settings;
using WinToMac.Localization;

namespace WinToMac.Services;

/// <summary>Приложение из общего списка (то же, что видно в «Все приложения» меню Пуск).</summary>
public sealed class AppEntry
{
    public string Name { get; init; } = "";

    /// <summary>Идентификатор внутри shell:AppsFolder (AUMID или путь с GUID известной папки).</summary>
    public string ParsingName { get; init; } = "";

    public string? AppId { get; init; }
    public string? ExeName { get; init; }

    public string LaunchTarget => "shell:AppsFolder\\" + ParsingName;

    public PinnedApp ToPinned() => new()
    {
        Name = Name,
        LaunchTarget = LaunchTarget,
        AppId = AppId,
        ExeName = ExeName,
        IconSource = LaunchTarget,
    };
}

public static partial class AppCatalog
{
    private static List<AppEntry>? _cache;

    [GeneratedRegex(@"^\{([0-9A-Fa-f\-]{36})\}\\(.+)$")]
    private static partial Regex KnownFolderPathRegex();

    // …\WindowsApps\<Имя>_<Версия>_<Архитектура>_<Ресурс>_<Издатель>\… (в имени пакета «_» не бывает)
    [GeneratedRegex(@"\\WindowsApps\\([^\\_]+)_[^\\_]+_[^\\_]+_[^\\_]*_([^\\_]+)\\", RegexOptions.IgnoreCase)]
    private static partial Regex PackageFolderRegex();

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern int SHGetKnownFolderPath(
        [MarshalAs(UnmanagedType.LPStruct)] Guid folderId, uint flags, IntPtr token, out string path);

    /// <summary>Список всех приложений. Читается в отдельном STA-потоке, чтобы не подвешивать интерфейс.</summary>
    public static Task<List<AppEntry>> GetAllAsync(bool refresh = false)
    {
        if (_cache != null && !refresh)
        {
            return Task.FromResult(_cache);
        }

        var completion = new TaskCompletionSource<List<AppEntry>>();
        var thread = new Thread(() =>
        {
            try
            {
                _cache = Enumerate();
                completion.SetResult(_cache);
            }
            catch (Exception exception)
            {
                completion.SetException(exception);
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.IsBackground = true;
        thread.Start();
        return completion.Task;
    }

    /// <summary>
    /// Приложения из Microsoft Store лежат в папке с номером версии
    /// (…\WindowsApps\MSTeams_26260.1717…_x64__8wekyb3d8bbwe\ms-teams.exe) — после автообновления
    /// такой путь пропадает вместе со значком. Закрепления по такому пути переводим на AUMID,
    /// который от версии не зависит. true — что-то поправили.
    /// </summary>
    public static async Task<bool> FixPackagedPinsAsync(IReadOnlyList<PinnedApp> pinnedApps)
    {
        var versioned = pinnedApps
            .Select(app => (App: app, Family: GetPackageFamily(app.LaunchTarget)))
            .Where(pair => pair.Family != null)
            .ToList();
        if (versioned.Count == 0)
        {
            return false;
        }

        var apps = await GetAllAsync(refresh: true);
        bool changed = false;
        foreach (var (app, family) in versioned)
        {
            var candidates = apps
                .Where(entry => entry.AppId?.StartsWith(family + "!", StringComparison.OrdinalIgnoreCase) == true)
                .ToList();
            var match = candidates.FirstOrDefault(entry => entry.Name.Equals(app.Name, StringComparison.CurrentCultureIgnoreCase))
                ?? candidates.FirstOrDefault();
            if (match == null)
            {
                continue;
            }

            app.LaunchTarget = match.LaunchTarget;
            app.IconSource = match.LaunchTarget;
            app.AppId = match.AppId;
            changed = true;
        }

        return changed;
    }

    /// <summary>"…\WindowsApps\MSTeams_26260…_x64__8wekyb3d8bbwe\ms-teams.exe" → "MSTeams_8wekyb3d8bbwe".</summary>
    private static string? GetPackageFamily(string path)
    {
        var match = PackageFolderRegex().Match(path);
        return match.Success ? match.Groups[1].Value + "_" + match.Groups[2].Value : null;
    }

    public static void Launch(string target)
    {
        try
        {
            if (target.StartsWith("shell:", StringComparison.OrdinalIgnoreCase))
            {
                Process.Start(new ProcessStartInfo("explorer.exe", target) { UseShellExecute = true });
            }
            else
            {
                Process.Start(new ProcessStartInfo(target) { UseShellExecute = true });
            }
        }
        catch
        {
            // Приложение удалено или запуск отменён (например, UAC) — молча игнорируем
        }
    }

    private static List<AppEntry> Enumerate()
    {
        var result = new List<AppEntry>();
        Type? shellType = Type.GetTypeFromProgID("Shell.Application");
        if (shellType == null)
        {
            return result;
        }

        dynamic shell = Activator.CreateInstance(shellType)!;
        dynamic folder = shell.NameSpace("shell:AppsFolder");
        foreach (dynamic item in folder.Items())
        {
            string name = item.Name;
            string parsingName = item.Path;
            if (string.IsNullOrWhiteSpace(name) || IsJunk(name, parsingName))
            {
                continue;
            }

            string? targetPath = ResolveKnownFolderPath(parsingName);
            if (targetPath == null)
            {
                try
                {
                    targetPath = item.ExtendedProperty("System.Link.TargetParsingPath") as string;
                }
                catch
                {
                    targetPath = null;
                }
            }

            bool looksLikePath = parsingName.Contains('\\') || parsingName.Contains(':');
            string? exeName = targetPath != null && targetPath.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)
                ? Path.GetFileName(targetPath).ToLowerInvariant()
                : null;

            result.Add(new AppEntry
            {
                Name = name,
                ParsingName = parsingName,
                AppId = looksLikePath ? null : parsingName,
                ExeName = exeName,
            });
        }

        Marshal.ReleaseComObject(folder);
        Marshal.ReleaseComObject(shell);

        return result
            .GroupBy(entry => entry.ParsingName, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .OrderBy(entry => entry.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
    }

    /// <summary>Деинсталляторы, справка и ссылки на сайты в списке приложений не нужны.</summary>
    private static bool IsJunk(string name, string parsingName)
    {
        string lowerName = name.ToLowerInvariant();
        if (lowerName.Contains("uninstall") || lowerName.StartsWith("удал") || lowerName.Contains("readme"))
        {
            return true;
        }

        string extension = Path.GetExtension(parsingName).ToLowerInvariant();
        return extension is ".url" or ".txt" or ".chm" or ".pdf" or ".htm" or ".html" or ".ini";
    }

    /// <summary>"{GUID}\notepad.exe" → "C:\Windows\System32\notepad.exe".</summary>
    private static string? ResolveKnownFolderPath(string parsingName)
    {
        var match = KnownFolderPathRegex().Match(parsingName);
        if (!match.Success)
        {
            return File.Exists(parsingName) ? parsingName : null;
        }

        if (SHGetKnownFolderPath(Guid.Parse(match.Groups[1].Value), 0, IntPtr.Zero, out string folder) != 0)
        {
            return null;
        }

        return Path.Combine(folder, match.Groups[2].Value);
    }
}
