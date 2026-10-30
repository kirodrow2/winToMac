using System.Collections.Specialized;
using System.Globalization;
using System.Net.NetworkInformation;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using WinToMac.Native;
using WinToMac.Services;
using WinToMac.Settings;
using ManagedShell.WindowsTray;
using WinToMac.Localization;

namespace WinToMac.UI;

public partial class TopBarWindow : Window
{
    private readonly DispatcherTimer _fastTimer = new() { Interval = TimeSpan.FromMilliseconds(300) };
    private readonly DispatcherTimer _slowTimer = new() { Interval = TimeSpan.FromSeconds(5) };
    private readonly Dictionary<string, string> _appNames = new(StringComparer.OrdinalIgnoreCase);
    private IntPtr _hwnd;
    private AppBar? _appBar;
    private bool _isTraySubscribed;

    /// <summary>
    /// Последнее «чужое» активное окно. Панель не забирает фокус, но меню (Popup) может —
    /// поэтому действия вроде смены раскладки адресуем запомненному окну.
    /// </summary>
    private IntPtr _lastForeground;

    public TopBarWindow()
    {
        InitializeComponent();
        SourceInitialized += OnSourceInitialized;
        Closed += (_, _) => _appBar?.Dispose();
        AppSettings.Changed += ApplySettings;

        _fastTimer.Tick += (_, _) => UpdateFast();
        _slowTimer.Tick += (_, _) => UpdateSlow();
        _fastTimer.Start();
        _slowTimer.Start();
        UpdateFast();
        UpdateSlow();
    }

    private void OnSourceInitialized(object? sender, EventArgs e)
    {
        _hwnd = new WindowInteropHelper(this).Handle;
        NativeMethods.AddExtendedStyle(_hwnd, NativeMethods.WS_EX_TOOLWINDOW | NativeMethods.WS_EX_NOACTIVATE);

        _appBar = new AppBar(HwndSource.FromHwnd(_hwnd)!, AppBarEdge.Top);
        _appBar.PositionChanged += PlaceWindow;
        ApplySettings();
    }

    protected override void OnDpiChanged(DpiScale oldDpi, DpiScale newDpi)
    {
        base.OnDpiChanged(oldDpi, newDpi);
        ApplySettings();
    }

    private void ApplySettings()
    {
        var settings = AppSettings.Current;
        FontSize = settings.TopBarFontSize;
        Blur.Apply(_hwnd, settings.TopBarBlur);
        if (_hwnd != IntPtr.Zero)
        {
            _appBar?.Reserve(HeightInPixels());
            WorkArea.SetTop(HeightInPixels());
        }

        PlaceWindow();
        RebuildTray();
        UpdateFast();
        UpdateSlow();
    }

    private int HeightInPixels() =>
        (int)Math.Round(AppSettings.Current.TopBarHeight * VisualTreeHelper.GetDpi(this).DpiScaleY);

    private void PlaceWindow()
    {
        if (_hwnd == IntPtr.Zero)
        {
            return;
        }

        var (width, _) = NativeMethods.GetPrimaryScreenSize();
        NativeMethods.SetWindowPos(_hwnd, NativeMethods.HWND_TOPMOST, 0, 0, width, HeightInPixels(),
            // Без SWP_SHOWWINDOW: показывать и прятать окно (полноэкранные игры) решает только winToMac,
            // иначе пересчёт места при запуске игры снова показывает уже спрятанную панель
            NativeMethods.SWP_NOACTIVATE);
    }

    // ---------- Значки трея ----------

    private void RebuildTray()
    {
        var area = TrayHost.Area;
        if (area != null && !_isTraySubscribed && area.TrayIcons is INotifyCollectionChanged collection)
        {
            collection.CollectionChanged += (_, _) => Dispatcher.BeginInvoke(RebuildTray);
            _isTraySubscribed = true;
        }

        TrayPanel.Children.Clear();
        var settings = AppSettings.Current;
        if (!settings.ShowTrayIcons)
        {
            return;
        }

        double size = Math.Round(Math.Clamp(settings.TopBarHeight * 0.58, 14, 24));
        foreach (var icon in TrayHost.GetIcons())
        {
            if (icon.Identifier != null && settings.HiddenTrayIcons.Contains(icon.Identifier))
            {
                continue;
            }

            TrayPanel.Children.Add(CreateTrayIcon(icon, size));
        }
    }

    private FrameworkElement CreateTrayIcon(NotifyIcon icon, double size)
    {
        var image = new Image { Width = size, Height = size, VerticalAlignment = VerticalAlignment.Center };
        RenderOptions.SetBitmapScalingMode(image, BitmapScalingMode.HighQuality);
        image.SetBinding(Image.SourceProperty, new Binding(nameof(NotifyIcon.Icon)) { Source = icon });

        var item = new Border
        {
            Style = (Style)FindResource("BarItem"),
            Padding = new Thickness(5, 0, 5, 0),
            Child = image,
        };
        item.SetBinding(ToolTipProperty, new Binding(nameof(NotifyIcon.Title)) { Source = icon });

        // Клики пересылаются самой программе — она покажет своё окно или меню
        uint doubleClickTime = NativeMethods.GetDoubleClickTime();
        item.MouseEnter += (_, _) => icon.IconMouseEnter(CursorPosition());
        item.MouseLeave += (_, _) => icon.IconMouseLeave(CursorPosition());
        item.MouseMove += (_, _) => icon.IconMouseMove(CursorPosition());
        item.MouseDown += (_, e) => icon.IconMouseDown(e.ChangedButton, CursorPosition(), (int)doubleClickTime);
        item.MouseUp += (_, e) =>
        {
            e.Handled = true;
            icon.IconMouseUp(e.ChangedButton, CursorPosition(), (int)doubleClickTime);
        };
        return item;
    }

    private static uint CursorPosition()
    {
        NativeMethods.GetCursorPos(out var point);
        return (uint)((point.Y << 16) | (point.X & 0xFFFF));
    }

    // ---------- Обновление индикаторов ----------

    private void UpdateFast()
    {
        UpdateActiveApp();
        UpdateClock();
        UpdateLanguage();
        UpdateVolume();
    }

    private void UpdateSlow()
    {
        UpdateBattery();
        UpdateNetwork();
    }

    private void UpdateActiveApp()
    {
        IntPtr foreground = NativeMethods.GetForegroundWindow();
        if (foreground == IntPtr.Zero)
        {
            return;
        }

        NativeMethods.GetWindowThreadProcessId(foreground, out uint processId);
        if (processId == Environment.ProcessId)
        {
            return;
        }

        _lastForeground = foreground;
        string windowClass = NativeMethods.GetWindowClass(foreground);
        if (windowClass is "Progman" or "WorkerW" or "Shell_TrayWnd")
        {
            AppNameText.Text = L.T("Рабочий стол");
            return;
        }

        string? exePath = NativeMethods.GetProcessPath(processId);
        if (exePath == null)
        {
            AppNameText.Text = NativeMethods.GetWindowTitle(foreground);
            return;
        }

        if (!_appNames.TryGetValue(exePath, out string? name))
        {
            name = exePath.EndsWith("ApplicationFrameHost.exe", StringComparison.OrdinalIgnoreCase)
                ? NativeMethods.GetWindowTitle(foreground)
                : WindowEnumerator.GetAppDisplayName(exePath, NativeMethods.GetWindowTitle(foreground));
            _appNames[exePath] = name;
        }

        AppNameText.Text = name;
    }

    private void UpdateClock()
    {
        var settings = AppSettings.Current;
        var culture = L.Culture;
        var now = DateTime.Now;
        string time = now.ToString(settings.ClockShowSeconds ? "HH:mm:ss" : "HH:mm", culture);
        if (!settings.ClockShowDate)
        {
            ClockText.Text = time;
            return;
        }

        ClockText.Text = Capitalize(now.ToString("ddd d MMM", culture).Replace(".", "")) + "   " + time;
    }

    private void UpdateLanguage()
    {
        LanguageText.Text = GetLanguageCode(GetCurrentLayout());
    }

    private IntPtr GetCurrentLayout()
    {
        uint threadId = NativeMethods.GetWindowThreadProcessId(_lastForeground, out _);
        return NativeMethods.GetKeyboardLayout(threadId);
    }

    private void UpdateVolume()
    {
        var volume = AudioVolume.Get();
        if (volume == null)
        {
            VolumeIcon.Text = "";
            return;
        }

        var (level, muted) = volume.Value;
        VolumeIcon.Text = muted || level < 0.01f ? ""
            : level < 0.34f ? ""
            : level < 0.67f ? ""
            : "";
    }

    private void UpdateBattery()
    {
        if (!TryGetBattery(out var status))
        {
            BatteryItem.Visibility = Visibility.Collapsed;
            return;
        }

        BatteryItem.Visibility = Visibility.Visible;
        int percent = status.BatteryLifePercent;
        bool charging = status.ACLineStatus == 1;
        BatteryText.Text = (charging ? "⚡ " : "") + (AppSettings.Current.ShowBatteryPercent ? percent + "%" : "");
        // E850..E859 — уровни 0..90%, E83F — полная
        BatteryIcon.Text = percent >= 95 ? "" : ((char)(0xE850 + percent / 10)).ToString();
    }

    private void UpdateNetwork()
    {
        var active = GetActiveAdapters();
        bool hasWireless = active.Any(adapter => adapter.NetworkInterfaceType == NetworkInterfaceType.Wireless80211);
        NetworkIcon.Text = hasWireless || active.Count == 0 ? "" : "";
        NetworkIcon.Opacity = active.Count == 0 ? 0.4 : 1;
    }

    private static List<NetworkInterface> GetActiveAdapters() => NetworkInterface.GetAllNetworkInterfaces()
        .Where(adapter => adapter.OperationalStatus == OperationalStatus.Up
            && adapter.NetworkInterfaceType is not NetworkInterfaceType.Loopback and not NetworkInterfaceType.Tunnel
            && !adapter.Description.Contains("Virtual", StringComparison.OrdinalIgnoreCase)
            && !adapter.Description.Contains("Hyper-V", StringComparison.OrdinalIgnoreCase)
            && !adapter.Description.Contains("VPN", StringComparison.OrdinalIgnoreCase))
        .ToList();

    private static bool TryGetBattery(out SYSTEM_POWER_STATUS status) =>
        NativeMethods.GetSystemPowerStatus(out status) && (status.BatteryFlag & 128) == 0 && status.BatteryLifePercent <= 100;

    // ---------- Меню ----------

    private void OnLogoClick(object sender, MouseButtonEventArgs e)
    {
        new MacMenu()
            .Add(L.T("Об этом компьютере"), () => SystemActions.OpenUri("ms-settings:about"))
            .Separator()
            .Add(L.T("Параметры Windows…"), () => SystemActions.OpenUri("ms-settings:"))
            .Add(L.T("Все приложения"), () => App.Current.ShowLauncher())
            .Add(L.T("Настройки winToMac…"), () => App.Current.ShowSettings())
            .Add(L.T("Живые обои…"), () => App.Current.ShowWallpapers())
            .Add(L.T("Инструкция"), AboutWindow.OpenInstructions)
            .Add(L.T("Язык интерфейса: {0}", L.NameOf(L.Code)), () => LanguagePicker.ShowMenu(LogoItem))
            .Add(L.T("О программе и поддержка ♥"), AboutWindow.ShowSingle)
            .Add(L.T("Проверить обновления…"), () => _ = UpdateWindow.CheckAsync(manual: true))
            .Add(L.T("💡 Предложить функцию (Telegram)"), () => SystemActions.OpenUri(ProjectLinks.Telegram))
            .Separator()
            .Add(L.T("Свернуть все окна"), () => App.Current.ShowDesktop(), hint: "Win+D")
            .Add(L.T("Меню «Пуск»"), SystemActions.OpenStartMenu, hint: "Win")
            .Add(L.T("Диспетчер задач"), () => SystemActions.OpenUri("taskmgr.exe"), hint: "Ctrl+Shift+Esc")
            .Separator()
            .Add(L.T("Сон"), SystemActions.Sleep)
            .Add(L.T("Перезагрузить…"), () => Confirm(L.T("Перезагрузить компьютер?"), SystemActions.Restart))
            .Add(L.T("Выключить…"), () => Confirm(L.T("Выключить компьютер?"), SystemActions.ShutDown))
            .Separator()
            .Add(L.T("Заблокировать экран"), SystemActions.Lock, hint: "Win+L")
            .Add(L.T("Завершить сеанс…"), () => Confirm(L.T("Выйти из учётной записи?"), SystemActions.SignOut))
            .Separator()
            .Add(L.T("Выйти из winToMac"), () => App.Current.Quit())
            .Show(LogoItem, PlacementMode.Bottom);
    }

    private void OnAppNameClick(object sender, MouseButtonEventArgs e)
    {
        IntPtr target = _lastForeground;
        bool isWindow = target != IntPtr.Zero && NativeMethods.IsWindow(target)
            && NativeMethods.GetWindowClass(target) is not ("Progman" or "WorkerW");

        new MacMenu()
            .Header(AppNameText.Text)
            .Add(L.T("Свернуть окно"), () => WindowEnumerator.Minimize(target), enabled: isWindow)
            .Add(L.T("Закрыть окно"), () => WindowEnumerator.Close(target), enabled: isWindow, hint: "Alt+F4")
            .Separator()
            .Add(L.T("Свернуть все окна"), () => App.Current.ShowDesktop(), hint: "Win+D")
            .Add(L.T("Все приложения"), () => App.Current.ShowLauncher())
            .Show(AppNameItem, PlacementMode.Bottom);
    }

    private void OnLanguageClick(object sender, MouseButtonEventArgs e)
    {
        IntPtr target = _lastForeground;
        IntPtr current = GetCurrentLayout();
        int count = NativeMethods.GetKeyboardLayoutList(0, null);
        var layouts = new IntPtr[count];
        NativeMethods.GetKeyboardLayoutList(count, layouts);

        var menu = new MacMenu().Header(L.T("Раскладка клавиатуры"));
        foreach (var layout in layouts)
        {
            menu.Add(GetLanguageName(layout), () =>
            {
                // Раскладка переключается у того окна, в котором пользователь печатает
                NativeMethods.PostMessage(target, NativeMethods.WM_INPUTLANGCHANGEREQUEST, IntPtr.Zero, layout);
                Dispatcher.BeginInvoke(UpdateLanguage, DispatcherPriority.Background);
            }, isChecked: layout == current, hint: GetLanguageCode(layout));
        }

        menu.Separator()
            .Add(L.T("Настройки языка…"), () => SystemActions.OpenUri("ms-settings:regionlanguage"))
            .Show(LanguageItem, PlacementMode.Bottom);
    }

    private void OnNetworkClick(object sender, MouseButtonEventArgs e)
    {
        var menu = new MacMenu();
        var networks = Wifi.GetNetworks();
        bool hasWifi = networks.Count > 0 || Wifi.IsAvailable;

        menu.Header("Wi‑Fi");
        if (!hasWifi)
        {
            menu.Add(L.T("Адаптер Wi‑Fi не найден"), () => { }, enabled: false);
        }

        foreach (var network in networks.Take(12))
        {
            string hint = (network.IsSecured ? "🔒  " : "") + SignalBars(network.Signal);
            menu.Add(network.Ssid, () =>
            {
                if (network.IsConnected)
                {
                    return;
                }

                // Сохранённые сети подключаем сами, для новых нужен пароль — открываем окно Windows
                if (!Wifi.Connect(network))
                {
                    SystemActions.OpenUri("ms-availablenetworks:");
                }
            }, isChecked: network.IsConnected, hint: hint);
        }

        var connected = networks.FirstOrDefault(network => network.IsConnected);
        if (connected != null)
        {
            menu.Separator().Add(L.T("Отключиться от «{0}»", connected.Ssid), () => Wifi.Disconnect(connected));
        }

        if (GetActiveAdapters().Any(adapter => adapter.NetworkInterfaceType == NetworkInterfaceType.Ethernet))
        {
            menu.Separator().Header("Ethernet").Add(L.T("Подключено"), () => { }, enabled: false);
        }

        menu.Separator()
            .Add(L.T("Другие сети…"), () => SystemActions.OpenUri("ms-availablenetworks:"))
            .Add(L.T("Настройки сети…"), () => SystemActions.OpenUri("ms-settings:network"))
            .Show(NetworkItem, PlacementMode.Bottom);

        // К следующему открытию список будет свежим
        Task.Run(Wifi.Scan);
    }

    private void OnBatteryClick(object sender, MouseButtonEventArgs e)
    {
        var menu = new MacMenu().Header(L.T("Батарея"));
        if (TryGetBattery(out var status))
        {
            string state = status.ACLineStatus == 1
                ? (status.BatteryLifePercent >= 100 ? L.T("заряжена") : L.T("заряжается"))
                : L.T("от батареи");
            menu.Add($"{status.BatteryLifePercent}% — {state}", () => { }, enabled: false);
            if (status.ACLineStatus != 1 && status.BatteryLifeTime > 0)
            {
                var left = TimeSpan.FromSeconds(status.BatteryLifeTime);
                menu.Add(L.T("Осталось примерно {0} ч {1} мин", (int)left.TotalHours, left.Minutes), () => { }, enabled: false);
            }
        }

        menu.Separator()
            .Add(L.T("Экономия заряда…"), () => SystemActions.OpenUri("ms-settings:batterysaver"))
            .Add(L.T("Питание и спящий режим…"), () => SystemActions.OpenUri("ms-settings:powersleep"))
            .Show(BatteryItem, PlacementMode.Bottom);
    }

    private void OnVolumeClick(object sender, MouseButtonEventArgs e)
    {
        var volume = AudioVolume.Get();
        var slider = new Slider
        {
            Minimum = 0,
            Maximum = 1,
            Value = volume?.Level ?? 0,
            Width = 220,
            Margin = new Thickness(18, 6, 18, 8),
            IsEnabled = volume != null,
        };
        slider.ValueChanged += (_, args) =>
        {
            AudioVolume.SetLevel((float)args.NewValue);
            UpdateVolume();
        };

        bool muted = volume?.Muted ?? false;
        new MacMenu()
            .Header(L.T("Звук"))
            .AddElement(slider)
            .Add(L.T("Без звука"), () =>
            {
                AudioVolume.SetMute(!muted);
                UpdateVolume();
            }, enabled: volume != null, isChecked: muted)
            .Separator()
            .Add(L.T("Микшер громкости…"), () => SystemActions.OpenUri("ms-settings:apps-volume"))
            .Add(L.T("Настройки звука…"), () => SystemActions.OpenUri("ms-settings:sound"))
            .Show(VolumeItem, PlacementMode.Bottom);
    }

    private void OnVolumeWheel(object sender, MouseWheelEventArgs e)
    {
        var volume = AudioVolume.Get();
        if (volume == null)
        {
            return;
        }

        AudioVolume.SetLevel(volume.Value.Level + (e.Delta > 0 ? 0.02f : -0.02f));
        UpdateVolume();
    }

    private void OnClockClick(object sender, MouseButtonEventArgs e)
    {
        var culture = L.Culture;
        var calendar = new System.Windows.Controls.Calendar
        {
            Margin = new Thickness(8, 2, 8, 4),
            FirstDayOfWeek = culture.DateTimeFormat.FirstDayOfWeek,
            Language = System.Windows.Markup.XmlLanguage.GetLanguage(culture.IetfLanguageTag),
            IsTodayHighlighted = true,
            SelectedDate = DateTime.Today,
        };

        new MacMenu()
            .Header(Capitalize(DateTime.Now.ToString("dddd, d MMMM yyyy", culture)))
            .AddElement(calendar)
            .Separator()
            .Add(L.T("Уведомления"), SystemActions.OpenNotifications, hint: "Win+N")
            .Add(L.T("Настройки даты и времени…"), () => SystemActions.OpenUri("ms-settings:dateandtime"))
            .Show(ClockItem, PlacementMode.Bottom);
    }

    // ---------- Вспомогательное ----------

    private static string GetLanguageCode(IntPtr layout)
    {
        try
        {
            return new CultureInfo((int)(layout.ToInt64() & 0xFFFF)).TwoLetterISOLanguageName.ToUpperInvariant();
        }
        catch (CultureNotFoundException)
        {
            return "??";
        }
    }

    private static string GetLanguageName(IntPtr layout)
    {
        try
        {
            return Capitalize(new CultureInfo((int)(layout.ToInt64() & 0xFFFF)).NativeName);
        }
        catch (CultureNotFoundException)
        {
            return L.T("Неизвестная раскладка");
        }
    }

    private static string SignalBars(int quality) => quality switch
    {
        >= 75 => "▂▄▆█",
        >= 50 => "▂▄▆",
        >= 25 => "▂▄",
        _ => "▂",
    };

    private static string Capitalize(string text) =>
        text.Length == 0 ? text : char.ToUpper(text[0], L.Culture) + text[1..];

    private static void Confirm(string question, Action action)
    {
        if (MessageBox.Show(question, "winToMac", MessageBoxButton.OKCancel, MessageBoxImage.Question)
            == MessageBoxResult.OK)
        {
            action();
        }
    }
}
