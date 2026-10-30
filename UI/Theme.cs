using System.Windows;
using System.Windows.Media;
using WinToMac.Settings;

namespace WinToMac.UI;

/// <summary>
/// Цвета светлой и тёмной темы. Кладутся в ресурсы приложения,
/// элементы берут их через DynamicResource/SetResourceReference и обновляются на лету.
/// </summary>
public static class Theme
{
    public const string BarForeground = "Bar.Foreground";
    public const string BarBackground = "Bar.Background";
    public const string BarHover = "Bar.Hover";
    public const string PanelBackground = "Panel.Background";
    public const string PanelBorder = "Panel.Border";
    public const string MenuBackground = "Menu.Background";
    public const string MenuForeground = "Menu.Foreground";
    public const string MenuSecondary = "Menu.Secondary";
    public const string MenuHover = "Menu.Hover";
    public const string MenuSeparator = "Menu.Separator";
    public const string LabelBackground = "Label.Background";

    public static readonly Color AccentColor = Color.FromRgb(0x0A, 0x64, 0xD8);

    public static bool IsDark => AppSettings.Current.Theme == ThemeKind.Dark;

    public static void Apply()
    {
        var settings = AppSettings.Current;
        var resources = Application.Current.Resources;
        byte dockAlpha = (byte)Math.Round(Math.Clamp(settings.DockBackgroundOpacity, 0, 1) * 255);
        // Без размытия подложка плотнее, иначе текст плохо читается на пёстрых обоях
        byte barAlpha = settings.TopBarBlur ? (byte)0xA0 : (byte)0xE0;

        if (IsDark)
        {
            resources[BarForeground] = Freeze(new SolidColorBrush(Colors.White));
            resources[BarBackground] = Freeze(new SolidColorBrush(Color.FromArgb(barAlpha, 0x1C, 0x1C, 0x1E)));
            resources[BarHover] = Freeze(new SolidColorBrush(Color.FromArgb(0x38, 0xFF, 0xFF, 0xFF)));
            resources[PanelBackground] = Freeze(new SolidColorBrush(Color.FromArgb(dockAlpha, 0x2A, 0x2A, 0x2E)));
            resources[PanelBorder] = Freeze(new SolidColorBrush(Color.FromArgb(0x40, 0xFF, 0xFF, 0xFF)));
            resources[MenuBackground] = Freeze(new SolidColorBrush(Color.FromArgb(0xF2, 0x2C, 0x2C, 0x2E)));
            resources[MenuForeground] = Freeze(new SolidColorBrush(Color.FromRgb(0xF5, 0xF5, 0xF7)));
            resources[MenuSecondary] = Freeze(new SolidColorBrush(Color.FromRgb(0x98, 0x98, 0x9D)));
            resources[MenuSeparator] = Freeze(new SolidColorBrush(Color.FromArgb(0x30, 0xFF, 0xFF, 0xFF)));
            resources[LabelBackground] = Freeze(new SolidColorBrush(Color.FromArgb(0xE6, 0x2C, 0x2C, 0x2E)));
        }
        else
        {
            resources[BarForeground] = Freeze(new SolidColorBrush(Color.FromRgb(0x1D, 0x1D, 0x1F)));
            resources[BarBackground] = Freeze(new SolidColorBrush(Color.FromArgb(barAlpha, 0xF2, 0xF2, 0xF4)));
            resources[BarHover] = Freeze(new SolidColorBrush(Color.FromArgb(0x26, 0x00, 0x00, 0x00)));
            resources[PanelBackground] = Freeze(new SolidColorBrush(Color.FromArgb(dockAlpha, 0xF6, 0xF6, 0xF8)));
            resources[PanelBorder] = Freeze(new SolidColorBrush(Color.FromArgb(0x70, 0xFF, 0xFF, 0xFF)));
            resources[MenuBackground] = Freeze(new SolidColorBrush(Color.FromArgb(0xF5, 0xF2, 0xF2, 0xF4)));
            resources[MenuForeground] = Freeze(new SolidColorBrush(Color.FromRgb(0x1D, 0x1D, 0x1F)));
            resources[MenuSecondary] = Freeze(new SolidColorBrush(Color.FromRgb(0x86, 0x86, 0x8B)));
            resources[MenuSeparator] = Freeze(new SolidColorBrush(Color.FromArgb(0x30, 0x00, 0x00, 0x00)));
            resources[LabelBackground] = Freeze(new SolidColorBrush(Color.FromArgb(0xEE, 0xF2, 0xF2, 0xF4)));
        }

        resources[MenuHover] = Freeze(new SolidColorBrush(AccentColor));
    }

    private static SolidColorBrush Freeze(SolidColorBrush brush)
    {
        brush.Freeze();
        return brush;
    }
}
