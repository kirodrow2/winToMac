using System.Windows;
using System.Windows.Threading;
using WinToMac.Native;
using WinToMac.Services;
using WinToMac.Settings;
using WinToMac.UI;
using WinToMac.Localization;

namespace WinToMac;

public partial class App : Application
{
    private Mutex? _singleInstance;
    private TopBarWindow? _topBar;
    private DockWindow? _dock;
    private LauncherWindow? _launcher;
    private SettingsWindow? _settingsWindow;
    private WallpaperWindow? _wallpaper;
    private WallpaperGalleryWindow? _wallpaperGallery;
    private readonly DispatcherTimer _fullScreenTimer = new() { Interval = TimeSpan.FromMilliseconds(500) };
    private bool _barsHidden;
    private bool _isQuitting;

    public static new App Current => (App)Application.Current;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Служебный запуск с правами администратора: только переназначить Caps Lock в реестре и выйти
        if (e.Args.Contains(ScancodeMap.CapsToF13Argument) || e.Args.Contains(ScancodeMap.CapsRestoreArgument))
        {
            int exitCode = 0;
            try
            {
                ScancodeMap.SetCapsLock(e.Args.Contains(ScancodeMap.CapsToF13Argument) ? ScancodeMap.F13ScanCode : null);
            }
            catch
            {
                exitCode = 1;
            }

            Shutdown(exitCode);
            return;
        }

        // Новая версия запущена поверх старой — старая уступает место
        _singleInstance = new Mutex(true, "WinToMac.SingleInstance", out bool isFirst);
        if (!isFirst)
        {
            SelfUpdate.StopOtherInstances(_singleInstance);
        }

        // Запустили winToMac.update.exe или другую копию — встаём на место winToMac.exe
        if (SelfUpdate.RelaunchFromMainExe())
        {
            try
            {
                _singleInstance.ReleaseMutex();
            }
            catch (ApplicationException)
            {
                // Мьютекс так и не стал нашим — новая копия дождётся выхода этой
            }

            Shutdown();
            return;
        }

        SelfUpdate.CleanUpInBackground();
        SelfUpdate.ListenForQuitRequest(() => Dispatcher.BeginInvoke(Quit));

        // Что бы ни случилось — панель задач и рабочая область Windows должны вернуться
        AppDomain.CurrentDomain.ProcessExit += (_, _) => RestoreSystem();
        AppDomain.CurrentDomain.UnhandledException += (_, _) => RestoreSystem();
        DispatcherUnhandledException += (_, args) =>
        {
            args.Handled = true;
            RestoreSystem();

            // Ошибка во время выхода — просто завершаемся, без окна и без повторного выхода
            if (_isQuitting)
            {
                Environment.Exit(0);
                return;
            }

            MessageBox.Show(args.Exception.ToString(), L.T("winToMac: ошибка"), MessageBoxButton.OK, MessageBoxImage.Error);
            Quit();
        };

        LegacyMigration.MigrateSettingsFile();
        AppSettings.Load();
        LegacyMigration.MigrateAfterLoad();
        L.Load(AppSettings.Current.UiLanguage);
        Theme.Apply();
        ApplyTaskbarVisibility();
        TrayHost.Start();
        KeyRemapper.Start();
        AppSettings.Changed += () =>
        {
            Theme.Apply();
            ApplyTaskbarVisibility();
            ApplyWallpaper();
        };
        ApplyWallpaper();

        _topBar = new TopBarWindow();
        _topBar.Show();
        _dock = new DockWindow();
        _dock.Show();

        _fullScreenTimer.Tick += (_, _) => CheckFullScreen();
        _fullScreenTimer.Start();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        RestoreSystem();
        base.OnExit(e);
    }

    public void ShowLauncher()
    {
        _launcher ??= new LauncherWindow();
        _launcher.Open();
    }

    /// <summary>
    /// После смены языка: окна с текстом пересоздаются на том же месте. Док и строка меню
    /// перерисовываются сами по AppSettings.Changed.
    /// </summary>
    public void ReopenWindowsForLanguage()
    {
        if (_launcher != null)
        {
            _launcher.Close();
            _launcher = null;
        }

        if (_settingsWindow is { IsLoaded: true } settings)
        {
            double left = settings.Left, top = settings.Top;
            settings.Close();
            ShowSettings();
            _settingsWindow!.Left = left;
            _settingsWindow.Top = top;
        }

        if (_wallpaperGallery is { IsLoaded: true } gallery)
        {
            double left = gallery.Left, top = gallery.Top;
            gallery.Close();
            ShowWallpapers();
            _wallpaperGallery!.Left = left;
            _wallpaperGallery.Top = top;
        }

        AboutWindow.Reopen();
    }

    public void ShowSettings()
    {
        if (_settingsWindow is { IsLoaded: true })
        {
            _settingsWindow.Activate();
            return;
        }

        _settingsWindow = new SettingsWindow();
        _settingsWindow.Show();
        _settingsWindow.Activate();
    }

    public void ShowWallpapers()
    {
        if (_wallpaperGallery is { IsLoaded: true })
        {
            _wallpaperGallery.Activate();
            return;
        }

        _wallpaperGallery = new WallpaperGalleryWindow();
        _wallpaperGallery.Show();
        _wallpaperGallery.Activate();
    }

    /// <summary>Что сейчас с живыми обоями — показывается внизу галереи.</summary>
    public string WallpaperStatus { get; private set; } = L.T("Живые обои выключены");

    public event Action? WallpaperStatusChanged;

    private void SetWallpaperStatus(string status)
    {
        WallpaperStatus = status;
        WallpaperStatusChanged?.Invoke();
    }

    /// <summary>Включить, сменить или убрать живые обои по настройкам.</summary>
    private void ApplyWallpaper()
    {
        var settings = AppSettings.Current;
        if (!settings.WallpaperEnabled || string.IsNullOrEmpty(settings.WallpaperSource))
        {
            if (_wallpaper != null)
            {
                _wallpaper.CloseIntentionally();
                _wallpaper = null;
                DesktopWallpaperHost.RefreshSystemWallpaper();
            }

            SetWallpaperStatus(L.T("Живые обои выключены"));
            return;
        }

        if (_wallpaper == null)
        {
            var window = new WallpaperWindow();
            window.Destroyed += () =>
            {
                // Проводник перезапустился и унёс окно с собой — встраиваемся заново, когда он поднимется
                _wallpaper = null;
                var retry = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };
                retry.Tick += (_, _) =>
                {
                    retry.Stop();
                    ApplyWallpaper();
                };
                retry.Start();
            };

            window.StatusChanged += status => SetWallpaperStatus(
                char.ToUpper(status[0]) + status[1..] + "  ·  " + DesktopWallpaperHost.LastStatus);

            if (!window.Attach())
            {
                window.CloseIntentionally();
                SetWallpaperStatus(L.T("Не удалось встроить обои под значки: ") + DesktopWallpaperHost.LastStatus);
                return;
            }

            _wallpaper = window;
        }

        if (_wallpaper.CurrentSource != settings.WallpaperSource)
        {
            SetWallpaperStatus(L.T("Загружаю…  ·  ") + DesktopWallpaperHost.LastStatus);
        }

        _wallpaper.SetSource(settings.WallpaperSource, settings.WallpaperMuted);
    }

    /// <summary>Свернуть все окна / вернуть их обратно (как Win+D).</summary>
    public void ShowDesktop()
    {
        try
        {
            Type? shellType = Type.GetTypeFromProgID("Shell.Application");
            if (shellType != null)
            {
                dynamic shell = Activator.CreateInstance(shellType)!;
                shell.ToggleDesktop();
                return;
            }
        }
        catch
        {
            // Ниже — запасной вариант без проводника
        }

        foreach (var window in WindowEnumerator.GetTaskbarWindows(withDetails: false))
        {
            WindowEnumerator.Minimize(window.Handle);
        }
    }

    public void Quit()
    {
        if (_isQuitting)
        {
            return;
        }

        _isQuitting = true;
        try
        {
            _fullScreenTimer.Stop();
            _wallpaper?.CloseIntentionally();
            _wallpaper = null;
            _topBar?.Close();
            _dock?.Close();
            RestoreSystem();
            Shutdown();
        }
        catch
        {
            // Панель задач и рабочая область уже возвращены — остальное при выходе не важно
            Environment.Exit(0);
        }
    }

    /// <summary>
    /// Панели прячутся, только когда активное окно занимает весь экран без заголовка:
    /// игры, видео на весь экран, презентации. Обычные развёрнутые окна их не скрывают.
    /// </summary>
    private void CheckFullScreen()
    {
        IntPtr foreground = NativeMethods.GetForegroundWindow();
        NativeMethods.GetWindowThreadProcessId(foreground, out uint processId);
        if (foreground == IntPtr.Zero || processId == Environment.ProcessId)
        {
            return;
        }

        bool fullScreen = NativeMethods.IsFullScreen(foreground);

        // Обои не видны за развёрнутым окном или игрой — незачем тратить на них видеокарту
        bool covered = fullScreen
            || AppSettings.Current.WallpaperPauseWhenCovered && NativeMethods.IsZoomed(foreground) && IsOnPrimaryScreen(foreground);
        _wallpaper?.SetPaused(covered);

        if (fullScreen == _barsHidden)
        {
            return;
        }

        _barsHidden = fullScreen;
        var visibility = fullScreen ? Visibility.Hidden : Visibility.Visible;
        if (_topBar != null)
        {
            _topBar.Visibility = visibility;
        }

        if (_dock != null)
        {
            _dock.Visibility = visibility;
        }
    }

    private static bool IsOnPrimaryScreen(IntPtr hwnd)
    {
        if (!NativeMethods.GetWindowRect(hwnd, out var rect))
        {
            return false;
        }

        var (width, height) = NativeMethods.GetPrimaryScreenSize();
        int centerX = rect.Left + rect.Width / 2;
        int centerY = rect.Top + rect.Height / 2;
        return centerX >= 0 && centerX < width && centerY >= 0 && centerY < height;
    }

    private static void RestoreSystem()
    {
        KeyRemapper.Stop();
        TrayHost.Stop();
        try
        {
            WorkArea.Restore();
        }
        catch
        {
            // Вызов может прийти не из UI-потока (ProcessExit) — рабочую область всё равно вернёт Explorer
        }

        try
        {
            DesktopWallpaperHost.RefreshSystemWallpaper();
        }
        catch
        {
            // Не критично: Windows перерисует обои сама при следующем изменении
        }

        try
        {
            TaskbarHider.Restore();
        }
        catch
        {
            // См. выше
        }
    }

    private static void ApplyTaskbarVisibility()
    {
        if (AppSettings.Current.HideWindowsTaskbar)
        {
            TaskbarHider.Hide();
        }
        else if (TaskbarHider.IsHidden)
        {
            TaskbarHider.Restore();
        }
    }
}
