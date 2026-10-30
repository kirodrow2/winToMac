using System.Windows;
using System.Windows.Controls;
using WinToMac.Native;
using WinToMac.Services;
using WinToMac.Settings;
using WinToMac.Localization;

namespace WinToMac.UI;

/// <summary>
/// Запись сочетания «нажми — и готово», как в PowerToys. Пока окно открыто, перехватчик отдаёт
/// все нажатия сюда и не пускает их дальше — поэтому можно записать даже Win или Alt+Tab.
/// </summary>
public sealed class ShortcutRecorderWindow : Window
{
    private readonly HashSet<ushort> _pressed = new();
    private readonly TextBlock _display;
    private readonly Button _saveButton;
    private KeyModifiers _modifiers;
    private ushort _mainKey;
    private ushort _lastModifier;

    public KeyShortcut? Result { get; private set; }

    public ShortcutRecorderWindow(string title)
    {
        Title = title;
        Width = 420;
        SizeToContent = SizeToContent.Height;
        ResizeMode = ResizeMode.NoResize;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Topmost = true;
        SetResourceReference(BackgroundProperty, Theme.MenuBackground);
        SetResourceReference(ForegroundProperty, Theme.MenuForeground);

        var hint = new TextBlock
        {
            Text = L.T("Нажмите клавишу или сочетание и отпустите.\nЧтобы записать заново — просто нажмите снова."),
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 0, 0, 14),
        };
        _display = new TextBlock
        {
            Text = "…",
            FontSize = 20,
            FontWeight = FontWeights.SemiBold,
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 6, 0, 18),
        };

        _saveButton = new Button { Content = L.T("Сохранить"), Padding = new Thickness(16, 5, 16, 5), IsEnabled = false };
        _saveButton.Click += (_, _) =>
        {
            DialogResult = true;
        };
        var cancelButton = new Button { Content = L.T("Отмена"), Padding = new Thickness(16, 5, 16, 5), Margin = new Thickness(0, 0, 8, 0) };
        cancelButton.Click += (_, _) => DialogResult = false;

        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
        buttons.Children.Add(cancelButton);
        buttons.Children.Add(_saveButton);

        var root = new StackPanel { Margin = new Thickness(20) };
        root.Children.Add(hint);
        root.Children.Add(_display);
        root.Children.Add(buttons);
        Content = root;

        Loaded += (_, _) => KeyRemapper.BeginRecording(e => Dispatcher.BeginInvoke(() => OnKey(e)));
        Closed += (_, _) => KeyRemapper.EndRecording();
    }

    /// <summary>Показать окно записи и вернуть сочетание (null — отмена).</summary>
    public static KeyShortcut? Record(Window owner, string title)
    {
        var window = new ShortcutRecorderWindow(title) { Owner = owner };
        return window.ShowDialog() == true ? window.Result : null;
    }

    private void OnKey(KeyEvent e)
    {
        if (e.IsDown)
        {
            if (_pressed.Count == 0)
            {
                _modifiers = KeyModifiers.None;
                _mainKey = 0;
                _lastModifier = 0;
            }

            _pressed.Add(e.Vk);
            var modifier = KeyNames.ModifierOf(e.Vk);
            if (modifier != KeyModifiers.None)
            {
                _modifiers |= modifier;
                _lastModifier = e.Vk;
            }
            else
            {
                _mainKey = e.Vk;
            }

            _display.Text = Current().ToString();
            return;
        }

        _pressed.Remove(e.Vk);
        if (_pressed.Count == 0 && (_mainKey != 0 || _lastModifier != 0))
        {
            Result = Current();
            _display.Text = Result.ToString();
            _saveButton.IsEnabled = true;
        }
    }

    /// <summary>Если нажаты только модификаторы — последний из них становится основной клавишей.</summary>
    private KeyShortcut Current() => new()
    {
        Modifiers = _modifiers,
        Key = _mainKey != 0 ? _mainKey : _lastModifier,
    };
}
