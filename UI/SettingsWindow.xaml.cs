using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using WinToMac.Native;
using WinToMac.Services;
using WinToMac.Settings;
using Microsoft.Win32;
using WinToMac.Localization;

namespace WinToMac.UI;

/// <summary>Настройки применяются сразу, без кнопки «Применить».</summary>
public partial class SettingsWindow : Window
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string RunValueName = AppSettings.ProductName;

    /// <summary>Ползунки меняют значение много раз в секунду — пересобираем док не чаще раза в 150 мс.</summary>
    private readonly DispatcherTimer _applyTimer = new() { Interval = TimeSpan.FromMilliseconds(150) };

    private AppSettings S => AppSettings.Current;

    public SettingsWindow()
    {
        InitializeComponent();
        Title = L.T("Настройки winToMac");
        FlowDirection = LanguagePicker.Direction;
        OpenFolderButton.Content = L.T("Открыть папку настроек");
        ResetButton.Content = L.T("Сбросить");
        DoneButton.Content = L.T("Готово");
        _applyTimer.Tick += (_, _) =>
        {
            _applyTimer.Stop();
            AppSettings.Apply();
        };
        Closed += (_, _) =>
        {
            if (_applyTimer.IsEnabled)
            {
                _applyTimer.Stop();
                AppSettings.Apply();
            }
        };
        Build();
    }

    private void Build()
    {
        SettingsPanel.Children.Clear();
        SettingsPanel.Children.Add(LanguagePicker.Create());

        AddSection(L.T("Док"));
        AddSlider(L.T("Размер иконок"), 32, 128, () => S.DockIconSize, v => S.DockIconSize = v, "0 px");
        AddCheck(L.T("Увеличение при наведении"), () => S.DockMagnification, v => S.DockMagnification = v);
        AddSlider(L.T("Размер при увеличении"), 48, 200, () => S.DockMagnifiedSize, v => S.DockMagnifiedSize = v, "0 px");
        AddSlider(L.T("Непрозрачность фона"), 0, 1, () => S.DockBackgroundOpacity, v => S.DockBackgroundOpacity = v, "0%");
        AddCheck(L.T("Автоматически скрывать"), () => S.DockAutoHide, v => S.DockAutoHide = v);
        AddCheck(L.T("Не давать окнам заходить под док"), () => S.DockReserveSpace, v => S.DockReserveSpace = v);
        AddCheck(L.T("Показывать точку под запущенными"), () => S.DockShowRunningIndicator, v => S.DockShowRunningIndicator = v);
        AddCheck(L.T("Показывать незакреплённые запущенные"), () => S.DockShowUnpinnedRunning, v => S.DockShowUnpinnedRunning = v);
        AddCheck(L.T("Кнопка «Свернуть все окна»"), () => S.DockShowDesktopButton, v => S.DockShowDesktopButton = v);
        AddCheck(L.T("Показывать корзину"), () => S.DockShowTrash, v => S.DockShowTrash = v);

        AddSection(L.T("Верхняя панель"));
        AddSlider(L.T("Высота"), 22, 44, () => S.TopBarHeight, v => S.TopBarHeight = v, "0 px");
        AddSlider(L.T("Размер шрифта"), 11, 18, () => S.TopBarFontSize, v => S.TopBarFontSize = v, "0");
        AddCheck(L.T("Размытие фона"), () => S.TopBarBlur, v => S.TopBarBlur = v);
        AddCheck(L.T("Показывать дату"), () => S.ClockShowDate, v => S.ClockShowDate = v);
        AddCheck(L.T("Показывать секунды"), () => S.ClockShowSeconds, v => S.ClockShowSeconds = v);
        AddCheck(L.T("Процент заряда батареи"), () => S.ShowBatteryPercent, v => S.ShowBatteryPercent = v);
        AddCheck(L.T("Значки фоновых программ (трей)"), () => S.ShowTrayIcons, v => S.ShowTrayIcons = v);
        AddTrayIconList();

        AddSection(L.T("Клавиатура как на Mac"));
        AddCheck(L.T("Включить сочетания клавиш как на Mac"), () => S.MacKeyboard, v => S.MacKeyboard = v);
        AddKeyboardStatus();
        AddChoice(L.T("⌘ на клавиатуре этого ПК"), new[]
        {
            (CommandKey.Alt, "Alt"), (CommandKey.Win, "Win"),
        }, () => S.LocalCommandKey, v => S.LocalCommandKey = v);
        AddChoice(L.T("⌘ с Mac через Moonlight"), new[]
        {
            (CommandKey.Win, "Win"), (CommandKey.Alt, "Alt"), (CommandKey.Ctrl, L.T("уже Ctrl")),
        }, () => S.RemoteCommandKey, v => S.RemoteCommandKey = v);
        AddCheck(L.T("⌘Пробел открывает «Все приложения»"), () => S.CommandSpaceOpensLauncher, v => S.CommandSpaceOpensLauncher = v);
        AddCheck(L.T("⌥←/→ и ⌥⌫ — по словам"), () => S.OptionWordNavigation, v => S.OptionWordNavigation = v);
        AddCheck(L.T("Не трогать клавиши в полноэкранных играх"), () => S.KeyboardSkipFullScreen, v => S.KeyboardSkipFullScreen = v);
        AddExcludedApps();
        AddShortcutHelp();

        AddSection(L.T("Смена языка"));
        AddChoice("Caps Lock", new[]
        {
            (CapsLockMode.Normal, L.T("Обычный (заглавные)")),
            (CapsLockMode.LanguageHoldForCaps, L.T("Смена языка, удержание — заглавные")),
            (CapsLockMode.LanguageOnly, L.T("Только смена языка")),
        }, GetCapsLockMode, SetCapsLockMode);
        AddLanguageKey();
        AddSystemCapsLockRemap();
        AddChoice(L.T("Как переключать"), new[]
        {
            (LanguageSwitchMethod.WinSpace, L.T("Win+Пробел")),
            (LanguageSwitchMethod.Shortcut, L.T("Своё сочетание")),
            (LanguageSwitchMethod.Message, L.T("Тихо")),
        }, () => S.LanguageSwitch, v => S.LanguageSwitch = v);
        AddShortcutRow(L.T("Своё сочетание"), L.T("Сочетание смены языка в Windows"),
            () => S.LanguageShortcut, v => S.LanguageShortcut = v);

        AddSection(L.T("Переназначение клавиш"));
        AddMappings();

        AddSection(L.T("Общее"));
        var wallpapersButton = new Button
        {
            Content = L.T("Живые обои…"),
            HorizontalAlignment = HorizontalAlignment.Left,
            Padding = new Thickness(12, 5, 12, 5),
            Margin = new Thickness(0, 2, 0, 8),
        };
        wallpapersButton.Click += (_, _) => App.Current.ShowWallpapers();
        SettingsPanel.Children.Add(wallpapersButton);
        var aboutButton = new Button
        {
            Content = L.T("О программе и поддержка ♥"),
            HorizontalAlignment = HorizontalAlignment.Left,
            Padding = new Thickness(12, 5, 12, 5),
            Margin = new Thickness(0, 0, 0, 8),
        };
        aboutButton.Click += (_, _) => AboutWindow.ShowSingle();
        SettingsPanel.Children.Add(aboutButton);
        var ideaButton = new Button
        {
            Content = L.T("💡 Предложить функцию (Telegram)"),
            HorizontalAlignment = HorizontalAlignment.Left,
            Padding = new Thickness(12, 5, 12, 5),
            Margin = new Thickness(0, 0, 0, 8),
        };
        ideaButton.Click += (_, _) => SystemActions.OpenUri(ProjectLinks.Telegram);
        SettingsPanel.Children.Add(ideaButton);
        AddThemeChoice();
        AddCheck(L.T("Скрывать панель задач Windows"), () => S.HideWindowsTaskbar, v => S.HideWindowsTaskbar = v);
        AddAutostart();
        AddCheck(L.T("Проверять обновления автоматически"), () => S.CheckForUpdates, v => S.CheckForUpdates = v);
    }

    private void AddSection(string title)
    {
        SettingsPanel.Children.Add(new TextBlock
        {
            Text = title,
            FontSize = 15,
            FontWeight = FontWeights.SemiBold,
            Margin = new Thickness(0, 16, 0, 8),
        });
    }

    private void AddSlider(string label, double min, double max, Func<double> get, Action<double> set, string format)
    {
        bool isPercent = format.EndsWith('%');
        var value = new TextBlock { Width = 64, TextAlignment = TextAlignment.Right, VerticalAlignment = VerticalAlignment.Center };
        var slider = new Slider
        {
            Minimum = min,
            Maximum = max,
            Value = get(),
            VerticalAlignment = VerticalAlignment.Center,
            IsSnapToTickEnabled = !isPercent,
            TickFrequency = 1,
        };

        void ShowValue() => value.Text = isPercent ? $"{slider.Value:P0}" : slider.Value.ToString(format);
        ShowValue();
        slider.ValueChanged += (_, _) =>
        {
            set(slider.Value);
            ShowValue();
            ScheduleApply();
        };

        var grid = new Grid { Margin = new Thickness(0, 4, 0, 4) };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(190) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var text = new TextBlock { Text = label, VerticalAlignment = VerticalAlignment.Center };
        Grid.SetColumn(slider, 1);
        Grid.SetColumn(value, 2);
        grid.Children.Add(text);
        grid.Children.Add(slider);
        grid.Children.Add(value);
        SettingsPanel.Children.Add(grid);
    }

    private void AddCheck(string label, Func<bool> get, Action<bool> set)
    {
        var check = new CheckBox
        {
            Content = label,
            IsChecked = get(),
            Margin = new Thickness(0, 5, 0, 5),
        };
        check.SetResourceReference(ForegroundProperty, Theme.MenuForeground);
        check.Click += (_, _) =>
        {
            set(check.IsChecked == true);
            ScheduleApply();
        };
        SettingsPanel.Children.Add(check);
    }

    /// <summary>Какие значки трея показывать — по одному флажку на каждую запущенную программу.</summary>
    private void AddTrayIconList()
    {
        var icons = TrayHost.GetIcons().Where(icon => icon.Identifier != null).ToList();
        if (icons.Count == 0)
        {
            return;
        }

        var panel = new StackPanel { Margin = new Thickness(24, 0, 0, 4) };
        foreach (var icon in icons)
        {
            string identifier = icon.Identifier!;
            var row = new StackPanel { Orientation = Orientation.Horizontal };
            var image = new Image { Source = icon.Icon, Width = 16, Height = 16, Margin = new Thickness(0, 0, 6, 0) };
            string title = string.IsNullOrWhiteSpace(icon.Title) ? identifier : icon.Title.Split('\n')[0];
            row.Children.Add(image);
            row.Children.Add(new TextBlock { Text = title.Length > 50 ? title[..47] + "…" : title });

            var check = new CheckBox
            {
                Content = row,
                IsChecked = !S.HiddenTrayIcons.Contains(identifier),
                Margin = new Thickness(0, 3, 0, 3),
            };
            check.SetResourceReference(ForegroundProperty, Theme.MenuForeground);
            check.Click += (_, _) =>
            {
                S.HiddenTrayIcons.Remove(identifier);
                if (check.IsChecked != true)
                {
                    S.HiddenTrayIcons.Add(identifier);
                }

                ScheduleApply();
            };
            panel.Children.Add(check);
        }

        SettingsPanel.Children.Add(panel);
    }

    /// <summary>Кнопка с сочетанием: по клику — запись нового.</summary>
    private Button CreateShortcutButton(string recordTitle, Func<KeyShortcut> get, Action<KeyShortcut> set, double width = 170)
    {
        var button = new Button
        {
            Content = get().ToString(),
            Width = width,
            Padding = new Thickness(6, 4, 6, 4),
            ToolTip = L.T("Нажмите, чтобы записать"),
        };
        button.Click += (_, _) =>
        {
            var recorded = ShortcutRecorderWindow.Record(this, recordTitle);
            if (recorded == null)
            {
                return;
            }

            set(recorded);
            button.Content = recorded.ToString();
            ScheduleApply();
        };
        return button;
    }

    private void AddShortcutRow(string label, string recordTitle, Func<KeyShortcut> get, Action<KeyShortcut> set)
    {
        var row = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 4, 0, 6) };
        row.Children.Add(new TextBlock { Text = label, Width = 190, VerticalAlignment = VerticalAlignment.Center });
        row.Children.Add(CreateShortcutButton(recordTitle, get, set));
        SettingsPanel.Children.Add(row);
    }

    /// <summary>Клавиша смены языка + подсказка, что нашлось в реестре.</summary>
    private void AddLanguageKey()
    {
        var row = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 4, 0, 2) };
        row.Children.Add(new TextBlock { Text = L.T("Клавиша"), Width = 190, VerticalAlignment = VerticalAlignment.Center });
        var keyButton = CreateShortcutButton(L.T("Клавиша смены языка"),
            () => new KeyShortcut { Key = KeyRemapper.GetLanguageKey(S) },
            recorded => S.LanguageKey = recorded.Key);
        row.Children.Add(keyButton);

        var autoButton = new Button { Content = L.T("Авто"), Padding = new Thickness(10, 4, 10, 4), Margin = new Thickness(8, 0, 0, 0) };
        autoButton.Click += (_, _) =>
        {
            S.LanguageKey = null;
            keyButton.Content = new KeyShortcut { Key = KeyRemapper.GetLanguageKey(S) }.ToString();
            ScheduleApply();
        };
        row.Children.Add(autoButton);
        SettingsPanel.Children.Add(row);

        string note = ScancodeMap.CapsLockReplacement switch
        {
            null => L.T("В реестре Caps Lock не переназначен."),
            0 => L.T("В реестре Caps Lock отключён — запишите клавишу, которой хотите менять язык."),
            ushort vk => L.T("В реестре Caps Lock переназначен на {0} — «Авто» использует её.", KeyNames.Get(vk)),
        };
        var hint = new TextBlock { Text = note, FontSize = 12, Margin = new Thickness(190, 0, 0, 6), TextWrapping = TextWrapping.Wrap };
        hint.SetResourceReference(TextBlock.ForegroundProperty, Theme.MenuSecondary);
        SettingsPanel.Children.Add(hint);
    }

    private enum CapsLockMode
    {
        Normal,
        LanguageHoldForCaps,
        LanguageOnly,
    }

    private CapsLockMode GetCapsLockMode() => !S.CapsLockSwitchesLanguage ? CapsLockMode.Normal
        : S.LanguageKeyHoldForCaps ? CapsLockMode.LanguageHoldForCaps
        : CapsLockMode.LanguageOnly;

    private void SetCapsLockMode(CapsLockMode mode)
    {
        S.CapsLockSwitchesLanguage = mode != CapsLockMode.Normal;
        S.LanguageKeyHoldForCaps = mode == CapsLockMode.LanguageHoldForCaps;
    }

    /// <summary>
    /// Переназначение Caps Lock на уровне Windows (как делают через реестр вручную): Caps Lock становится F13,
    /// заглавные не включаются даже без winToMac. Нужны права администратора и перезагрузка.
    /// </summary>
    private void AddSystemCapsLockRemap()
    {
        var panel = new StackPanel { Margin = new Thickness(0, 6, 0, 8) };
        var title = new TextBlock { Text = L.T("Системное переназначение Caps Lock"), FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 0, 0, 4) };
        var hint = new TextBlock
        {
            Text = L.T("Caps Lock превращается в клавишу F13 на уровне Windows: заглавные не включатся случайно, даже если winToMac закрыт, а winToMac будет менять язык по F13. Нужны права администратора и перезагрузка компьютера."),
            FontSize = 12,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 0, 0, 6),
        };
        hint.SetResourceReference(TextBlock.ForegroundProperty, Theme.MenuSecondary);
        var status = new TextBlock { FontSize = 12, Margin = new Thickness(0, 0, 0, 6), TextWrapping = TextWrapping.Wrap };

        void UpdateStatus()
        {
            ScancodeMap.Refresh();
            status.Text = ScancodeMap.CapsLockReplacement switch
            {
                null => L.T("Сейчас: Caps Lock не переназначен."),
                0 => L.T("Сейчас: Caps Lock отключён в реестре."),
                ushort vk => L.T("Сейчас: Caps Lock → {0}.", KeyNames.Get(vk)),
            };
        }

        var toF13 = new Button { Content = L.T("Сделать Caps Lock клавишей F13"), Padding = new Thickness(12, 5, 12, 5), Margin = new Thickness(0, 0, 8, 0) };
        var restore = new Button { Content = L.T("Вернуть обычный Caps Lock"), Padding = new Thickness(12, 5, 12, 5) };
        toF13.Click += (_, _) =>
        {
            if (RunElevated(ScancodeMap.CapsToF13Argument))
            {
                // Клавишу смены языка оставляем на «Авто» — после перезагрузки это будет F13
                S.LanguageKey = null;
                S.CapsLockSwitchesLanguage = true;
                ScheduleApply();
            }

            UpdateStatus();
        };
        restore.Click += (_, _) =>
        {
            RunElevated(ScancodeMap.CapsRestoreArgument);
            UpdateStatus();
        };

        var buttons = new WrapPanel();
        buttons.Children.Add(toF13);
        buttons.Children.Add(restore);
        UpdateStatus();

        panel.Children.Add(title);
        panel.Children.Add(hint);
        panel.Children.Add(status);
        panel.Children.Add(buttons);
        SettingsPanel.Children.Add(panel);
    }

    /// <summary>Запустить копию winToMac от администратора с одним действием и дождаться её.</summary>
    private bool RunElevated(string argument)
    {
        try
        {
            using var process = Process.Start(new ProcessStartInfo(Environment.ProcessPath!, argument)
            {
                UseShellExecute = true,
                Verb = "runas",
            });
            process?.WaitForExit();
            if (process?.ExitCode != 0)
            {
                MessageBox.Show(this, L.T("Не удалось изменить реестр."), AppSettings.ProductName);
                return false;
            }

            MessageBox.Show(this, L.T("Готово. Перезагрузите компьютер, чтобы изменение вступило в силу."), AppSettings.ProductName);
            return true;
        }
        catch (System.ComponentModel.Win32Exception)
        {
            // Пользователь отказался в окне UAC
            return false;
        }
    }

    // Свойство, а не поле: подписи должны переводиться заново после смены языка
    private static (MappingAction Action, string Title)[] MappingActions => new[]
    {
        (MappingAction.Shortcut, L.T("Нажать сочетание")),
        (MappingAction.SwitchLanguage, L.T("Сменить язык")),
        (MappingAction.OpenLauncher, L.T("Все приложения")),
        (MappingAction.ShowDesktop, L.T("Свернуть все окна")),
        (MappingAction.ToggleCapsLock, "Caps Lock"),
        (MappingAction.Disable, L.T("Ничего (отключить)")),
    };

    private StackPanel? _mappingsPanel;

    /// <summary>Список правил «нажал → получил», как в PowerToys Keyboard Manager.</summary>
    private void AddMappings()
    {
        var help = new TextBlock
        {
            Text = L.T("Слева — что вы нажимаете (физически), справа — что получится. Правила срабатывают раньше встроенных «маковских» сочетаний. Поле «только в» — имя exe, например chrome.exe; пусто — везде."),
            FontSize = 12,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 0, 0, 8),
        };
        help.SetResourceReference(TextBlock.ForegroundProperty, Theme.MenuSecondary);
        SettingsPanel.Children.Add(help);

        _mappingsPanel = new StackPanel();
        SettingsPanel.Children.Add(_mappingsPanel);
        RebuildMappings();

        var addButton = new Button
        {
            Content = L.T("+ Добавить переназначение"),
            HorizontalAlignment = HorizontalAlignment.Left,
            Padding = new Thickness(12, 5, 12, 5),
            Margin = new Thickness(0, 6, 0, 6),
        };
        addButton.Click += (_, _) =>
        {
            var from = ShortcutRecorderWindow.Record(this, L.T("Что нажимаете"));
            if (from == null)
            {
                return;
            }

            // Новый список целиком: перехватчик читает его в своём потоке
            S.KeyMappings = new List<KeyMapping>(S.KeyMappings) { new() { From = from } };
            RebuildMappings();
            ScheduleApply();
        };
        SettingsPanel.Children.Add(addButton);
    }

    private void RebuildMappings()
    {
        if (_mappingsPanel == null)
        {
            return;
        }

        _mappingsPanel.Children.Clear();
        if (S.KeyMappings.Count == 0)
        {
            var empty = new TextBlock { Text = L.T("Пока нет переназначений."), FontSize = 12, Margin = new Thickness(0, 2, 0, 2) };
            empty.SetResourceReference(TextBlock.ForegroundProperty, Theme.MenuSecondary);
            _mappingsPanel.Children.Add(empty);
            return;
        }

        foreach (var mapping in S.KeyMappings)
        {
            _mappingsPanel.Children.Add(CreateMappingRow(mapping));
        }
    }

    private FrameworkElement CreateMappingRow(KeyMapping mapping)
    {
        var fromButton = CreateShortcutButton(L.T("Что нажимаете"), () => mapping.From, v => mapping.From = v, 150);
        var toButton = CreateShortcutButton(L.T("Что получится"), () => mapping.To, v => mapping.To = v, 150);
        toButton.Visibility = mapping.Action == MappingAction.Shortcut ? Visibility.Visible : Visibility.Collapsed;

        var actionBox = new ComboBox { Width = 160, Margin = new Thickness(0, 0, 8, 0), VerticalContentAlignment = VerticalAlignment.Center };
        foreach (var (_, title) in MappingActions)
        {
            actionBox.Items.Add(title);
        }

        actionBox.SelectedIndex = Array.FindIndex(MappingActions, item => item.Action == mapping.Action);
        actionBox.SelectionChanged += (_, _) =>
        {
            mapping.Action = MappingActions[actionBox.SelectedIndex].Action;
            toButton.Visibility = mapping.Action == MappingAction.Shortcut ? Visibility.Visible : Visibility.Collapsed;
            ScheduleApply();
        };

        var arrow = new TextBlock { Text = "→", FontSize = 16, Margin = new Thickness(8, 0, 8, 0), VerticalAlignment = VerticalAlignment.Center };
        var firstLine = new StackPanel { Orientation = Orientation.Horizontal };
        firstLine.Children.Add(fromButton);
        firstLine.Children.Add(arrow);
        firstLine.Children.Add(actionBox);
        firstLine.Children.Add(toButton);

        var appBox = new TextBox { Text = mapping.App ?? "", Width = 160, Padding = new Thickness(4, 2, 4, 2), VerticalAlignment = VerticalAlignment.Center };
        appBox.LostFocus += (_, _) =>
        {
            mapping.App = string.IsNullOrWhiteSpace(appBox.Text) ? null : appBox.Text.Trim();
            ScheduleApply();
        };

        var deleteButton = new Button { Content = L.T("Удалить"), Padding = new Thickness(10, 2, 10, 2), Margin = new Thickness(12, 0, 0, 0) };
        deleteButton.Click += (_, _) =>
        {
            S.KeyMappings = S.KeyMappings.Where(item => item != mapping).ToList();
            RebuildMappings();
            ScheduleApply();
        };

        var secondLine = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 4, 0, 0) };
        secondLine.Children.Add(new TextBlock { Text = L.T("только в:"), Margin = new Thickness(0, 0, 8, 0), VerticalAlignment = VerticalAlignment.Center });
        secondLine.Children.Add(appBox);
        secondLine.Children.Add(deleteButton);

        var card = new Border
        {
            Padding = new Thickness(10, 8, 10, 8),
            Margin = new Thickness(0, 0, 0, 6),
            CornerRadius = new CornerRadius(6),
            BorderThickness = new Thickness(1),
            Child = new StackPanel { Children = { firstLine, secondLine } },
        };
        card.SetResourceReference(Border.BorderBrushProperty, Theme.MenuSeparator);
        return card;
    }

    private void AddChoice<T>(string label, (T Value, string Title)[] options, Func<T> get, Action<T> set)
    {
        var row = new DockPanel { Margin = new Thickness(0, 4, 0, 6) };
        var caption = new TextBlock { Text = label, Width = 190, VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(0, 2, 0, 0) };
        DockPanel.SetDock(caption, Dock.Left);
        row.Children.Add(caption);
        var panel = new WrapPanel();
        row.Children.Add(panel);
        string group = label;
        foreach (var (value, title) in options)
        {
            var radio = new RadioButton
            {
                Content = title,
                GroupName = group,
                IsChecked = EqualityComparer<T>.Default.Equals(get(), value),
                Margin = new Thickness(0, 2, 14, 2),
                VerticalAlignment = VerticalAlignment.Center,
            };
            radio.SetResourceReference(ForegroundProperty, Theme.MenuForeground);
            radio.Checked += (_, _) =>
            {
                set(value);
                ScheduleApply();
            };
            panel.Children.Add(radio);
        }

        SettingsPanel.Children.Add(row);
    }

    /// <summary>Откуда пришло последнее нажатие — чтобы было видно, что автоопределение работает.</summary>
    private void AddKeyboardStatus()
    {
        var status = new TextBlock { Margin = new Thickness(0, 2, 0, 6), FontSize = 12 };
        status.SetResourceReference(TextBlock.ForegroundProperty, Theme.MenuSecondary);

        void Update() => status.Text = KeyRemapper.LastSource == InputSource.Remote
            ? L.T("Сейчас: ввод с Mac через Moonlight — ⌘ = ") + Describe(S.RemoteCommandKey)
            : L.T("Сейчас: клавиатура этого ПК — ⌘ = ") + Describe(S.LocalCommandKey);

        Update();
        var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
        timer.Tick += (_, _) => Update();
        timer.Start();
        Closed += (_, _) => timer.Stop();
        SettingsPanel.Children.Add(status);
    }

    private static string Describe(CommandKey key) => key switch
    {
        CommandKey.Alt => "Alt",
        CommandKey.Win => "Win",
        _ => "Ctrl",
    };

    private void AddExcludedApps()
    {
        var label = new TextBlock { Text = L.T("Не трогать клавиши в программах (exe через запятую):"), Margin = new Thickness(0, 6, 0, 4) };
        var box = new TextBox
        {
            Text = string.Join(", ", S.KeyboardExcludedApps),
            Padding = new Thickness(4, 3, 4, 3),
            Margin = new Thickness(0, 0, 0, 6),
        };
        box.LostFocus += (_, _) =>
        {
            S.KeyboardExcludedApps = box.Text
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .ToList();
            ScheduleApply();
        };
        SettingsPanel.Children.Add(label);
        SettingsPanel.Children.Add(box);
    }

    private void AddShortcutHelp()
    {
        var help = new TextBlock
        {
            FontSize = 12,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 4, 0, 4),
            Text = L.T("⌘C / ⌘V / ⌘X / ⌘Z / ⌘A / ⌘S / ⌘F / ⌘T / ⌘W и любые другие — как Ctrl\n⌘⇧Z — повторить · ⌘Q — закрыть программу · ⌘H, ⌘M — свернуть\n")
                + L.T("⌘Tab — переключение программ · ⌘Пробел — поиск приложений\n⌘←/→ — начало/конец строки · ⌘↑/↓ — начало/конец документа · ⌘⌫ — удалить до начала строки\n")
                + L.T("⌥←/→ — по словам · ⌥⌫ — удалить слово\n⌘⇧3 — снимок экрана · ⌘⇧4 — снимок области · ⌘⌥Esc — диспетчер задач · ⌃⌘Q — блокировка\n")
                + L.T("Caps Lock — сменить язык, удержание — заглавные"),
        };
        help.SetResourceReference(TextBlock.ForegroundProperty, Theme.MenuSecondary);
        SettingsPanel.Children.Add(help);
    }

    private void AddThemeChoice()
    {
        var panel = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 4, 0, 6) };
        panel.Children.Add(new TextBlock { Text = L.T("Тема"), Width = 190, VerticalAlignment = VerticalAlignment.Center });
        foreach (var (kind, title) in new[] { (ThemeKind.Light, L.T("Светлая")), (ThemeKind.Dark, L.T("Тёмная")) })
        {
            var radio = new RadioButton
            {
                Content = title,
                GroupName = "Theme",
                IsChecked = S.Theme == kind,
                Margin = new Thickness(0, 0, 16, 0),
            };
            radio.SetResourceReference(ForegroundProperty, Theme.MenuForeground);
            radio.Checked += (_, _) =>
            {
                S.Theme = kind;
                ScheduleApply();
            };
            panel.Children.Add(radio);
        }

        SettingsPanel.Children.Add(panel);
    }

    private void AddAutostart()
    {
        var check = new CheckBox
        {
            Content = L.T("Запускать вместе с Windows"),
            IsChecked = IsAutostartEnabled(),
            Margin = new Thickness(0, 5, 0, 5),
        };
        check.SetResourceReference(ForegroundProperty, Theme.MenuForeground);
        check.Click += (_, _) => SetAutostart(check.IsChecked == true);
        SettingsPanel.Children.Add(check);
    }

    private static bool IsAutostartEnabled()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath);
        return key?.GetValue(RunValueName) != null;
    }

    private static void SetAutostart(bool enabled)
    {
        using var key = Registry.CurrentUser.CreateSubKey(RunKeyPath);
        if (enabled && Environment.ProcessPath is { } exePath)
        {
            key.SetValue(RunValueName, $"\"{exePath}\"");
        }
        else
        {
            key.DeleteValue(RunValueName, throwOnMissingValue: false);
        }
    }

    private void ScheduleApply()
    {
        _applyTimer.Stop();
        _applyTimer.Start();
    }

    private void OnOpenFolderClick(object sender, RoutedEventArgs e)
    {
        Directory.CreateDirectory(AppSettings.Folder);
        Process.Start(new ProcessStartInfo("explorer.exe", AppSettings.Folder) { UseShellExecute = true });
    }

    private void OnResetClick(object sender, RoutedEventArgs e)
    {
        AppSettings.Reset();
        Build();
    }

    private void OnCloseClick(object sender, RoutedEventArgs e) => Close();
}
