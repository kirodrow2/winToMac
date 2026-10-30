using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using WinToMac.Settings;
using WinToMac.Localization;

namespace WinToMac.Services;

public sealed class AerialVideo
{
    public string Name { get; init; } = "";
    public string Url { get; init; } = "";
    public bool IsHevc { get; init; }

    public string FileName => "Aerial - " + string.Concat(Name.Split(Path.GetInvalidFileNameChars())) + Path.GetExtension(new Uri(Url).LocalPath);

    public string LocalPath => Path.Combine(AppSettings.WallpaperFolder, FileName);

    public bool IsDownloaded => File.Exists(LocalPath);
}

/// <summary>
/// Заставки Apple Aerial (Apple TV и macOS) — список и загрузка с серверов Apple, как это делает
/// проект Aerial. Видео не входят в winToMac: каждое скачивается по кнопке в папку обоев.
/// </summary>
public static class AerialCatalog
{
    /// <summary>Источники списка по порядку: архив ресурсов tvOS 13 (там есть версии H.264) и старый JSON.</summary>
    private static readonly string[] ManifestUrls =
    {
        "https://sylvan.apple.com/Aerials/resources-13.tar",
        "http://sylvan.apple.com/Aerials/resources-13.tar",
        "http://a1.phobos.apple.com/us/r1000/000/Features/atv/AutumnResources/videos/entries.json",
    };

    /// <summary>Что скачивается по кнопке «Популярные».</summary>
    private static readonly string[] PopularNames =
    {
        "Dubai", "Hong Kong", "Greenland", "Hawaii", "San Francisco", "New York", "London", "Los Angeles", "Liwa", "China",
    };

    private static readonly HttpClient Http = CreateClient();

    public static async Task<List<AerialVideo>> LoadAsync()
    {
        var errors = new List<string>();
        foreach (string url in ManifestUrls)
        {
            try
            {
                using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
                byte[] data = await Http.GetByteArrayAsync(url, timeout.Token);
                string json = url.EndsWith(".tar") ? ExtractFromTar(data, "entries.json") : Encoding.UTF8.GetString(data);
                var videos = Parse(json);
                if (videos.Count > 0)
                {
                    return videos;
                }
            }
            catch (Exception exception)
            {
                errors.Add(url + ": " + exception.Message);
            }
        }

        throw new InvalidOperationException(L.T("Не удалось получить список заставок Apple.\n") + string.Join("\n", errors));
    }

    public static IEnumerable<AerialVideo> PickPopular(IEnumerable<AerialVideo> videos, int count) => videos
        .Where(video => !video.IsHevc)
        .Where(video => PopularNames.Any(name => video.Name.Contains(name, StringComparison.OrdinalIgnoreCase)))
        .GroupBy(video => PopularNames.First(name => video.Name.Contains(name, StringComparison.OrdinalIgnoreCase)))
        .Select(group => group.First())
        .Take(count);

    public static async Task DownloadAsync(AerialVideo video, IProgress<double> progress, CancellationToken cancellation)
    {
        Directory.CreateDirectory(AppSettings.WallpaperFolder);
        string partPath = video.LocalPath + ".part";
        try
        {
            await DownloadFrom(video.Url, partPath, progress, cancellation);
        }
        catch (HttpRequestException) when (video.Url.StartsWith("https://"))
        {
            // У серверов Apple бывают проблемы с цепочкой сертификатов — повторяем по http
            await DownloadFrom("http://" + video.Url["https://".Length..], partPath, progress, cancellation);
        }

        File.Move(partPath, video.LocalPath, overwrite: true);
    }

    private static async Task DownloadFrom(string url, string path, IProgress<double> progress, CancellationToken cancellation)
    {
        using var response = await Http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellation);
        response.EnsureSuccessStatusCode();
        long total = response.Content.Headers.ContentLength ?? 0;
        await using var source = await response.Content.ReadAsStreamAsync(cancellation);
        await using var target = File.Create(path);
        var buffer = new byte[1 << 16];
        long received = 0;
        int read;
        while ((read = await source.ReadAsync(buffer, cancellation)) > 0)
        {
            await target.WriteAsync(buffer.AsMemory(0, read), cancellation);
            received += read;
            if (total > 0)
            {
                progress.Report((double)received / total);
            }
        }
    }

    /// <summary>
    /// Разные версии списка устроены по-разному, поэтому ищем любые объекты с названием и ссылкой на видео.
    /// Предпочитаем 1080p H.264 — его Windows играет без дополнительных кодеков.
    /// </summary>
    private static List<AerialVideo> Parse(string json)
    {
        var result = new Dictionary<string, AerialVideo>();
        using var document = JsonDocument.Parse(json);
        Walk(document.RootElement, result);
        return result.Values
            .OrderBy(video => video.IsHevc)
            .ThenBy(video => video.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
    }

    private static void Walk(JsonElement element, Dictionary<string, AerialVideo> result)
    {
        if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in element.EnumerateArray())
            {
                Walk(item, result);
            }

            return;
        }

        if (element.ValueKind != JsonValueKind.Object)
        {
            return;
        }

        if (element.TryGetProperty("accessibilityLabel", out var label) && label.ValueKind == JsonValueKind.String)
        {
            string? url = GetString(element, "url-1080-H264") ?? GetString(element, "url");
            bool isHevc = false;
            if (url == null)
            {
                url = GetString(element, "url-1080-SDR");
                isHevc = url != null;
            }

            if (url != null && !result.ContainsKey(url))
            {
                string name = label.GetString()!;
                // Одинаковые названия (несколько роликов про один город) различаем номером
                int duplicates = result.Values.Count(video => video.Name == name || video.Name.StartsWith(name + " "));
                result[url] = new AerialVideo
                {
                    Name = duplicates == 0 ? name : $"{name} {duplicates + 1}",
                    Url = url,
                    IsHevc = isHevc,
                };
            }
        }

        foreach (var property in element.EnumerateObject())
        {
            Walk(property.Value, result);
        }
    }

    private static string? GetString(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;

    /// <summary>Достать файл из tar: заголовки по 512 байт, размер — восьмеричное число.</summary>
    private static string ExtractFromTar(byte[] tar, string fileName)
    {
        int offset = 0;
        while (offset + 512 <= tar.Length)
        {
            string name = Encoding.ASCII.GetString(tar, offset, 100).TrimEnd('\0');
            if (name.Length == 0)
            {
                break;
            }

            string sizeText = Encoding.ASCII.GetString(tar, offset + 124, 12).Trim('\0', ' ');
            long size = sizeText.Length == 0 ? 0 : Convert.ToInt64(sizeText, 8);
            if (name.EndsWith(fileName, StringComparison.OrdinalIgnoreCase))
            {
                return Encoding.UTF8.GetString(tar, offset + 512, (int)size);
            }

            offset += 512 + (int)((size + 511) / 512 * 512);
        }

        throw new InvalidDataException(L.T("В архиве нет ") + fileName);
    }

    private static HttpClient CreateClient()
    {
        var client = new HttpClient { Timeout = Timeout.InfiniteTimeSpan };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("winToMac/1.0");
        return client;
    }
}
