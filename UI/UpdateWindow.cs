using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using WinToMac.Localization;
using WinToMac.Services;
using WinToMac.Settings;

namespace WinToMac.UI;

/// <summary>«Доступна новая версия»: что нового, кнопка «Обновить» с прогрессом загрузки.</summary>
public sealed class UpdateWindow : Window
{
    private static UpdateWindow? _instance;
    private static DispatcherTimer? _timer;

    private readonly UpdateInfo _update;
    private readonly ProgressBar _progress;
    private readonly TextBlock _status;
    private readonly StackPanel _buttons;

    /// <summary>Тихая проверка: через минуту после запуска, затем раз в 12 часов.</summary>
    public static void StartBackgroundChecks()
    {
        _timer = new DispatcherTimer { Interval = TimeSpan.FromMinutes(1) };
        _timer.Tick += async (_, _) =>
        {
            _timer.Interval = TimeSpan.FromHours(12);
            if (AppSettings.Current.CheckForUpdates)
            {
                await CheckAsync(manual: false);
            }
        };
        _timer.Start();
    }

    /// <summary>manual — пользователь сам нажал «Проверить обновления»: сообщаем и об отсутствии версии, и об ошибке.</summary>
    public static async Task CheckAsync(bool manual)
    {
        UpdateInfo? update;
        try
        {
            update = await Updater.CheckAsync();
        }
        catch (Exception exception)
        {
            if (manual)
            {
                MessageBox.Show(L.T("Не удалось проверить обновления: {0}", exception.Message), AppSettings.ProductName);
            }

            return;
        }

        if (update == null)
        {
            if (manual)
            {
                MessageBox.Show(L.T("У вас последняя версия {0}.", Updater.CurrentVersion), AppSettings.ProductName);
            }

            return;
        }

        if (!manual && AppSettings.Current.SkippedUpdateVersion == update.Version.ToString())
        {
            return;
        }

        if (_instance is { IsLoaded: true })
        {
            _instance.Activate();
            return;
        }

        _instance = new UpdateWindow(update);
        _instance.Show();
        _instance.Activate();
    }

    private UpdateWindow(UpdateInfo update)
    {
        _update = update;
        Title = L.T("Обновление {0}", AppSettings.ProductName);
        Width = 480;
        SizeToContent = SizeToContent.Height;
        ResizeMode = ResizeMode.NoResize;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        FontSize = 13;
        FlowDirection = LanguagePicker.Direction;
        SetResourceReference(BackgroundProperty, Theme.MenuBackground);
        SetResourceReference(ForegroundProperty, Theme.MenuForeground);

        var root = new StackPanel { Margin = new Thickness(24, 20, 24, 20) };
        root.Children.Add(new TextBlock
        {
            Text = L.T("Доступна новая версия {0} (у вас {1})", update.Version, Updater.CurrentVersion),
            FontSize = 16,
            FontWeight = FontWeights.SemiBold,
            TextWrapping = TextWrapping.Wrap,
        });
        if (update.Notes.Length > 0)
        {
            root.Children.Add(new ScrollViewer
            {
                MaxHeight = 220,
                Margin = new Thickness(0, 10, 0, 0),
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                Content = new TextBlock { Text = update.Notes, TextWrapping = TextWrapping.Wrap, Opacity = 0.85 },
            });
        }

        _progress = new ProgressBar { Height = 6, Margin = new Thickness(0, 14, 0, 0), Maximum = 1, Visibility = Visibility.Collapsed };
        root.Children.Add(_progress);
        _status = new TextBlock
        {
            Text = L.T("Обновление скачается ({0} МБ) и установится само, программа перезапустится.", Math.Round(update.Size / 1048576.0)),
            TextWrapping = TextWrapping.Wrap,
            Opacity = 0.75,
            Margin = new Thickness(0, 10, 0, 0),
        };
        root.Children.Add(_status);

        _buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 16, 0, 0) };
        _buttons.Children.Add(CreateButton(L.T("Пропустить эту версию"), () =>
        {
            AppSettings.Current.SkippedUpdateVersion = _update.Version.ToString();
            AppSettings.Save();
            Close();
        }));
        _buttons.Children.Add(CreateButton(L.T("Позже"), Close));
        var install = CreateButton(L.T("Обновить"), Install);
        install.IsDefault = true;
        _buttons.Children.Add(install);
        root.Children.Add(_buttons);
        Content = root;
    }

    private async void Install()
    {
        _buttons.IsEnabled = false;
        _progress.Visibility = Visibility.Visible;
        _status.Text = L.T("Скачивание…");
        try
        {
            await Updater.InstallAsync(_update, new Progress<double>(value => _progress.Value = value));
            // Новая версия сама попросит эту копию выйти
            _status.Text = L.T("Запуск новой версии…");
        }
        catch (Exception exception)
        {
            _status.Text = L.T("Не удалось обновить: {0}", exception.Message);
            _progress.Visibility = Visibility.Collapsed;
            _buttons.IsEnabled = true;
        }
    }

    private static Button CreateButton(string text, Action click)
    {
        var button = new Button { Content = text, Padding = new Thickness(14, 5, 14, 5), Margin = new Thickness(8, 0, 0, 0) };
        button.Click += (_, _) => click();
        return button;
    }
}
