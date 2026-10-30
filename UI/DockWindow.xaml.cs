using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using WinToMac.Native;
using WinToMac.Services;
using WinToMac.Settings;
using WinToMac.Localization;

namespace WinToMac.UI;

public partial class DockWindow : Window
{
    /// <summary>Скорость сглаживания размеров иконок (доля пути за кадр).</summary>
    private const double SizeEasing = 0.35;

    /// <summary>Радиус влияния увеличения — в «базовых» иконках.</summary>
    private const double MagnificationRange = 3.0;

    private readonly DispatcherTimer _frameTimer = new() { Interval = TimeSpan.FromMilliseconds(16) };
    private readonly DispatcherTimer _refreshTimer = new() { Interval = TimeSpan.FromSeconds(1) };
    private List<DockItem> _items = new();
    private DockMetrics _metrics = new(AppSettings.Current);
    private string _signature = "";
    private IntPtr _hwnd;
    private AppBar? _appBar;
    private int _windowTopPx;
    private bool _isHidden;
    private bool? _appliedAutoHide;
    private int _ticksAway;

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern int SHEmptyRecycleBin(IntPtr hwnd, string? rootPath, uint flags);

    public DockWindow()
    {
        InitializeComponent();
        SourceInitialized += OnSourceInitialized;
        Closed += (_, _) => _appBar?.Dispose();
        Drop += OnDrop;
        AppSettings.Changed += ApplySettings;

        _frameTimer.Tick += (_, _) => OnFrame();
        _refreshTimer.Tick += (_, _) => Refresh();
    }

    private void OnSourceInitialized(object? sender, EventArgs e)
    {
        _hwnd = new WindowInteropHelper(this).Handle;
        // Док не забирает фокус: иначе клик по иконке «активного» приложения не сможет его свернуть
        NativeMethods.AddExtendedStyle(_hwnd, NativeMethods.WS_EX_TOOLWINDOW | NativeMethods.WS_EX_NOACTIVATE);
        _appBar = new AppBar(HwndSource.FromHwnd(_hwnd)!, AppBarEdge.Bottom);
        _appBar.PositionChanged += PlaceWindow;

        ApplySettings();
        _frameTimer.Start();
        _refreshTimer.Start();
    }

    protected override void OnDpiChanged(DpiScale oldDpi, DpiScale newDpi)
    {
        base.OnDpiChanged(oldDpi, newDpi);
        ApplySettings();
    }

    private void ApplySettings()
    {
        _metrics = new DockMetrics(AppSettings.Current);
        DockBackground.Height = _metrics.BackgroundHeight;
        DockBackground.CornerRadius = new CornerRadius(Math.Min(_metrics.IconSize * 0.32, 22));
        DockArea.Margin = new Thickness(0, 0, 0, _metrics.BottomMargin);
        double sideMargin = Math.Max(0, _metrics.Padding - _metrics.Gap / 2);
        ItemsPanel.Margin = new Thickness(sideMargin, 0, sideMargin, 0);

        var settings = AppSettings.Current;
        double scale = VisualTreeHelper.GetDpi(this).DpiScaleY;
        if (settings.DockAutoHide || !settings.DockReserveSpace)
        {
            _appBar?.Dispose();
            WorkArea.SetBottom(0);
        }
        else
        {
            int reserved = (int)Math.Round((_metrics.BackgroundHeight + _metrics.BottomMargin) * scale);
            _appBar?.Reserve(reserved);
            WorkArea.SetBottom(reserved);
        }

        // Прячем/показываем док только при переключении автоскрытия, а не при любой смене настроек
        if (_appliedAutoHide != settings.DockAutoHide)
        {
            _appliedAutoHide = settings.DockAutoHide;
            SetHidden(settings.DockAutoHide, animate: false);
        }

        PlaceWindow();
        Refresh(force: true);
    }

    private void PlaceWindow()
    {
        if (_hwnd == IntPtr.Zero)
        {
            return;
        }

        double scale = VisualTreeHelper.GetDpi(this).DpiScaleY;
        var (width, height) = NativeMethods.GetPrimaryScreenSize();
        int windowHeight = (int)Math.Ceiling(_metrics.WindowHeight * scale);
        _windowTopPx = height - windowHeight;
        NativeMethods.SetWindowPos(_hwnd, NativeMethods.HWND_TOPMOST, 0, _windowTopPx, width, windowHeight,
            // Без SWP_SHOWWINDOW: показывать и прятать окно (полноэкранные игры) решает только winToMac,
            // иначе пересчёт места при запуске игры снова показывает уже спрятанную панель
            NativeMethods.SWP_NOACTIVATE);
    }

    // ---------- Состав дока ----------

    private void Refresh(bool force = false)
    {
        var settings = AppSettings.Current;
        var windows = WindowEnumerator.GetTaskbarWindows();
        var remaining = new List<WindowInfo>(windows);
        var items = new List<DockItem> { DockItem.Launcher() };

        foreach (var pinned in settings.PinnedApps)
        {
            var item = DockItem.ForPinned(pinned);
            foreach (var window in windows.Where(item.Matches))
            {
                item.Windows.Add(window);
                remaining.Remove(window);
            }

            items.Add(item);
        }

        if (settings.DockShowUnpinnedRunning && remaining.Count > 0)
        {
            items.Add(DockItem.Separator("separator-running"));
            foreach (var group in remaining.GroupBy(DockItem.GroupKey))
            {
                var item = DockItem.ForRunning(group.First());
                item.Windows.AddRange(group);
                items.Add(item);
            }
        }

        if (settings.DockShowDesktopButton || settings.DockShowTrash)
        {
            items.Add(DockItem.Separator("separator-trash"));
        }

        if (settings.DockShowDesktopButton)
        {
            items.Add(DockItem.ShowDesktop());
        }

        if (settings.DockShowTrash)
        {
            items.Add(DockItem.Trash());
        }

        string signature = string.Join("|", items.Select(item => item.Key));
        if (!force && signature == _signature)
        {
            // Состав прежний — обновляем только списки окон и точки «запущено»
            for (int index = 0; index < items.Count; index++)
            {
                _items[index].Windows.Clear();
                _items[index].Windows.AddRange(items[index].Windows);
                _items[index].UpdateRunningState(_metrics);
            }

            return;
        }

        _signature = signature;
        _items = items;
        ItemsPanel.Children.Clear();
        foreach (var item in _items)
        {
            item.BuildView(_metrics);
            if (item.IsIcon)
            {
                item.View.MouseLeftButtonUp += (_, e) =>
                {
                    e.Handled = true;
                    OnItemClick(item);
                };
                item.View.MouseRightButtonUp += (_, e) =>
                {
                    e.Handled = true;
                    ShowItemMenu(item);
                };
            }

            ItemsPanel.Children.Add(item.View);
        }
    }

    // ---------- Кадр: увеличение, подпись, автоскрытие ----------

    private void OnFrame()
    {
        if (!IsVisible || _items.Count == 0)
        {
            return;
        }

        NativeMethods.GetCursorPos(out var cursor);
        double scale = VisualTreeHelper.GetDpi(this).DpiScaleY;
        var point = new Point(cursor.X / scale, (cursor.Y - _windowTopPx) / scale);
        bool isOverDock = !_isHidden && !MacMenu.IsAnyOpen && IsInside(DockArea, point);

        UpdateAutoHide(cursor, isOverDock, scale);
        UpdateTargetSizes(isOverDock ? point.X : null);

        foreach (var item in _items.Where(item => item.IsIcon))
        {
            double difference = item.TargetSize - item.CurrentSize;
            if (Math.Abs(difference) > 0.3)
            {
                item.ApplySize(item.CurrentSize + difference * SizeEasing);
            }
            else if (difference != 0)
            {
                item.ApplySize(item.TargetSize);
            }
        }

        UpdateLabel(isOverDock ? _items.FirstOrDefault(item => item.IsIcon && IsInsideHorizontally(item.View, point.X)) : null);
    }

    /// <summary>
    /// Размеры считаются от «неувеличенных» позиций иконок: так результат зависит только
    /// от курсора, и док не дрожит, пока расширяется.
    /// </summary>
    private void UpdateTargetSizes(double? cursorX)
    {
        double baseSize = _metrics.IconSize;
        foreach (var item in _items)
        {
            item.TargetSize = baseSize;
        }

        if (cursorX == null || _metrics.MaxSize <= baseSize)
        {
            return;
        }

        double sideMargin = Math.Max(0, _metrics.Padding - _metrics.Gap / 2);
        double totalWidth = _items.Sum(_metrics.SlotWidth) + sideMargin * 2;
        double offset = (Root.ActualWidth - totalWidth) / 2 + sideMargin;
        double range = baseSize * MagnificationRange;

        foreach (var item in _items)
        {
            double slot = _metrics.SlotWidth(item);
            double distance = Math.Abs(cursorX.Value - (offset + slot / 2));
            offset += slot;
            if (item.IsIcon && distance < range)
            {
                double factor = (Math.Cos(Math.PI * distance / range) + 1) / 2;
                item.TargetSize = baseSize + (_metrics.MaxSize - baseSize) * factor;
            }
        }
    }

    private void UpdateLabel(DockItem? hovered)
    {
        if (hovered == null || string.IsNullOrEmpty(hovered.Name))
        {
            Label.Visibility = Visibility.Collapsed;
            return;
        }

        LabelText.Text = hovered.Name;
        Label.Visibility = Visibility.Visible;
        Label.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));

        var topLeft = hovered.View.TranslatePoint(new Point(0, 0), Root);
        double iconTop = topLeft.Y + hovered.View.ActualHeight - _metrics.DotArea - hovered.CurrentSize;
        double left = topLeft.X + hovered.View.ActualWidth / 2 - Label.DesiredSize.Width / 2;
        Canvas.SetLeft(Label, Math.Clamp(left, 4, Math.Max(4, Root.ActualWidth - Label.DesiredSize.Width - 4)));
        Canvas.SetTop(Label, Math.Max(0, iconTop - Label.DesiredSize.Height - 8));
    }

    private void UpdateAutoHide(POINT cursor, bool isOverDock, double scale)
    {
        if (!AppSettings.Current.DockAutoHide)
        {
            return;
        }

        var (_, screenHeight) = NativeMethods.GetPrimaryScreenSize();
        if (_isHidden)
        {
            if (cursor.Y >= screenHeight - 2)
            {
                SetHidden(false, animate: true);
            }

            return;
        }

        double dockTopPx = screenHeight - (_metrics.BackgroundHeight + _metrics.BottomMargin) * scale;
        bool isNear = isOverDock || MacMenu.IsAnyOpen || cursor.Y >= dockTopPx;
        _ticksAway = isNear ? 0 : _ticksAway + 1;
        if (_ticksAway > 30)
        {
            SetHidden(true, animate: true);
        }
    }

    private void SetHidden(bool hidden, bool animate)
    {
        _isHidden = hidden;
        _ticksAway = 0;
        double target = hidden ? _metrics.BackgroundHeight + _metrics.BottomMargin + 12 : 0;
        if (!animate)
        {
            HideTransform.BeginAnimation(TranslateTransform.YProperty, null);
            HideTransform.Y = target;
            return;
        }

        HideTransform.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(target, TimeSpan.FromMilliseconds(220))
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut },
        });
    }

    private bool IsInside(FrameworkElement element, Point point)
    {
        var topLeft = element.TranslatePoint(new Point(0, 0), Root);
        return new Rect(topLeft, new Size(element.ActualWidth, element.ActualHeight)).Contains(point);
    }

    private bool IsInsideHorizontally(FrameworkElement element, double x)
    {
        double left = element.TranslatePoint(new Point(0, 0), Root).X;
        return x >= left && x < left + element.ActualWidth;
    }

    // ---------- Клики и меню ----------

    private void OnItemClick(DockItem item)
    {
        switch (item.Kind)
        {
            case DockItemKind.Launcher:
                App.Current.ShowLauncher();
                return;
            case DockItemKind.Trash:
                AppCatalog.Launch("shell:RecycleBinFolder");
                return;
            case DockItemKind.ShowDesktop:
                App.Current.ShowDesktop();
                return;
        }

        var windows = item.Windows.Where(window => NativeMethods.IsWindow(window.Handle)).ToList();
        if (windows.Count == 0)
        {
            if (item.LaunchTarget is { } target)
            {
                AppCatalog.Launch(target);
                item.Bounce();
            }

            return;
        }

        // Как в macOS: клик по активному приложению сворачивает его (или листает его окна)
        IntPtr foreground = NativeMethods.GetForegroundWindow();
        int activeIndex = windows.FindIndex(window => window.Handle == foreground);
        if (activeIndex < 0)
        {
            WindowEnumerator.Activate(windows[0].Handle);
        }
        else if (windows.Count == 1)
        {
            WindowEnumerator.Minimize(foreground);
        }
        else
        {
            WindowEnumerator.Activate(windows[(activeIndex + 1) % windows.Count].Handle);
        }
    }

    private void ShowItemMenu(DockItem item)
    {
        var menu = new MacMenu();
        switch (item.Kind)
        {
            case DockItemKind.Launcher:
                menu.Add(L.T("Открыть"), () => App.Current.ShowLauncher())
                    .Add(L.T("Настройки winToMac…"), () => App.Current.ShowSettings());
                break;

            case DockItemKind.ShowDesktop:
                menu.Add(L.T("Свернуть все окна"), () => App.Current.ShowDesktop(), hint: "Win+D")
                    .Add(L.T("Убрать из дока"), () =>
                    {
                        AppSettings.Current.DockShowDesktopButton = false;
                        AppSettings.Apply();
                    });
                break;

            case DockItemKind.Trash:
                menu.Add(L.T("Открыть"), () => AppCatalog.Launch("shell:RecycleBinFolder"))
                    .Add(L.T("Очистить корзину…"), () => SHEmptyRecycleBin(IntPtr.Zero, null, 0));
                break;

            default:
                BuildAppMenu(menu, item);
                break;
        }

        menu.Show(item.View, PlacementMode.Top);
    }

    private void BuildAppMenu(MacMenu menu, DockItem item)
    {
        menu.Header(item.Name);
        foreach (var window in item.Windows.Take(12))
        {
            string title = window.Title.Length > 60 ? window.Title[..57] + "…" : window.Title;
            menu.Add(title, () => WindowEnumerator.Activate(window.Handle));
        }

        if (item.Windows.Count > 0)
        {
            menu.Separator();
        }

        if (item.LaunchTarget is { } target)
        {
            menu.Add(item.IsRunning ? L.T("Новое окно") : L.T("Открыть"), () =>
            {
                AppCatalog.Launch(target);
                item.Bounce();
            });
        }

        var pinnedApps = AppSettings.Current.PinnedApps;
        if (item.Pinned != null)
        {
            int index = pinnedApps.IndexOf(item.Pinned);
            menu.Add(L.T("Сдвинуть влево"), () => MovePinned(index, -1), enabled: index > 0)
                .Add(L.T("Сдвинуть вправо"), () => MovePinned(index, 1), enabled: index < pinnedApps.Count - 1)
                .Add(L.T("Убрать из дока"), () =>
                {
                    pinnedApps.Remove(item.Pinned);
                    AppSettings.Apply();
                });
        }
        else if (item.CreatePinned() is { } pinned)
        {
            menu.Add(L.T("Оставить в доке"), () =>
            {
                pinnedApps.Add(pinned);
                AppSettings.Apply();
            });
        }

        if (item.IsRunning)
        {
            menu.Separator()
                .Add(item.Windows.Count > 1 ? L.T("Закрыть все окна") : L.T("Закрыть"), () =>
                {
                    foreach (var window in item.Windows)
                    {
                        WindowEnumerator.Close(window.Handle);
                    }
                });
        }
    }

    private static void MovePinned(int index, int direction)
    {
        var pinnedApps = AppSettings.Current.PinnedApps;
        int newIndex = index + direction;
        if (index < 0 || newIndex < 0 || newIndex >= pinnedApps.Count)
        {
            return;
        }

        (pinnedApps[index], pinnedApps[newIndex]) = (pinnedApps[newIndex], pinnedApps[index]);
        AppSettings.Apply();
    }

    private void OnDockBackgroundRightClick(object sender, MouseButtonEventArgs e)
    {
        var settings = AppSettings.Current;
        new MacMenu()
            .Add(L.T("Увеличение"), () =>
            {
                settings.DockMagnification = !settings.DockMagnification;
                AppSettings.Apply();
            }, isChecked: settings.DockMagnification)
            .Add(L.T("Автоматически скрывать"), () =>
            {
                settings.DockAutoHide = !settings.DockAutoHide;
                AppSettings.Apply();
            }, isChecked: settings.DockAutoHide)
            .Separator()
            .Add(L.T("Все приложения"), () => App.Current.ShowLauncher())
            .Add(L.T("Настройки дока…"), () => App.Current.ShowSettings())
            .Show(DockBackground, PlacementMode.Top);
    }

    /// <summary>Перетащили exe или ярлык на док — закрепляем.</summary>
    private void OnDrop(object sender, DragEventArgs e)
    {
        if (e.Data.GetData(DataFormats.FileDrop) is not string[] paths)
        {
            return;
        }

        foreach (string path in paths)
        {
            string extension = Path.GetExtension(path).ToLowerInvariant();
            if (extension is not (".exe" or ".lnk"))
            {
                continue;
            }

            string? exePath = extension == ".exe" ? path : ResolveShortcut(path);
            AppSettings.Current.PinnedApps.Add(new PinnedApp
            {
                Name = Path.GetFileNameWithoutExtension(path),
                LaunchTarget = path,
                IconSource = path,
                ExeName = exePath?.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) == true
                    ? Path.GetFileName(exePath).ToLowerInvariant()
                    : null,
            });
        }

        AppSettings.Apply();
    }

    private static string? ResolveShortcut(string path)
    {
        try
        {
            Type? shellType = Type.GetTypeFromProgID("Shell.Application");
            if (shellType == null)
            {
                return null;
            }

            dynamic shell = Activator.CreateInstance(shellType)!;
            dynamic? item = shell.NameSpace(Path.GetDirectoryName(path)).ParseName(Path.GetFileName(path));
            return item?.GetLink.Path as string;
        }
        catch
        {
            return null;
        }
    }
}
