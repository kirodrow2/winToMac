using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Media.Animation;
using WinToMac.Native;
using WinToMac.Services;
using WinToMac.Settings;
using Microsoft.Win32;
using WinToMac.Localization;

namespace WinToMac.UI;

/// <summary>Галерея живых обоев: встроенные анимации, свои видео/GIF и заставки Apple Aerial.</summary>
public sealed class WallpaperGalleryWindow : Window
{
    private static readonly string[] VideoExtensions = { ".mp4", ".mov", ".m4v", ".wmv", ".avi", ".mkv", ".webm", ".gif" };

    private readonly StackPanel _content = new() { Margin = new Thickness(24, 16, 24, 24) };
    private readonly WrapPanel _presetTiles = new();
    private readonly WrapPanel _customTiles = new();
    private readonly StackPanel _aerialList = new();
    private readonly List<(Border Tile, string Source)> _tiles = new();
    private readonly List<(Storyboard Animation, FrameworkElement View)> _previews = new();
    private readonly Dictionary<string, CancellationTokenSource> _downloads = new();

    /// <summary>Запуск загрузки по ссылке видео — для кнопки «Скачать популярные».</summary>
    private readonly Dictionary<string, Action> _downloadStarters = new();
    private List<AerialVideo>? _aerials;

    // Нижняя панель: выбранное + «Применить»
    private readonly TextBlock _selectedText = new() { FontWeight = FontWeights.SemiBold, TextTrimming = TextTrimming.CharacterEllipsis };
    private readonly TextBlock _statusText = new() { FontSize = 12, TextTrimming = TextTrimming.CharacterEllipsis };
    private readonly Button _applyButton = new() { Content = L.T("Применить"), Padding = new Thickness(22, 6, 22, 6), IsDefault = true };
    private string? _selected = AppSettings.Current.WallpaperSource;

    private static AppSettings S => AppSettings.Current;

    public WallpaperGalleryWindow()
    {
        Title = L.T("Живые обои");
        Width = 940;
        Height = 760;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        FontSize = 13;
        FlowDirection = LanguagePicker.Direction;
        SetResourceReference(BackgroundProperty, Theme.MenuBackground);
        SetResourceReference(ForegroundProperty, Theme.MenuForeground);
        var layout = new DockPanel();
        var bottomBar = CreateBottomBar();
        DockPanel.SetDock(bottomBar, Dock.Bottom);
        layout.Children.Add(bottomBar);
        layout.Children.Add(new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Content = _content });
        Content = layout;

        Action statusChanged = () => Dispatcher.BeginInvoke(UpdateStatus);
        App.Current.WallpaperStatusChanged += statusChanged;
        Closed += (_, _) =>
        {
            App.Current.WallpaperStatusChanged -= statusChanged;
            foreach (var (animation, view) in _previews)
            {
                animation.Stop(view);
            }

            foreach (var download in _downloads.Values)
            {
                download.Cancel();
            }
        };

        Build();
    }

    private void Build()
    {
        var options = new WrapPanel { Margin = new Thickness(0, 0, 0, 8) };
        options.Children.Add(CreateCheck(L.T("Звук видео"), () => !S.WallpaperMuted, value => S.WallpaperMuted = !value));
        options.Children.Add(CreateCheck(L.T("Пауза, когда рабочий стол закрыт окнами"), () => S.WallpaperPauseWhenCovered, value => S.WallpaperPauseWhenCovered = value));
        _content.Children.Add(options);

        AddHeader(L.T("Анимированные"));
        _content.Children.Add(_presetTiles);
        foreach (var group in WallpaperPresets.All.GroupBy(preset => preset.Group))
        {
            foreach (var preset in group)
            {
                var (view, animation) = preset.Create();
                animation.Begin(view, isControllable: true);
                _previews.Add((animation, view));
                _presetTiles.Children.Add(CreateTile(preset.Source, view, preset.Name, preset.Group));
            }
        }

        AddHeader(L.T("Мои видео и GIF"));
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 8) };
        buttons.Children.Add(CreateButton(L.T("+ Добавить видео или GIF…"), AddFiles));
        buttons.Children.Add(CreateButton(L.T("Открыть папку обоев"), () =>
        {
            Directory.CreateDirectory(AppSettings.WallpaperFolder);
            Process.Start(new ProcessStartInfo("explorer.exe", AppSettings.WallpaperFolder) { UseShellExecute = true });
        }));
        _content.Children.Add(buttons);
        _content.Children.Add(Hint(L.T("Видео крутится по кругу. Файлы из папки обоев появляются здесь сами. Лучше всего подходят MP4 (H.264); для HEVC нужно расширение «HEVC Video Extensions» из Microsoft Store.")));
        _content.Children.Add(_customTiles);
        RebuildCustomTiles();

        AddHeader(L.T("Заставки Apple (Aerial)"));
        _content.Children.Add(Hint(L.T("Видео с серверов Apple, 1080p, каждое 100–600 МБ. Скачиваются в папку обоев.")));
        var aerialButtons = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 4, 0, 8) };
        aerialButtons.Children.Add(CreateButton(L.T("Показать список"), () => _ = LoadAerials(downloadPopular: false)));
        aerialButtons.Children.Add(CreateButton(L.T("Скачать популярные (6)"), () => _ = LoadAerials(downloadPopular: true)));
        _content.Children.Add(aerialButtons);
        _content.Children.Add(_aerialList);

        UpdateSelection();
    }

    // ---------- Плитки ----------

    private Border CreateTile(string source, FrameworkElement preview, string title, string? subtitle)
    {
        var previewHost = new Border
        {
            Width = 200,
            Height = 112,
            CornerRadius = new CornerRadius(8),
            ClipToBounds = true,
            Background = Brushes.Black,
            Child = preview,
        };
        // Скругление клипом: у Border.ClipToBounds углы остаются прямыми
        previewHost.Clip = new RectangleGeometry(new Rect(0, 0, 200, 112), 8, 8);

        var name = new TextBlock { Text = title, Margin = new Thickness(2, 6, 2, 0), TextTrimming = TextTrimming.CharacterEllipsis, MaxWidth = 200 };
        var panel = new StackPanel();
        panel.Children.Add(previewHost);
        panel.Children.Add(name);
        if (subtitle != null)
        {
            panel.Children.Add(Hint(subtitle, 11.5, new Thickness(2, 0, 2, 0)));
        }

        var tile = new Border
        {
            Padding = new Thickness(4),
            Margin = new Thickness(0, 0, 10, 10),
            CornerRadius = new CornerRadius(10),
            BorderThickness = new Thickness(2),
            BorderBrush = Brushes.Transparent,
            Background = Brushes.Transparent,
            Child = panel,
            Cursor = System.Windows.Input.Cursors.Hand,
        };
        // Клик — выбрать, двойной клик — сразу применить
        tile.MouseLeftButtonDown += (_, e) =>
        {
            if (e.ClickCount == 2)
            {
                Apply(source);
            }
            else
            {
                Select(source);
            }
        };
        _tiles.Add((tile, source));
        return tile;
    }

    private void RebuildCustomTiles()
    {
        _tiles.RemoveAll(item => _customTiles.Children.Contains(item.Tile));
        _customTiles.Children.Clear();

        var files = new List<string>();
        if (Directory.Exists(AppSettings.WallpaperFolder))
        {
            files.AddRange(Directory.EnumerateFiles(AppSettings.WallpaperFolder)
                .Where(path => VideoExtensions.Contains(Path.GetExtension(path).ToLowerInvariant())));
        }

        files.AddRange(S.CustomWallpapers.Where(File.Exists));
        foreach (string path in files.Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(Path.GetFileName))
        {
            var image = new Image { Stretch = Stretch.UniformToFill };
            LoadThumbnail(image, path);
            var tile = CreateTile(path, image, Path.GetFileNameWithoutExtension(path), Path.GetExtension(path).TrimStart('.').ToUpperInvariant());
            tile.MouseRightButtonUp += (_, e) =>
            {
                e.Handled = true;
                bool isInLibrary = Path.GetDirectoryName(path)?.Equals(AppSettings.WallpaperFolder, StringComparison.OrdinalIgnoreCase) == true;
                new MacMenu()
                    .Add(L.T("Установить"), () => Apply(path))
                    .Add(L.T("Показать в папке"), () => Process.Start(new ProcessStartInfo("explorer.exe", $"/select,\"{path}\"") { UseShellExecute = true }))
                    .Add(isInLibrary ? L.T("Удалить файл") : L.T("Убрать из списка"), () => Remove(path, isInLibrary))
                    .Show(tile, PlacementMode.MousePoint);
            };
            _customTiles.Children.Add(tile);
        }

        if (files.Count == 0)
        {
            _customTiles.Children.Add(Hint(L.T("Пока пусто — добавьте своё видео или GIF, или скачайте заставки Apple ниже.")));
        }

        UpdateSelection();
    }

    private static async void LoadThumbnail(Image image, string path)
    {
        image.Source = await ShellIcons.GetThumbnailAsync(path);
    }

    private void AddFiles()
    {
        var dialog = new OpenFileDialog
        {
            Title = L.T("Видео или GIF для обоев"),
            Multiselect = true,
            Filter = L.T("Видео и GIF|*.mp4;*.mov;*.m4v;*.wmv;*.avi;*.mkv;*.webm;*.gif|Все файлы|*.*"),
        };
        if (dialog.ShowDialog(this) != true)
        {
            return;
        }

        S.CustomWallpapers = S.CustomWallpapers.Concat(dialog.FileNames)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        AppSettings.Save();
        RebuildCustomTiles();
        Apply(dialog.FileNames[0]);
    }

    private void Remove(string path, bool deleteFile)
    {
        if (deleteFile)
        {
            if (MessageBox.Show(L.T("Удалить файл «{0}»?", Path.GetFileName(path)), L.T("Живые обои"), MessageBoxButton.OKCancel) != MessageBoxResult.OK)
            {
                return;
            }

            if (S.WallpaperSource == path)
            {
                S.WallpaperSource = null;
                AppSettings.Apply();
            }

            File.Delete(path);
        }

        S.CustomWallpapers = S.CustomWallpapers.Where(item => !item.Equals(path, StringComparison.OrdinalIgnoreCase)).ToList();
        AppSettings.Save();
        RebuildCustomTiles();
    }

    private void Select(string source)
    {
        _selected = source;
        UpdateSelection();
    }

    private void Apply(string source)
    {
        _selected = source;
        S.WallpaperSource = source;
        S.WallpaperEnabled = true;
        AppSettings.Apply();
        UpdateSelection();
    }

    /// <summary>Выбранная плитка — синяя рамка, установленная сейчас — серая.</summary>
    private void UpdateSelection()
    {
        foreach (var (tile, source) in _tiles)
        {
            if (source == _selected)
            {
                tile.SetResourceReference(Border.BorderBrushProperty, Theme.MenuHover);
            }
            else if (S.WallpaperEnabled && source == S.WallpaperSource)
            {
                tile.SetResourceReference(Border.BorderBrushProperty, Theme.MenuSecondary);
            }
            else
            {
                tile.BorderBrush = Brushes.Transparent;
            }
        }

        bool isApplied = S.WallpaperEnabled && _selected == S.WallpaperSource;
        _selectedText.Text = _selected == null
            ? L.T("Выберите обои выше")
            : L.T("Выбрано: ") + DisplayName(_selected) + (isApplied ? L.T("  ✓ установлено") : "");
        _applyButton.IsEnabled = _selected != null && !isApplied;
        UpdateStatus();
    }

    private void UpdateStatus()
    {
        _statusText.Text = L.T("Сейчас: ") + App.Current.WallpaperStatus;
    }

    private static string DisplayName(string source) =>
        WallpaperPresets.Find(source)?.Name ?? Path.GetFileNameWithoutExtension(source);

    private FrameworkElement CreateBottomBar()
    {
        _statusText.SetResourceReference(TextBlock.ForegroundProperty, Theme.MenuSecondary);
        _applyButton.Click += (_, _) =>
        {
            if (_selected != null)
            {
                Apply(_selected);
            }
        };

        var disableButton = new Button { Content = L.T("Выключить живые обои"), Padding = new Thickness(12, 6, 12, 6), Margin = new Thickness(0, 0, 8, 0) };
        disableButton.Click += (_, _) =>
        {
            S.WallpaperEnabled = false;
            AppSettings.Apply();
            UpdateSelection();
        };

        var buttons = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        buttons.Children.Add(disableButton);
        buttons.Children.Add(_applyButton);

        var texts = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 16, 0) };
        texts.Children.Add(_selectedText);
        texts.Children.Add(_statusText);
        texts.Children.Add(LanguagePicker.Create());

        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        Grid.SetColumn(buttons, 1);
        grid.Children.Add(texts);
        grid.Children.Add(buttons);

        var bar = new Border
        {
            Padding = new Thickness(24, 12, 24, 14),
            BorderThickness = new Thickness(0, 1, 0, 0),
            Child = grid,
        };
        bar.SetResourceReference(Border.BorderBrushProperty, Theme.MenuSeparator);
        return bar;
    }

    // ---------- Apple Aerial ----------

    private async Task LoadAerials(bool downloadPopular)
    {
        if (_aerials == null)
        {
            _aerialList.Children.Clear();
            _aerialList.Children.Add(Hint(L.T("Загружаю список с серверов Apple…")));
            try
            {
                _aerials = await AerialCatalog.LoadAsync();
            }
            catch (Exception exception)
            {
                _aerialList.Children.Clear();
                _aerialList.Children.Add(Hint(exception.Message));
                return;
            }

            _aerialList.Children.Clear();
            foreach (var video in _aerials)
            {
                _aerialList.Children.Add(CreateAerialRow(video));
            }
        }

        if (downloadPopular)
        {
            foreach (var video in AerialCatalog.PickPopular(_aerials, 6).Where(video => !video.IsDownloaded))
            {
                if (_downloadStarters.TryGetValue(video.Url, out var start))
                {
                    start();
                }
            }
        }
    }

    private FrameworkElement CreateAerialRow(AerialVideo video)
    {
        var grid = new Grid { Margin = new Thickness(0, 2, 0, 2) };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(160) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(130) });

        var name = new TextBlock { Text = video.Name + (video.IsHevc ? "  (HEVC)" : ""), VerticalAlignment = VerticalAlignment.Center };
        var progress = new ProgressBar { Height = 6, Margin = new Thickness(8, 0, 8, 0), Maximum = 1, Visibility = Visibility.Collapsed, VerticalAlignment = VerticalAlignment.Center };
        var button = new Button { Padding = new Thickness(10, 3, 10, 3) };
        Grid.SetColumn(progress, 1);
        Grid.SetColumn(button, 2);
        grid.Children.Add(name);
        grid.Children.Add(progress);
        grid.Children.Add(button);

        void Refresh()
        {
            bool downloading = _downloads.ContainsKey(video.Url);
            button.Content = downloading ? L.T("Отмена") : video.IsDownloaded ? L.T("Установить") : L.T("Скачать");
            progress.Visibility = downloading ? Visibility.Visible : Visibility.Collapsed;
        }

        async void Start()
        {
            if (_downloads.ContainsKey(video.Url) || video.IsDownloaded)
            {
                return;
            }

            var cancellation = new CancellationTokenSource();
            _downloads[video.Url] = cancellation;
            Refresh();
            try
            {
                await AerialCatalog.DownloadAsync(video, new Progress<double>(value => progress.Value = value), cancellation.Token);
                RebuildCustomTiles();
            }
            catch (OperationCanceledException)
            {
                // Отменили — недокачанный .part остаётся и перезапишется при следующей попытке
            }
            catch (Exception exception)
            {
                MessageBox.Show(this, L.T("Не удалось скачать «{0}»:\n{1}", video.Name, exception.Message), L.T("Живые обои"));
            }
            finally
            {
                _downloads.Remove(video.Url);
                Refresh();
            }
        }

        button.Click += (_, _) =>
        {
            if (_downloads.TryGetValue(video.Url, out var cancellation))
            {
                cancellation.Cancel();
            }
            else if (video.IsDownloaded)
            {
                Apply(video.LocalPath);
            }
            else
            {
                Start();
            }
        };
        _downloadStarters[video.Url] = Start;
        Refresh();
        return grid;
    }

    // ---------- Мелочи ----------

    private void AddHeader(string text)
    {
        _content.Children.Add(new TextBlock
        {
            Text = text,
            FontSize = 16,
            FontWeight = FontWeights.SemiBold,
            Margin = new Thickness(0, 16, 0, 8),
        });
    }

    private static TextBlock Hint(string text, double size = 12, Thickness? margin = null)
    {
        var hint = new TextBlock
        {
            Text = text,
            FontSize = size,
            TextWrapping = TextWrapping.Wrap,
            Margin = margin ?? new Thickness(0, 0, 0, 6),
        };
        hint.SetResourceReference(TextBlock.ForegroundProperty, Theme.MenuSecondary);
        return hint;
    }

    private static Button CreateButton(string text, Action action)
    {
        var button = new Button { Content = text, Padding = new Thickness(12, 5, 12, 5), Margin = new Thickness(0, 0, 8, 0) };
        button.Click += (_, _) => action();
        return button;
    }

    private CheckBox CreateCheck(string text, Func<bool> get, Action<bool> set)
    {
        var check = new CheckBox { Content = text, IsChecked = get(), Margin = new Thickness(0, 0, 20, 0) };
        check.SetResourceReference(ForegroundProperty, Theme.MenuForeground);
        check.Click += (_, _) =>
        {
            set(check.IsChecked == true);
            AppSettings.Apply();
            UpdateSelection();
        };
        return check;
    }
}
