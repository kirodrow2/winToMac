using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using WinToMac.Native;
using WinToMac.Settings;
using WinToMac.Localization;

namespace WinToMac.UI;

/// <summary>«О программе»: версия, инструкция и блок «Поддержать проект».</summary>
public sealed class AboutWindow : Window
{
    private static AboutWindow? _instance;

    /// <summary>Пересоздать окно, если открыто (смена языка).</summary>
    public static void Reopen()
    {
        if (_instance is { IsLoaded: true } window)
        {
            window.Close();
            _instance = null;
            ShowSingle();
        }
    }

    public static void ShowSingle()
    {
        if (_instance is { IsLoaded: true })
        {
            _instance.Activate();
            return;
        }

        _instance = new AboutWindow();
        _instance.Show();
        _instance.Activate();
    }

    private AboutWindow()
    {
        Title = L.T("О программе {0}", AppSettings.ProductName);
        Width = 460;
        SizeToContent = SizeToContent.Height;
        ResizeMode = ResizeMode.NoResize;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        FontSize = 13;
        FlowDirection = LanguagePicker.Direction;
        SetResourceReference(BackgroundProperty, Theme.MenuBackground);
        SetResourceReference(ForegroundProperty, Theme.MenuForeground);

        var root = new StackPanel { Margin = new Thickness(28, 24, 28, 24) };
        var icon = new Image
        {
            Width = 96,
            Height = 96,
            Source = LoadIcon(),
            HorizontalAlignment = HorizontalAlignment.Center,
        };
        RenderOptions.SetBitmapScalingMode(icon, BitmapScalingMode.HighQuality);
        root.Children.Add(icon);
        root.Children.Add(new TextBlock
        {
            Text = AppSettings.ProductName,
            FontSize = 22,
            FontWeight = FontWeights.SemiBold,
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 10, 0, 0),
        });
        root.Children.Add(Secondary(L.T("Версия {0}", ProjectLinks.Version), HorizontalAlignment.Center));
        root.Children.Add(new TextBlock
        {
            Text = L.T("Док, строка меню, сочетания клавиш и живые обои в стиле macOS для Windows.\nПрограмма бесплатная. Если она вам пригодилась — поддержите разработку, это помогает выпускать обновления."),
            TextWrapping = TextWrapping.Wrap,
            TextAlignment = TextAlignment.Center,
            Margin = new Thickness(0, 14, 0, 14),
        });

        var donations = ProjectLinks.ConfiguredDonations.ToList();
        if (donations.Count > 0)
        {
            root.Children.Add(new TextBlock
            {
                Text = L.T("♥  Поддержать проект"),
                FontWeight = FontWeights.SemiBold,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 0, 0, 8),
            });
            var buttons = new WrapPanel { HorizontalAlignment = HorizontalAlignment.Center };
            foreach (var link in donations)
            {
                buttons.Children.Add(CreateButton(link.Title, () => OpenDonation(link)));
            }

            root.Children.Add(buttons);
        }

        root.Children.Add(new TextBlock
        {
            Text = L.T("💡 Не хватает какой-то функции? Напишите мне в Telegram {0} — добавлю её в новой версии.", ProjectLinks.TelegramName),
            TextWrapping = TextWrapping.Wrap,
            TextAlignment = TextAlignment.Center,
            Margin = new Thickness(0, 16, 0, 8),
        });
        var telegram = new WrapPanel { HorizontalAlignment = HorizontalAlignment.Center };
        telegram.Children.Add(CreateButton(L.T("Написать в Telegram"), () => SystemActions.OpenUri(ProjectLinks.Telegram)));
        root.Children.Add(telegram);

        var links = new WrapPanel { HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 14, 0, 0) };
        links.Children.Add(CreateButton(L.T("Инструкция"), OpenInstructions));
        if (!string.IsNullOrWhiteSpace(ProjectLinks.Website))
        {
            links.Children.Add(CreateButton(L.T("Сайт проекта"), () => SystemActions.OpenUri(ProjectLinks.Website)));
        }

        root.Children.Add(links);
        var picker = LanguagePicker.Create();
        picker.HorizontalAlignment = HorizontalAlignment.Center;
        picker.Margin = new Thickness(0, 16, 0, 0);
        root.Children.Add(picker);
        Content = root;
    }

    /// <summary>Открыть инструкцию (папка Instructions рядом с exe), а без неё — сайт.</summary>
    public static void OpenInstructions()
    {
        if (File.Exists(ProjectLinks.InstructionsPath))
        {
            // Язык инструкции — тот же, что у программы (страница читает его из #код)
            SystemActions.OpenUri(new Uri(ProjectLinks.InstructionsPath).AbsoluteUri + "#" + L.Code);
        }
        else if (!string.IsNullOrWhiteSpace(ProjectLinks.Website))
        {
            SystemActions.OpenUri(ProjectLinks.Website);
        }
        else
        {
            MessageBox.Show(L.T("Инструкция не найдена: папка Instructions должна лежать рядом с winToMac.exe."),
                AppSettings.ProductName);
        }
    }

    private void OpenDonation(DonationLink link)
    {
        if (!string.IsNullOrWhiteSpace(link.Url))
        {
            SystemActions.OpenUri(link.Url);
            return;
        }

        Clipboard.SetText(link.Address!);
        MessageBox.Show(this, L.T("Адрес {0} скопирован:\n{1}\n\nСпасибо за поддержку!", link.Title, link.Address!), AppSettings.ProductName);
    }

    private static ImageSource? LoadIcon()
    {
        try
        {
            // Значок самой программы — тот же, что у exe
            return Environment.ProcessPath is { } exe ? ShellIcons.Get(exe, 256) : null;
        }
        catch
        {
            return null;
        }
    }

    private static TextBlock Secondary(string text, HorizontalAlignment alignment)
    {
        var block = new TextBlock { Text = text, FontSize = 12, HorizontalAlignment = alignment };
        block.SetResourceReference(TextBlock.ForegroundProperty, Theme.MenuSecondary);
        return block;
    }

    private static Button CreateButton(string text, Action action)
    {
        var button = new Button { Content = text, Padding = new Thickness(14, 5, 14, 5), Margin = new Thickness(4) };
        button.Click += (_, _) => action();
        return button;
    }
}
