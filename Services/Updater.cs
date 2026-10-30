using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Reflection;
using System.Text.Json;
using WinToMac.Settings;

namespace WinToMac.Services;

/// <summary>Новая версия на GitHub: номер, что нового и ссылка на архив.</summary>
public sealed record UpdateInfo(Version Version, string Notes, string DownloadUrl, long Size);

/// <summary>
/// Обновление из GitHub Releases без ручного скачивания: архив распаковывается рядом с программой
/// как winToMac.update.exe и запускается — дальше SelfUpdate сам закрывает старую копию и встаёт на её место.
/// </summary>
public static class Updater
{
    private const string ArchiveName = "winToMac.zip";

    private static readonly HttpClient Http = CreateClient();

    public static Version CurrentVersion =>
        Assembly.GetEntryAssembly()?.GetName().Version is { } version
            ? new Version(version.Major, version.Minor, Math.Max(version.Build, 0))
            : new Version(0, 0, 0);

    /// <summary>Последний выпуск, если он новее установленной версии; иначе null.</summary>
    public static async Task<UpdateInfo?> CheckAsync(CancellationToken cancellation = default)
    {
        string url = $"https://api.github.com/repos/{ProjectLinks.Repository}/releases/latest";
        using var response = await Http.GetAsync(url, cancellation);
        response.EnsureSuccessStatusCode();
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellation));
        var root = document.RootElement;

        string tag = root.GetProperty("tag_name").GetString() ?? "";
        if (!Version.TryParse(tag.TrimStart('v', 'V'), out var latest) || latest <= CurrentVersion)
        {
            return null;
        }

        foreach (var asset in root.GetProperty("assets").EnumerateArray())
        {
            if (asset.GetProperty("name").GetString() == ArchiveName)
            {
                string notes = root.TryGetProperty("body", out var body) ? body.GetString() ?? "" : "";
                return new UpdateInfo(
                    latest,
                    notes.Trim(),
                    asset.GetProperty("browser_download_url").GetString()!,
                    asset.GetProperty("size").GetInt64());
            }
        }

        // Выпуск есть, но архив к нему ещё не загружен — проверим позже
        return null;
    }

    /// <summary>Скачать, распаковать рядом с программой и запустить новую версию. progress — от 0 до 1.</summary>
    public static async Task InstallAsync(UpdateInfo update, IProgress<double> progress, CancellationToken cancellation = default)
    {
        string exe = Environment.ProcessPath ?? throw new InvalidOperationException("Environment.ProcessPath");
        string folder = Path.GetDirectoryName(exe)!;
        string archive = Path.Combine(Path.GetTempPath(), $"winToMac-{update.Version}.zip");

        using (var response = await Http.GetAsync(update.DownloadUrl, HttpCompletionOption.ResponseHeadersRead, cancellation))
        {
            response.EnsureSuccessStatusCode();
            long total = response.Content.Headers.ContentLength ?? update.Size;
            await using var source = await response.Content.ReadAsStreamAsync(cancellation);
            await using var target = File.Create(archive);
            var buffer = new byte[81920];
            long done = 0;
            int read;
            while ((read = await source.ReadAsync(buffer, cancellation)) > 0)
            {
                await target.WriteAsync(buffer.AsMemory(0, read), cancellation);
                done += read;
                progress.Report(total > 0 ? (double)done / total : 0);
            }
        }

        string updateExe = Path.Combine(folder, "winToMac.update.exe");
        await Task.Run(() => Extract(archive, folder, updateExe), cancellation);
        TryDelete(archive);
        Process.Start(new ProcessStartInfo(updateExe) { UseShellExecute = true });
    }

    /// <summary>
    /// Работающий winToMac.exe занят, поэтому новый exe кладём под другим именем,
    /// а инструкцию и лицензии просто заменяем.
    /// </summary>
    private static void Extract(string archive, string folder, string updateExe)
    {
        using var zip = ZipFile.OpenRead(archive);
        foreach (var entry in zip.Entries)
        {
            if (entry.FullName.EndsWith('/'))
            {
                continue;
            }

            // В архиве всё лежит в папке winToMac/
            string relative = entry.FullName.Contains('/') ? entry.FullName[(entry.FullName.IndexOf('/') + 1)..] : entry.FullName;
            string target = relative.Equals(SelfUpdate.MainExeName, StringComparison.OrdinalIgnoreCase)
                ? updateExe
                : Path.GetFullPath(Path.Combine(folder, relative));
            if (!target.StartsWith(Path.GetFullPath(folder), StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                entry.ExtractToFile(target, overwrite: true);
            }
            catch (IOException) when (target != updateExe)
            {
                // Файл инструкции открыт в браузере — оставим старый
            }
        }
    }

    private static void TryDelete(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch
        {
            // Временный файл уберёт Windows
        }
    }

    private static HttpClient CreateClient()
    {
        var client = new HttpClient { Timeout = TimeSpan.FromMinutes(10) };
        // GitHub API требует User-Agent
        client.DefaultRequestHeaders.UserAgent.ParseAdd($"winToMac/{CurrentVersion}");
        return client;
    }
}
