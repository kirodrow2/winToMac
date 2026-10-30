using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using WinToMac.Native;
using WinToMac.Services;
using WinToMac.Settings;
using WinToMac.Localization;

namespace WinToMac.UI;

/// <summary>Список всех приложений с поиском и избранным (избранное = закреплённые в доке).</summary>
public partial class LauncherWindow : Window
{
    private sealed class Tile
    {
        public string Name { get; init; } = "";
        public string LaunchTarget { get; init; } = "";
        public string IconSource { get; init; } = "";
        public AppEntry? Entry { get; init; }
        public FrameworkElement View { get; set; } = null!;
        public TextBlock Star { get; set; } = null!;
    }

    private static readonly Brush StarBrush = new SolidColorBrush(Color.FromRgb(0xFF, 0xC4, 0x00));

    private List<Tile> _tiles = new();
    private bool _favoritesOnly;
    private bool _isLoaded;
    private bool _needsRebuild;

    public LauncherWindow()
    {
        InitializeComponent();
        FlowDirection = LanguagePicker.Direction;
        AllTabText.Text = L.T("Все");
        FavoritesTabText.Text = "★ " + L.T("Избранное");
        SearchPlaceholder.Text = L.T("Поиск");
        StatusText.Text = L.T("Звёздочка — добавить в избранное (оно же закрепляется в доке). Enter — открыть первое найденное.");
        Deactivated += (_, _) => Hide();
        PreviewKeyDown += OnPreviewKeyDown;
        AppSettings.Changed += () =>
        {
            // Полная пересборка сбросила бы прокрутку — сейчас только обновляем звёздочки,
            // а состав плиток (вручную закреплённые exe) обновится при следующем открытии
            _needsRebuild = true;
            if (_isLoaded)
            {
                UpdateStars();
                ApplyFilter();
            }
        };
        UpdateTabs();
    }

    public async void Open()
    {
        SearchBox.Text = "";
        Left = (SystemParameters.PrimaryScreenWidth - Width) / 2;
        Top = (SystemParameters.PrimaryScreenHeight - Height) / 2;
        Show();
        // Окно открывается из дока, который сам не берёт фокус, — помогаем Windows отдать его нам
        WindowEnumerator.Activate(new WindowInteropHelper(this).Handle);
        SearchBox.Focus();

        if (!_isLoaded)
        {
            EmptyText.Text = L.T("Загружаю список приложений…");
            EmptyText.Visibility = Visibility.Visible;
            await AppCatalog.GetAllAsync();
            _isLoaded = true;
            RebuildTiles();
        }
        else if (_needsRebuild)
        {
            RebuildTiles();
        }

        ApplyFilter();
    }

    // ---------- Плитки ----------

    private async void RebuildTiles()
    {
        var entries = await AppCatalog.GetAllAsync();
        var pinnedApps = AppSettings.Current.PinnedApps;
        var tiles = entries.Select(entry => new Tile
        {
            Name = entry.Name,
            LaunchTarget = entry.LaunchTarget,
            IconSource = entry.LaunchTarget,
            Entry = entry,
        }).ToList();

        // Закреплённые вручную exe/ярлыки, которых нет в общем списке, тоже показываем
        var knownTargets = new HashSet<string>(tiles.Select(tile => tile.LaunchTarget), StringComparer.OrdinalIgnoreCase);
        tiles.AddRange(pinnedApps
            .Where(pinned => !knownTargets.Contains(pinned.LaunchTarget))
            .Select(pinned => new Tile { Name = pinned.Name, LaunchTarget = pinned.LaunchTarget, IconSource = pinned.IconSource }));

        _needsRebuild = false;
        _tiles = tiles.OrderBy(tile => tile.Name, StringComparer.CurrentCultureIgnoreCase).ToList();
        TilesPanel.Children.Clear();
        foreach (var tile in _tiles)
        {
            tile.View = CreateTileView(tile);
            TilesPanel.Children.Add(tile.View);
        }

        UpdateStars();
        ApplyFilter();
    }

    private FrameworkElement CreateTileView(Tile tile)
    {
        var image = new Image { Width = 56, Height = 56, Margin = new Thickness(0, 4, 0, 6) };
        RenderOptions.SetBitmapScalingMode(image, BitmapScalingMode.HighQuality);
        LoadIcon(image, tile.IconSource);

        var name = new TextBlock
        {
            Text = tile.Name,
            FontSize = 12,
            TextAlignment = TextAlignment.Center,
            TextWrapping = TextWrapping.Wrap,
            TextTrimming = TextTrimming.CharacterEllipsis,
            MaxHeight = 32,
        };
        name.SetResourceReference(TextBlock.ForegroundProperty, Theme.MenuForeground);

        var star = new TextBlock
        {
            FontSize = 16,
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Top,
            Margin = new Thickness(0, 2, 6, 0),
            Cursor = Cursors.Hand,
            ToolTip = L.T("Избранное"),
        };
        star.MouseLeftButtonUp += (_, e) =>
        {
            e.Handled = true;
            ToggleFavorite(tile);
        };
        tile.Star = star;

        var content = new StackPanel { Margin = new Thickness(6, 8, 6, 8) };
        content.Children.Add(image);
        content.Children.Add(name);

        var grid = new Grid();
        grid.Children.Add(content);
        grid.Children.Add(star);

        var view = new Border
        {
            Width = 112,
            Height = 112,
            Margin = new Thickness(2),
            CornerRadius = new CornerRadius(10),
            Background = Brushes.Transparent,
            Child = grid,
            ToolTip = tile.Name,
        };
        view.MouseEnter += (_, _) =>
        {
            view.Background = new SolidColorBrush(Color.FromArgb(0x26, 0x80, 0x80, 0x80));
            UpdateStar(tile, hovered: true);
        };
        view.MouseLeave += (_, _) =>
        {
            view.Background = Brushes.Transparent;
            UpdateStar(tile, hovered: false);
        };
        view.MouseLeftButtonUp += (_, _) => Launch(tile);
        view.MouseRightButtonUp += (_, e) =>
        {
            e.Handled = true;
            bool isFavorite = FindPinned(tile) != null;
            new MacMenu()
                .Header(tile.Name)
                .Add(L.T("Открыть"), () => Launch(tile))
                .Add(isFavorite ? L.T("Убрать из избранного") : L.T("Добавить в избранное"), () => ToggleFavorite(tile))
                .Show(view, PlacementMode.MousePoint);
        };
        return view;
    }

    private static async void LoadIcon(Image image, string source)
    {
        image.Source = await ShellIcons.GetAsync(source, 96);
    }

    private PinnedApp? FindPinned(Tile tile) => AppSettings.Current.PinnedApps.FirstOrDefault(pinned =>
        string.Equals(pinned.LaunchTarget, tile.LaunchTarget, StringComparison.OrdinalIgnoreCase));

    private void UpdateStars()
    {
        foreach (var tile in _tiles)
        {
            UpdateStar(tile, hovered: false);
        }
    }

    private void UpdateStar(Tile tile, bool hovered)
    {
        bool isFavorite = FindPinned(tile) != null;
        tile.Star.Text = isFavorite ? "★" : "☆";
        tile.Star.Visibility = isFavorite || hovered ? Visibility.Visible : Visibility.Hidden;
        if (isFavorite)
        {
            tile.Star.Foreground = StarBrush;
        }
        else
        {
            tile.Star.SetResourceReference(TextBlock.ForegroundProperty, Theme.MenuSecondary);
        }
    }

    private void ToggleFavorite(Tile tile)
    {
        var pinnedApps = AppSettings.Current.PinnedApps;
        var pinned = FindPinned(tile);
        if (pinned != null)
        {
            pinnedApps.Remove(pinned);
        }
        else if (tile.Entry != null)
        {
            pinnedApps.Add(tile.Entry.ToPinned());
        }

        AppSettings.Apply();
    }

    private void Launch(Tile tile)
    {
        Hide();
        AppCatalog.Launch(tile.LaunchTarget);
    }

    // ---------- Фильтр ----------

    private void ApplyFilter()
    {
        string query = SearchBox.Text.Trim();
        SearchPlaceholder.Visibility = query.Length == 0 ? Visibility.Visible : Visibility.Collapsed;

        int visibleCount = 0;
        foreach (var tile in _tiles)
        {
            bool visible = (!_favoritesOnly || FindPinned(tile) != null)
                && (query.Length == 0 || tile.Name.Contains(query, StringComparison.CurrentCultureIgnoreCase));
            tile.View.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
            visibleCount += visible ? 1 : 0;
        }

        if (!_isLoaded)
        {
            return;
        }

        EmptyText.Text = _favoritesOnly && query.Length == 0
            ? L.T("В избранном пока пусто — нажмите ☆ у любого приложения")
            : L.T("Ничего не найдено");
        EmptyText.Visibility = visibleCount == 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private void OnSearchChanged(object sender, TextChangedEventArgs e) => ApplyFilter();

    private void OnAllTabClick(object sender, MouseButtonEventArgs e) => SetFavoritesOnly(false);

    private void OnFavoritesTabClick(object sender, MouseButtonEventArgs e) => SetFavoritesOnly(true);

    private void SetFavoritesOnly(bool favoritesOnly)
    {
        _favoritesOnly = favoritesOnly;
        UpdateTabs();
        ApplyFilter();
    }

    private void UpdateTabs()
    {
        var selected = _favoritesOnly ? FavoritesTab : AllTab;
        var other = _favoritesOnly ? AllTab : FavoritesTab;
        selected.SetResourceReference(Border.BackgroundProperty, Theme.MenuSeparator);
        other.Background = Brushes.Transparent;
    }

    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.Escape:
                Hide();
                e.Handled = true;
                break;
            case Key.Enter:
                var first = _tiles.FirstOrDefault(tile => tile.View.Visibility == Visibility.Visible);
                if (first != null)
                {
                    Launch(first);
                }

                e.Handled = true;
                break;
        }
    }
}
