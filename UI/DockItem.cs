using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using WinToMac.Native;
using WinToMac.Settings;
using WinToMac.Localization;

namespace WinToMac.UI;

public enum DockItemKind
{
    Launcher,
    App,
    Separator,
    ShowDesktop,
    Trash,
}

/// <summary>Элемент дока: модель (что за приложение, какие у него окна) и его визуальное представление.</summary>
public sealed class DockItem
{
    public DockItemKind Kind { get; }
    public PinnedApp? Pinned { get; }
    public string Name { get; }
    public string IconSource { get; }
    public List<WindowInfo> Windows { get; } = new();

    /// <summary>Ключ для сравнения состава дока между обновлениями.</summary>
    public string Key { get; }

    public FrameworkElement View { get; private set; } = null!;
    public double CurrentSize { get; set; }
    public double TargetSize { get; set; }

    private Image? _image;
    private Ellipse? _dot;
    private readonly TranslateTransform _bounce = new();

    public bool IsRunning => Windows.Count > 0;
    public bool IsIcon => Kind != DockItemKind.Separator;

    private DockItem(DockItemKind kind, string key, string name, string iconSource, PinnedApp? pinned)
    {
        Kind = kind;
        Key = key;
        Name = name;
        IconSource = iconSource;
        Pinned = pinned;
    }

    public static DockItem Launcher() => new(DockItemKind.Launcher, "launcher", L.T("Все приложения"), "", null);

    public static DockItem Trash() => new(DockItemKind.Trash, "trash", L.T("Корзина"), ShellIcons.RecycleBin, null);

    public static DockItem ShowDesktop() => new(DockItemKind.ShowDesktop, "desktop", L.T("Свернуть все окна"), "", null);

    public static DockItem Separator(string key) => new(DockItemKind.Separator, key, "", "", null);

    public static DockItem ForPinned(PinnedApp pinned) =>
        new(DockItemKind.App, "pin:" + pinned.LaunchTarget, pinned.Name, pinned.IconSource, pinned);

    /// <summary>Запущенное, но не закреплённое приложение.</summary>
    public static DockItem ForRunning(WindowInfo window)
    {
        bool isPackagedApp = window.AppId?.Contains('!') == true;
        string iconSource = isPackagedApp ? "shell:AppsFolder\\" + window.AppId : window.ExePath ?? "";
        bool isFrameHost = window.ExeName == "applicationframehost.exe";
        string name = isFrameHost ? window.Title : WindowEnumerator.GetAppDisplayName(window.ExePath, window.Title);
        return new DockItem(DockItemKind.App, "run:" + GroupKey(window), name, iconSource, null);
    }

    /// <summary>По этому ключу окна одного приложения собираются в один значок.</summary>
    public static string GroupKey(WindowInfo window) =>
        (window.AppId ?? window.ExePath ?? window.Handle.ToString()).ToLowerInvariant();

    public bool Matches(WindowInfo window)
    {
        if (Pinned == null)
        {
            return false;
        }

        if (Pinned.AppId != null && string.Equals(Pinned.AppId, window.AppId, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return Pinned.ExeName != null && Pinned.ExeName == window.ExeName;
    }

    /// <summary>Описание для закрепления запущенного приложения.</summary>
    public PinnedApp? CreatePinned()
    {
        if (Pinned != null)
        {
            return Pinned;
        }

        var window = Windows.FirstOrDefault();
        if (window == null)
        {
            return null;
        }

        bool isPackagedApp = window.AppId?.Contains('!') == true;
        string? launchTarget = isPackagedApp ? "shell:AppsFolder\\" + window.AppId : window.ExePath;
        if (launchTarget == null)
        {
            return null;
        }

        return new PinnedApp
        {
            Name = Name,
            LaunchTarget = launchTarget,
            AppId = window.AppId,
            ExeName = window.ExeName,
            IconSource = IconSource,
        };
    }

    public string? LaunchTarget => Pinned?.LaunchTarget
        ?? (Windows.FirstOrDefault() is { } window
            ? (window.AppId?.Contains('!') == true ? "shell:AppsFolder\\" + window.AppId : window.ExePath)
            : null);

    // ---------- Отрисовка ----------

    public void BuildView(DockMetrics metrics)
    {
        CurrentSize = TargetSize = metrics.IconSize;

        if (Kind == DockItemKind.Separator)
        {
            var line = new Border
            {
                Width = 1,
                Height = metrics.IconSize * 0.8,
                Margin = new Thickness(metrics.Gap, 0, metrics.Gap, metrics.DotArea + metrics.IconSize * 0.1),
                VerticalAlignment = VerticalAlignment.Bottom,
            };
            line.SetResourceReference(Border.BackgroundProperty, Theme.MenuSeparator);
            View = line;
            return;
        }

        _image = new Image
        {
            Width = metrics.IconSize,
            Height = metrics.IconSize,
            VerticalAlignment = VerticalAlignment.Bottom,
            RenderTransform = _bounce,
        };
        RenderOptions.SetBitmapScalingMode(_image, BitmapScalingMode.HighQuality);

        _dot = new Ellipse
        {
            Width = 4,
            Height = 4,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Bottom,
            Margin = new Thickness(0, 0, 0, 1),
        };
        _dot.SetResourceReference(Shape.FillProperty, Theme.BarForeground);

        var panel = new StackPanel { VerticalAlignment = VerticalAlignment.Bottom };
        panel.Children.Add(_image);
        var dotHolder = new Grid { Height = metrics.DotArea };
        dotHolder.Children.Add(_dot);
        panel.Children.Add(dotHolder);

        View = new Border
        {
            // Прозрачная, но не null-подложка: промежутки между иконками тоже принимают клики
            Background = new SolidColorBrush(Color.FromArgb(1, 0, 0, 0)),
            Padding = new Thickness(metrics.Gap / 2, 0, metrics.Gap / 2, 0),
            VerticalAlignment = VerticalAlignment.Bottom,
            Child = panel,
            Tag = this,
        };

        UpdateRunningState(metrics);
        LoadIcon();
    }

    public void UpdateRunningState(DockMetrics metrics)
    {
        if (_dot != null)
        {
            _dot.Visibility = IsRunning && metrics.ShowRunningIndicator ? Visibility.Visible : Visibility.Hidden;
        }
    }

    public void ApplySize(double size)
    {
        CurrentSize = size;
        if (_image != null)
        {
            _image.Width = size;
            _image.Height = size;
        }
    }

    /// <summary>Подпрыгивание при запуске приложения.</summary>
    public void Bounce()
    {
        var animation = new DoubleAnimation(0, -CurrentSize * 0.45, TimeSpan.FromMilliseconds(280))
        {
            AutoReverse = true,
            RepeatBehavior = new RepeatBehavior(2),
            EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut },
        };
        _bounce.BeginAnimation(TranslateTransform.YProperty, animation);
    }

    private async void LoadIcon()
    {
        if (_image == null)
        {
            return;
        }

        if (Kind == DockItemKind.Launcher)
        {
            _image.Source = LauncherIcon.Create();
            return;
        }

        if (Kind == DockItemKind.ShowDesktop)
        {
            _image.Source = ShowDesktopIcon.Create();
            return;
        }

        // После входа в систему оболочка отдаёт иконки не сразу — пробуем ещё несколько раз
        for (int attempt = 0; attempt < 6; attempt++)
        {
            var icon = await ShellIcons.GetAsync(IconSource);
            if (icon == null && Pinned != null && File.Exists(Pinned.LaunchTarget))
            {
                icon = await ShellIcons.GetAsync(Pinned.LaunchTarget);
            }

            if (icon != null)
            {
                _image.Source = icon;
                return;
            }

            // Оболочка иконку не отдала (портативные программы, нестандартные установщики) —
            // пока берём ту, что показывает само окно, и продолжаем пробовать получить крупную
            if (_image.Source == null && Windows.Count > 0)
            {
                _image.Source = WindowIcon.Get(Windows[0].Handle);
            }

            await Task.Delay(TimeSpan.FromSeconds(2 + attempt * 3));
        }
    }
}

/// <summary>Размеры дока в DIP, вычисленные из настроек.</summary>
public sealed class DockMetrics
{
    public double IconSize { get; }
    public double MaxSize { get; }
    public double Gap { get; }
    public double Padding { get; }
    public double DotArea => 7;
    public double BottomMargin => 4;
    public double LabelSpace => 40;
    public bool ShowRunningIndicator { get; }

    public DockMetrics(AppSettings settings)
    {
        IconSize = Math.Clamp(settings.DockIconSize, 24, 160);
        MaxSize = settings.DockMagnification ? Math.Max(IconSize, Math.Clamp(settings.DockMagnifiedSize, 24, 256)) : IconSize;
        Gap = Math.Max(4, IconSize * 0.1);
        Padding = Math.Max(5, IconSize * 0.12);
        ShowRunningIndicator = settings.DockShowRunningIndicator;
    }

    /// <summary>Высота подложки дока.</summary>
    public double BackgroundHeight => IconSize + DotArea + Padding;

    /// <summary>Высота всего окна дока: с запасом под увеличение и подпись.</summary>
    public double WindowHeight => MaxSize + DotArea + Padding + BottomMargin + LabelSpace + IconSize * 0.5;

    public double SlotWidth(DockItem item) => item.IsIcon ? IconSize + Gap : 1 + Gap * 2;
}

/// <summary>Значок «Свернуть все окна» — рабочий стол с окнами, уезжающими вниз.</summary>
internal static class ShowDesktopIcon
{
    private static DrawingImage? _image;

    public static DrawingImage Create()
    {
        if (_image != null)
        {
            return _image;
        }

        var group = new DrawingGroup();
        var background = new LinearGradientBrush(Color.FromRgb(0x3A, 0x8D, 0xFF), Color.FromRgb(0x1B, 0x4F, 0xC4), 90);
        group.Children.Add(new GeometryDrawing(background, null, new RectangleGeometry(new Rect(4, 4, 92, 92), 22, 22)));

        var window = new SolidColorBrush(Color.FromArgb(0xF0, 0xFF, 0xFF, 0xFF));
        var windowBack = new SolidColorBrush(Color.FromArgb(0x90, 0xFF, 0xFF, 0xFF));
        group.Children.Add(new GeometryDrawing(windowBack, null, new RectangleGeometry(new Rect(30, 22, 46, 32), 5, 5)));
        group.Children.Add(new GeometryDrawing(window, null, new RectangleGeometry(new Rect(22, 30, 46, 32), 5, 5)));

        // Стрелка вниз
        var arrow = Geometry.Parse("M 50,66 L 50,80 M 42,73 L 50,81 L 58,73");
        group.Children.Add(new GeometryDrawing(null, new Pen(Brushes.White, 5)
        {
            StartLineCap = PenLineCap.Round,
            EndLineCap = PenLineCap.Round,
            LineJoin = PenLineJoin.Round,
        }, arrow));

        group.Freeze();
        _image = new DrawingImage(group);
        _image.Freeze();
        return _image;
    }
}

/// <summary>Значок «Все приложения» — сетка цветных квадратиков, как у Launchpad.</summary>
internal static class LauncherIcon
{
    private static DrawingImage? _image;

    public static DrawingImage Create()
    {
        if (_image != null)
        {
            return _image;
        }

        var group = new DrawingGroup();
        var background = new LinearGradientBrush(Color.FromRgb(0x5B, 0x5F, 0x6B), Color.FromRgb(0x2D, 0x30, 0x38), 90);
        group.Children.Add(new GeometryDrawing(background, null, new RectangleGeometry(new Rect(4, 4, 92, 92), 22, 22)));

        Color[] colors =
        {
            Color.FromRgb(0xFF, 0x5F, 0x57), Color.FromRgb(0xFE, 0xBC, 0x2E), Color.FromRgb(0x28, 0xC8, 0x40),
            Color.FromRgb(0x0A, 0x84, 0xFF), Color.FromRgb(0xBF, 0x5A, 0xF2), Color.FromRgb(0xFF, 0x37, 0x5F),
            Color.FromRgb(0x64, 0xD2, 0xFF), Color.FromRgb(0xFF, 0x9F, 0x0A), Color.FromRgb(0x30, 0xD1, 0x58),
        };
        for (int index = 0; index < 9; index++)
        {
            double x = 20 + index % 3 * 22;
            double y = 20 + index / 3 * 22;
            group.Children.Add(new GeometryDrawing(
                new SolidColorBrush(colors[index]), null, new RectangleGeometry(new Rect(x, y, 16, 16), 4, 4)));
        }

        group.Freeze();
        _image = new DrawingImage(group);
        _image.Freeze();
        return _image;
    }
}
