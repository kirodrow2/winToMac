using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using WinToMac.Localization;
using WinToMac.Settings;

namespace WinToMac.UI;

/// <summary>Выбор языка интерфейса — один и тот же в настройках, галерее, «О программе» и меню.</summary>
public static class LanguagePicker
{
    /// <summary>Строка «🌐 Язык интерфейса: [список]».</summary>
    public static FrameworkElement Create()
    {
        var box = new ComboBox { MinWidth = 140, VerticalContentAlignment = VerticalAlignment.Center };
        foreach (var (_, name) in L.Languages)
        {
            box.Items.Add(name);
        }

        box.SelectedIndex = Array.FindIndex(L.Languages, language => language.Code == L.Code);
        box.SelectionChanged += (_, _) =>
        {
            if (box.SelectedIndex >= 0)
            {
                Apply(L.Languages[box.SelectedIndex].Code);
            }
        };

        var panel = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 4, 0, 4) };
        panel.Children.Add(new TextBlock
        {
            Text = "🌐  " + L.T("Язык интерфейса"),
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 10, 0),
        });
        panel.Children.Add(box);
        return panel;
    }

    /// <summary>Меню со списком языков (для строки меню).</summary>
    public static void ShowMenu(UIElement target)
    {
        var menu = new MacMenu().Header(L.T("Язык интерфейса"));
        foreach (var (code, name) in L.Languages)
        {
            menu.Add(name, () => Apply(code), isChecked: code == L.Code);
        }

        menu.Show(target, PlacementMode.Bottom);
    }

    /// <summary>Сохранить выбор и перерисовать открытые окна на новом языке.</summary>
    public static void Apply(string code)
    {
        if (code == L.Code)
        {
            return;
        }

        AppSettings.Current.UiLanguage = code;
        L.Load(code);
        // Окно, в котором выбрали язык, само будет пересоздано — поэтому после обработки события
        Application.Current.Dispatcher.BeginInvoke(() =>
        {
            AppSettings.Apply();
            App.Current.ReopenWindowsForLanguage();
        });
    }

    /// <summary>Направление текста: для арабского — справа налево.</summary>
    public static FlowDirection Direction => L.IsRightToLeft ? FlowDirection.RightToLeft : FlowDirection.LeftToRight;
}
