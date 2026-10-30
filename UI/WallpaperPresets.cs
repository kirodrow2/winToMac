using System.Globalization;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using WinToMac.Localization;

namespace WinToMac.UI;

/// <summary>
/// Встроенная анимированная заставка. Сцена описывается один раз и рисуется двумя способами:
/// WPF — для превью в галерее, HTML/CSS/SVG — для самих обоев (их показывает WebView2).
/// </summary>
public sealed class WallpaperPreset
{
    private readonly string _name;
    private readonly string _group;

    public string Id { get; }
    public string Name => L.T(_name);
    public string Group => L.T(_group);
    private readonly Func<(FrameworkElement View, Storyboard Animation)> _createView;
    private readonly Func<string> _createHtml;

    public WallpaperPreset(string id, string name, string group,
        Func<(FrameworkElement View, Storyboard Animation)> createView, Func<string> createHtml)
    {
        Id = id;
        _name = name;
        _group = group;
        _createView = createView;
        _createHtml = createHtml;
    }

    public string Source => "preset:" + Id;

    public (FrameworkElement View, Storyboard Animation) Create() => _createView();

    public string CreateHtml() => _createHtml();
}

/// <summary>
/// Анимированные обои в духе macOS и Windows 11: плавающие цветовые пятна и слои «холмов».
/// Сцена задана в координатах 480×270 и растягивается на экран — мягким градиентам это не вредит.
/// </summary>
public static class WallpaperPresets
{
    private const double SceneWidth = 480;
    private const double SceneHeight = 270;

    /// <summary>Частота кадров превью: для медленного движения 24 кадров хватает с запасом.</summary>
    private const int FrameRate = 24;

    private sealed record Blob(Color Color, double Size, double X1, double X2, double DurationX,
        double Y1, double Y2, double DurationY, double Scale1, double Scale2, double DurationScale);

    private sealed record Hill(Color Color, IReadOnlyList<Point> Points, double Duration, bool IsReversed);

    public static readonly IReadOnlyList<WallpaperPreset> All = new[]
    {
        Waves("sonoma", "Sonoma", "В стиле macOS", "#9FD3C7", "#EEF3D8",
            "#8CC9A3", "#5DA984", "#3A8A6B", "#226B55", "#0F4A3D"),
        Blobs("ventura", "Ventura", "В стиле macOS", "#1B1F3B", "#0E1230",
            "#FF8A00", "#FFB347", "#E8505B", "#3A7BD5", "#FF5E3A"),
        Blobs("monterey", "Monterey", "В стиле macOS", "#2A0845", "#140A33",
            "#B721FF", "#21D4FD", "#FF4FD8", "#6A5CFF", "#F857A6"),
        Waves("bigsur", "Big Sur", "В стиле macOS", "#1E3C72", "#F2A65A",
            "#4C7CB5", "#355F94", "#244873", "#173355", "#0B1D35"),
        Blobs("bloom", "Bloom", "В стиле Windows 11", "#020B2A", "#00123F",
            "#0A84FF", "#3FA9F5", "#0050D8", "#67C4FF", "#1E5BFF"),
        Blobs("glow", "Glow", "В стиле Windows 11", "#05010F", "#0B0420",
            "#7B2FF7", "#2F80ED", "#C33AFF", "#4A00E0", "#00C6FF"),
        Blobs("aurora", "Полярное сияние", "Другие", "#020A12", "#031821",
            "#00F5A0", "#00D9F5", "#7B61FF", "#12C2E9", "#43E97B"),
        Waves("sunset", "Закат", "Другие", "#FF9A8B", "#FFD1A9",
            "#E07A8F", "#B35C8A", "#7E4F7F", "#553E6B", "#2E2A4F"),
    };

    public static WallpaperPreset? Find(string source) =>
        All.FirstOrDefault(preset => preset.Source == source);

    // ---------- Плавающие пятна (Ventura, Monterey, Bloom…) ----------

    private static WallpaperPreset Blobs(string id, string name, string group, string top, string bottom, params string[] colors)
    {
        var random = new Random(StableSeed(id));
        var blobs = colors.Concat(colors.Take(2)).Select(hex =>
        {
            double size = 200 + random.NextDouble() * 180;
            double x = random.NextDouble() * SceneWidth - size / 2;
            double y = random.NextDouble() * SceneHeight - size / 2;
            double dx = (random.NextDouble() - 0.5) * 260;
            double durationX = 20 + random.NextDouble() * 25;
            double dy = (random.NextDouble() - 0.5) * 180;
            double durationY = 18 + random.NextDouble() * 22;
            double scale = 0.75 + random.NextDouble() * 0.2;
            double durationScale = 14 + random.NextDouble() * 12;
            return new Blob(Parse(hex), size, x, x + dx, durationX, y, y + dy, durationY, scale, 2.05 - scale, durationScale);
        }).ToList();

        return new WallpaperPreset(id, name, group, () => BlobsView(top, bottom, blobs), () => BlobsHtml(top, bottom, blobs));
    }

    private static (FrameworkElement, Storyboard) BlobsView(string top, string bottom, List<Blob> blobs)
    {
        var canvas = CreateCanvas(top, bottom);
        var storyboard = CreateStoryboard();
        foreach (var blob in blobs)
        {
            var color = blob.Color;
            var ellipse = new Ellipse
            {
                Width = blob.Size,
                Height = blob.Size,
                Fill = new RadialGradientBrush(new GradientStopCollection
                {
                    new(Color.FromArgb(0xE0, color.R, color.G, color.B), 0),
                    new(Color.FromArgb(0x70, color.R, color.G, color.B), 0.45),
                    new(Color.FromArgb(0x00, color.R, color.G, color.B), 1),
                }),
                RenderTransformOrigin = new Point(0.5, 0.5),
                RenderTransform = new ScaleTransform(1, 1),
            };
            canvas.Children.Add(ellipse);
            Animate(storyboard, ellipse, "(Canvas.Left)", blob.X1, blob.X2, blob.DurationX);
            Animate(storyboard, ellipse, "(Canvas.Top)", blob.Y1, blob.Y2, blob.DurationY);
            Animate(storyboard, ellipse, "(UIElement.RenderTransform).(ScaleTransform.ScaleX)", blob.Scale1, blob.Scale2, blob.DurationScale);
            Animate(storyboard, ellipse, "(UIElement.RenderTransform).(ScaleTransform.ScaleY)", blob.Scale1, blob.Scale2, blob.DurationScale);
        }

        return (Wrap(canvas), storyboard);
    }

    private static string BlobsHtml(string top, string bottom, List<Blob> blobs)
    {
        var body = new StringBuilder();
        var keyframes = new StringBuilder();
        for (int index = 0; index < blobs.Count; index++)
        {
            var blob = blobs[index];
            var c = blob.Color;
            // Отдельные слои для движения по X, по Y и масштаба — как три независимые анимации в превью
            keyframes.Append(Css($"@keyframes x{index}{{from{{transform:translateX({X(blob.X1)})}}to{{transform:translateX({X(blob.X2)})}}}}"));
            keyframes.Append(Css($"@keyframes y{index}{{from{{transform:translateY({Y(blob.Y1)})}}to{{transform:translateY({Y(blob.Y2)})}}}}"));
            keyframes.Append(Css($"@keyframes s{index}{{from{{transform:scale({blob.Scale1})}}to{{transform:scale({blob.Scale2})}}}}"));
            body.Append(Css($"<div class=a style=\"animation:x{index} {blob.DurationX}s\"><div class=a style=\"animation:y{index} {blob.DurationY}s\">"))
                .Append(Css($"<div class=blob style=\"width:{X(blob.Size)};height:{X(blob.Size)};animation:s{index} {blob.DurationScale}s;"))
                .Append(Css($"background:radial-gradient(closest-side,rgba({c.R},{c.G},{c.B},.88),rgba({c.R},{c.G},{c.B},.44) 45%,rgba({c.R},{c.G},{c.B},0))\"></div></div></div>"));
        }

        return Page(top, bottom, """
            .a{position:absolute;left:0;top:0;animation-timing-function:ease-in-out!important;animation-iteration-count:infinite!important;animation-direction:alternate!important;will-change:transform}
            .blob{border-radius:50%;animation-timing-function:ease-in-out!important;animation-iteration-count:infinite!important;animation-direction:alternate!important;will-change:transform}
            """ + keyframes, body.ToString(), "");
    }

    // ---------- Слои холмов (Sonoma, Big Sur…) ----------

    private static WallpaperPreset Waves(string id, string name, string group, string sky, string horizon, params string[] layers)
    {
        var random = new Random(StableSeed(id));
        var hills = layers.Select((hex, index) =>
        {
            double baseY = 120 + index * 30;
            double amplitude = 10 + random.NextDouble() * 14;
            int waves = 1 + random.Next(3);
            double phase = random.NextDouble() * Math.PI * 2;
            return new Hill(Parse(hex), CreateWavePoints(baseY, amplitude, waves, phase), 140 - index * 18, index % 2 == 1);
        }).ToList();

        return new WallpaperPreset(id, name, group, () => WavesView(sky, horizon, hills), () => WavesHtml(sky, horizon, hills));
    }

    private static (FrameworkElement, Storyboard) WavesView(string sky, string horizon, List<Hill> hills)
    {
        var canvas = CreateCanvas(sky, horizon);
        var storyboard = CreateStoryboard();

        var sun = new Ellipse
        {
            Width = 260,
            Height = 260,
            Fill = new RadialGradientBrush(Color.FromArgb(0x90, 0xFF, 0xF4, 0xD6), Color.FromArgb(0x00, 0xFF, 0xF4, 0xD6)),
        };
        Canvas.SetLeft(sun, SceneWidth * 0.55);
        canvas.Children.Add(sun);
        Animate(storyboard, sun, "(Canvas.Top)", 10, 40, 40);

        foreach (var hill in hills)
        {
            var figure = new PathFigure { StartPoint = hill.Points[0], IsClosed = true };
            foreach (var point in hill.Points.Skip(1))
            {
                figure.Segments.Add(new LineSegment(point, true));
            }

            var geometry = new PathGeometry(new[] { figure });
            geometry.Freeze();
            var path = new Path { Data = geometry, Fill = new SolidColorBrush(hill.Color), RenderTransform = new TranslateTransform() };
            canvas.Children.Add(path);

            // Волна периодична на ширине сцены, поэтому сдвиг на эту ширину зацикливается без шва
            var drift = new DoubleAnimation(hill.IsReversed ? -SceneWidth : 0, hill.IsReversed ? 0 : -SceneWidth,
                TimeSpan.FromSeconds(hill.Duration)) { RepeatBehavior = RepeatBehavior.Forever };
            Storyboard.SetTarget(drift, path);
            Storyboard.SetTargetProperty(drift, new PropertyPath("(UIElement.RenderTransform).(TranslateTransform.X)"));
            storyboard.Children.Add(drift);
        }

        return (Wrap(canvas), storyboard);
    }

    private static string WavesHtml(string sky, string horizon, List<Hill> hills)
    {
        var svg = new StringBuilder();
        svg.Append($"<svg viewBox=\"0 0 {SceneWidth} {SceneHeight}\" preserveAspectRatio=\"xMidYMid slice\">");
        svg.Append("<defs><radialGradient id=sun><stop offset=0 stop-color=\"#FFF4D6\" stop-opacity=\".56\"/>"
            + "<stop offset=1 stop-color=\"#FFF4D6\" stop-opacity=\"0\"/></radialGradient></defs>");
        svg.Append(Css($"<circle cx=\"{SceneWidth * 0.55 + 130}\" cy=\"140\" r=\"130\" fill=\"url(#sun)\">"))
            .Append("<animate attributeName=\"cy\" values=\"140;170;140\" dur=\"80s\" repeatCount=\"indefinite\" calcMode=\"spline\" keySplines=\".45 0 .55 1;.45 0 .55 1\"/></circle>");

        foreach (var hill in hills)
        {
            var data = new StringBuilder("M");
            foreach (var point in hill.Points)
            {
                data.Append(Css($"{point.X:0.#},{point.Y:0.#} "));
            }

            string from = hill.IsReversed ? $"{-SceneWidth} 0" : "0 0";
            string to = hill.IsReversed ? "0 0" : $"{-SceneWidth} 0";
            svg.Append(Css($"<path fill=\"#{hill.Color.R:X2}{hill.Color.G:X2}{hill.Color.B:X2}\" d=\"{data}Z\">"))
                .Append(Css($"<animateTransform attributeName=\"transform\" type=\"translate\" from=\"{from}\" to=\"{to}\" dur=\"{hill.Duration}s\" repeatCount=\"indefinite\"/></path>"));
        }

        svg.Append("</svg>");
        return Page(sky, horizon, "svg{position:absolute;inset:0;width:100%;height:100%}", svg.ToString(),
            "document.querySelectorAll('svg').forEach(s=>p?s.pauseAnimations():s.unpauseAnimations());");
    }

    private static List<Point> CreateWavePoints(double baseY, double amplitude, int waves, double phase)
    {
        var points = new List<Point> { new(0, SceneHeight + 5) };
        for (double x = 0; x <= SceneWidth * 2; x += 6)
        {
            double y = baseY + amplitude * Math.Sin(2 * Math.PI * waves * x / SceneWidth + phase)
                + amplitude * 0.35 * Math.Sin(2 * Math.PI * waves * 2 * x / SceneWidth + phase * 1.7);
            points.Add(new Point(x, y));
        }

        points.Add(new Point(SceneWidth * 2, SceneHeight + 5));
        return points;
    }

    // ---------- Общее ----------

    /// <summary>Страница обоев: фон, сцена и функции паузы/звука, которые вызывает окно обоев.</summary>
    private static string Page(string top, string bottom, string css, string body, string pauseScript) => $$"""
        <!doctype html><html><head><meta charset="utf-8"><style>
        html,body{margin:0;width:100%;height:100%;overflow:hidden;background:linear-gradient(180deg,{{top}},{{bottom}})}
        body.paused *{animation-play-state:paused!important}
        {{css}}
        </style></head><body>{{body}}<script>
        function setPaused(p){document.body.classList.toggle('paused',p);{{pauseScript}}}
        function setMuted(m){}
        </script></body></html>
        """;

    private static string X(double sceneX) => Css($"{sceneX / SceneWidth * 100:0.###}vw");

    private static string Y(double sceneY) => Css($"{sceneY / SceneHeight * 100:0.###}vh");

    /// <summary>Числа в CSS/SVG — всегда с точкой, независимо от языка системы.</summary>
    private static string Css(FormattableString text) => text.ToString(CultureInfo.InvariantCulture);

    /// <summary>string.GetHashCode в .NET случаен для каждого запуска — сцена должна быть одинаковой всегда.</summary>
    private static int StableSeed(string text) => text.Aggregate(17, (hash, ch) => unchecked(hash * 31 + ch));

    private static Canvas CreateCanvas(string top, string bottom) => new()
    {
        Width = SceneWidth,
        Height = SceneHeight,
        ClipToBounds = true,
        Background = new LinearGradientBrush(Parse(top), Parse(bottom), 90),
    };

    private static Storyboard CreateStoryboard()
    {
        var storyboard = new Storyboard();
        Timeline.SetDesiredFrameRate(storyboard, FrameRate);
        return storyboard;
    }

    private static FrameworkElement Wrap(Canvas canvas) => new Viewbox
    {
        Stretch = Stretch.UniformToFill,
        Child = canvas,
    };

    private static void Animate(Storyboard storyboard, DependencyObject target, string property, double from, double to, double seconds)
    {
        var animation = new DoubleAnimation(from, to, TimeSpan.FromSeconds(seconds))
        {
            AutoReverse = true,
            RepeatBehavior = RepeatBehavior.Forever,
            EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut },
        };
        Storyboard.SetTarget(animation, target);
        Storyboard.SetTargetProperty(animation, new PropertyPath(property));
        storyboard.Children.Add(animation);
    }

    private static Color Parse(string hex) => (Color)ColorConverter.ConvertFromString(hex);
}
