using System.IO;
using System.Text.Json;
using System.Windows.Forms;
using WinToMac.Native;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;
using WinToMac.Localization;

namespace WinToMac.UI;

/// <summary>
/// Окно живых обоев: встраивается под значки рабочего стола и показывает видео, GIF или анимацию.
///
/// Сделано как в Lively Wallpaper — окно WinForms с WebView2 (движок Edge) внутри. На Windows 11 24H2+
/// окно под значками должно быть «слоистым»; WPF такой флаг сбрасывает, а его встроенный плеер
/// не выводит видео в слоистое окно. WebView2 рисует через видеокарту в собственное дочернее окно
/// и под значками виден.
/// </summary>
public sealed class WallpaperWindow : Form
{
    private const string MediaHost = "media.macshell";
    private const string PageHost = "page.macshell";

    private readonly WebView2 _web = new()
    {
        Dock = DockStyle.Fill,
        DefaultBackgroundColor = System.Drawing.Color.Black,
    };

    private readonly System.Windows.Forms.Timer _placeTimer = new() { Interval = 5000 };
    private Task? _initialization;
    private string? _source;
    private bool _paused;
    private bool _muted = true;

    public bool IsClosingIntentionally { get; private set; }

    public string? CurrentSource => _source;

    /// <summary>Состояние для галереи: что играет или почему не играет.</summary>
    public event Action<string>? StatusChanged;

    /// <summary>Окно уничтожено не нами — например, перезапустился проводник.</summary>
    public event Action? Destroyed;

    private static string DataFolder => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "winToMac");

    public WallpaperWindow()
    {
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.Manual;
        Location = new System.Drawing.Point(-32000, -32000);
        Size = new System.Drawing.Size(10, 10);
        BackColor = System.Drawing.Color.Black;
        Controls.Add(_web);
    }

    protected override bool ShowWithoutActivation => true;

    protected override CreateParams CreateParams
    {
        get
        {
            var parameters = base.CreateParams;
            parameters.ExStyle |= (int)(NativeMethods.WS_EX_TOOLWINDOW | NativeMethods.WS_EX_NOACTIVATE);
            return parameters;
        }
    }

    /// <summary>Встроить окно под значки рабочего стола и показать.</summary>
    public bool Attach()
    {
        IntPtr hwnd = Handle;
        if (!DesktopWallpaperHost.Attach(hwnd))
        {
            return false;
        }

        Show();
        DesktopWallpaperHost.Place(hwnd);

        // Проводник иногда перестраивает свои окна (смена темы, разрешения) — возвращаем обои на место
        _placeTimer.Tick += (_, _) => DesktopWallpaperHost.Place(hwnd);
        _placeTimer.Start();
        HandleDestroyed += (_, _) =>
        {
            if (!IsClosingIntentionally)
            {
                _placeTimer.Stop();
                Destroyed?.Invoke();
            }
        };

        _initialization = InitializeWebViewAsync();
        return true;
    }

    public void CloseIntentionally()
    {
        IsClosingIntentionally = true;
        _placeTimer.Stop();
        Close();
        Dispose();
    }

    public async void SetSource(string source, bool muted)
    {
        if (_initialization == null)
        {
            return;
        }

        try
        {
            await _initialization;
        }
        catch (Exception exception)
        {
            StatusChanged?.Invoke(L.T("ошибка: не запустился WebView2 (движок Edge) — ") + exception.Message);
            return;
        }

        if (source == _source)
        {
            if (muted != _muted)
            {
                _muted = muted;
                await RunScript($"setMuted({(muted ? "true" : "false")})");
            }

            return;
        }

        _source = source;
        _muted = muted;
        string html;
        if (WallpaperPresets.Find(source) is { } preset)
        {
            html = preset.CreateHtml();
            StatusChanged?.Invoke(L.T("играет анимация «{0}»", preset.Name));
        }
        else if (!File.Exists(source))
        {
            StatusChanged?.Invoke(L.T("ошибка: файл не найден — ") + source);
            return;
        }
        else
        {
            html = CreateMediaPage(source, muted);
        }

        // Страницу кладём файлом и открываем по https-адресу: так ей разрешено брать видео из папки обоев
        string pageFolder = Path.Combine(DataFolder, "WallpaperPage");
        Directory.CreateDirectory(pageFolder);
        File.WriteAllText(Path.Combine(pageFolder, "index.html"), html);
        var core = _web.CoreWebView2;
        core.ClearVirtualHostNameToFolderMapping(PageHost);
        core.SetVirtualHostNameToFolderMapping(PageHost, pageFolder, CoreWebView2HostResourceAccessKind.Allow);
        core.Navigate($"https://{PageHost}/index.html?{DateTime.Now.Ticks}");
    }

    /// <summary>Пауза, пока рабочий стол закрыт окнами или идёт игра, — чтобы не грузить видеокарту.</summary>
    public async void SetPaused(bool paused)
    {
        if (_paused == paused)
        {
            return;
        }

        _paused = paused;
        await RunScript($"setPaused({(paused ? "true" : "false")})");
    }

    private async Task InitializeWebViewAsync()
    {
        var options = new CoreWebView2EnvironmentOptions("--autoplay-policy=no-user-gesture-required");
        var environment = await CoreWebView2Environment.CreateAsync(null, Path.Combine(DataFolder, "WebView2"), options);
        await _web.EnsureCoreWebView2Async(environment);

        var core = _web.CoreWebView2;
        core.Settings.AreDefaultContextMenusEnabled = false;
        core.Settings.AreDevToolsEnabled = false;
        core.Settings.IsStatusBarEnabled = false;
        core.Settings.IsZoomControlEnabled = false;
        core.WebMessageReceived += (_, args) => StatusChanged?.Invoke(args.TryGetWebMessageAsString());
        // После загрузки страницы возвращаем текущее состояние паузы
        core.NavigationCompleted += async (_, _) => await RunScript($"setPaused({(_paused ? "true" : "false")})");
    }

    private string CreateMediaPage(string path, bool muted)
    {
        var core = _web.CoreWebView2;
        core.ClearVirtualHostNameToFolderMapping(MediaHost);
        core.SetVirtualHostNameToFolderMapping(MediaHost, Path.GetDirectoryName(path)!, CoreWebView2HostResourceAccessKind.Allow);
        string url = $"https://{MediaHost}/{Uri.EscapeDataString(Path.GetFileName(path))}";
        // Тексты состояния переводятся здесь и попадают в скрипт как готовые JS-строки
        string playing = JsonSerializer.Serialize(L.T("играет «{0}»", Path.GetFileNameWithoutExtension(path)));
        string failed = JsonSerializer.Serialize(L.T("ошибка: видео не воспроизводится (код {0}). Попробуйте MP4 с H.264 или поставьте «HEVC Video Extensions».", "#"));

        if (Path.GetExtension(path).Equals(".gif", StringComparison.OrdinalIgnoreCase))
        {
            return Page($"<img id=i src=\"{url}\">", $$"""
                document.getElementById('i').addEventListener('load',()=>chrome.webview.postMessage({{playing}}));
                function setPaused(p){} function setMuted(m){}
                """);
        }

        return Page($"<video id=v src=\"{url}\" autoplay loop playsinline {(muted ? "muted" : "")}></video>", $$"""
            const v=document.getElementById('v');
            v.addEventListener('playing',()=>chrome.webview.postMessage({{playing}}));
            v.addEventListener('error',()=>chrome.webview.postMessage({{failed}}.replace('#', v.error?v.error.code:'?')));
            function setPaused(p){p?v.pause():v.play();}
            function setMuted(m){v.muted=m;if(!m)v.play();}
            """);
    }

    private static string Page(string body, string script) => $$"""
        <!doctype html><html><head><meta charset="utf-8"><style>
        html,body{margin:0;width:100%;height:100%;overflow:hidden;background:#000}
        video,img{width:100%;height:100%;object-fit:cover;display:block}
        </style></head><body>{{body}}<script>{{script}}</script></body></html>
        """;

    private async Task RunScript(string script)
    {
        try
        {
            if (_web.CoreWebView2 != null)
            {
                await _web.CoreWebView2.ExecuteScriptAsync(script);
            }
        }
        catch
        {
            // Страница ещё грузится — состояние применится в NavigationCompleted
        }
    }
}
