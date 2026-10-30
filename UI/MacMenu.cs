using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Threading;
using WinToMac.Native;

namespace WinToMac.UI;

/// <summary>
/// Выпадающее меню в стиле macOS: скруглённое, с синей подсветкой пункта.
/// Штатный ContextMenu WPF выглядит слишком «по-виндовому», поэтому меню собирается из Popup.
/// </summary>
public sealed class MacMenu
{
    private static Popup? _openPopup;

    // Клик снаружи отслеживаем сами: Popup со StaysOpen=false держит захват мыши,
    // а ползунок и календарь внутри меню этот захват сбивают — после них меню переставало
    // закрываться по клику мимо. К тому же штатный Popup «съедал» этот клик.
    private static readonly DispatcherTimer OutsideClickTimer = new() { Interval = TimeSpan.FromMilliseconds(30) };
    private static bool _wasButtonDown;
    private static IntPtr _foregroundOnOpen;

    // Клик по тому же пункту панели закрывает меню и не должен тут же открыть его снова
    private static UIElement? _closedByTarget;
    private static DateTime _closedByTargetAt;

    private readonly StackPanel _panel = new() { MinWidth = 220 };
    private readonly Popup _popup;

    public static bool IsAnyOpen => _openPopup?.IsOpen == true;

    public MacMenu()
    {
        var border = new Border
        {
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(5),
            BorderThickness = new Thickness(0.5),
            Margin = new Thickness(12),
            Child = _panel,
            Effect = new DropShadowEffect { BlurRadius = 18, ShadowDepth = 3, Opacity = 0.35 },
        };
        border.SetResourceReference(Border.BackgroundProperty, Theme.MenuBackground);
        border.SetResourceReference(Border.BorderBrushProperty, Theme.MenuSeparator);

        _popup = new Popup
        {
            AllowsTransparency = true,
            StaysOpen = true,
            Child = border,
            PopupAnimation = PopupAnimation.Fade,
        };
        _popup.Closed += (_, _) =>
        {
            if (_openPopup == _popup)
            {
                _openPopup = null;
                OutsideClickTimer.Stop();
            }
        };
    }

    public MacMenu Add(string text, Action action, bool enabled = true, bool isChecked = false, string? hint = null)
    {
        var check = CreateText(isChecked ? "✓" : "", Theme.MenuForeground);
        check.Width = 18;
        var label = CreateText(text, Theme.MenuForeground);
        var hintText = CreateText(hint ?? "", Theme.MenuSecondary);
        hintText.Margin = new Thickness(24, 0, 4, 0);

        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        Grid.SetColumn(label, 1);
        Grid.SetColumn(hintText, 2);
        grid.Children.Add(check);
        grid.Children.Add(label);
        grid.Children.Add(hintText);

        var row = new Border
        {
            CornerRadius = new CornerRadius(4),
            Padding = new Thickness(4, 3, 8, 4),
            Background = Brushes.Transparent,
            Child = grid,
            Opacity = enabled ? 1 : 0.4,
        };

        if (enabled)
        {
            row.MouseEnter += (_, _) =>
            {
                row.SetResourceReference(Border.BackgroundProperty, Theme.MenuHover);
                foreach (var text in new[] { check, label, hintText })
                {
                    text.Foreground = Brushes.White;
                }
            };
            row.MouseLeave += (_, _) =>
            {
                row.Background = Brushes.Transparent;
                check.SetResourceReference(TextBlock.ForegroundProperty, Theme.MenuForeground);
                label.SetResourceReference(TextBlock.ForegroundProperty, Theme.MenuForeground);
                hintText.SetResourceReference(TextBlock.ForegroundProperty, Theme.MenuSecondary);
            };
            row.MouseLeftButtonUp += (_, e) =>
            {
                e.Handled = true;
                _popup.IsOpen = false;
                // Действие после закрытия меню: иначе Popup может перехватить фокус у открываемого окна
                row.Dispatcher.BeginInvoke(action);
            };
        }

        _panel.Children.Add(row);
        return this;
    }

    public MacMenu Header(string text)
    {
        var header = CreateText(text, Theme.MenuSecondary);
        header.FontWeight = FontWeights.SemiBold;
        header.FontSize = 11.5;
        header.Margin = new Thickness(22, 3, 8, 3);
        _panel.Children.Add(header);
        return this;
    }

    public MacMenu Separator()
    {
        var line = new Border { Height = 1, Margin = new Thickness(8, 4, 8, 4) };
        line.SetResourceReference(Border.BackgroundProperty, Theme.MenuSeparator);
        _panel.Children.Add(line);
        return this;
    }

    /// <summary>Добавить произвольный элемент (например, ползунок громкости).</summary>
    public MacMenu AddElement(UIElement element)
    {
        _panel.Children.Add(element);
        return this;
    }

    static MacMenu()
    {
        OutsideClickTimer.Tick += (_, _) => CheckOutsideClick();
    }

    public void Show(UIElement target, PlacementMode placement)
    {
        if (_closedByTarget == target && DateTime.UtcNow - _closedByTargetAt < TimeSpan.FromMilliseconds(600))
        {
            _closedByTarget = null;
            return;
        }

        _closedByTarget = null;
        if (_openPopup != null)
        {
            _openPopup.IsOpen = false;
        }

        _popup.PlacementTarget = target;
        _popup.Placement = placement;
        // Margin у Border нужен под тень — компенсируем его, чтобы меню прилегало к цели
        _popup.HorizontalOffset = -12;
        _popup.VerticalOffset = placement == PlacementMode.Top ? 8 : -8;
        _popup.IsOpen = true;
        _openPopup = _popup;

        // Кнопка, которой меню открыли, ещё может быть нажата — её нажатие не считаем
        _wasButtonDown = true;
        _foregroundOnOpen = NativeMethods.GetForegroundWindow();
        OutsideClickTimer.Start();
    }

    private static void CheckOutsideClick()
    {
        var popup = _openPopup;
        if (popup == null || !popup.IsOpen || popup.Child is not FrameworkElement content)
        {
            OutsideClickTimer.Stop();
            return;
        }

        // Переключились на другое окно с клавиатуры (Alt+Tab, Win) — меню больше не нужно
        var foreground = NativeMethods.GetForegroundWindow();
        if (foreground != _foregroundOnOpen && !IsOwnWindow(foreground))
        {
            popup.IsOpen = false;
            return;
        }

        bool isDown = IsPressed(0x01) || IsPressed(0x02) || IsPressed(0x04);
        bool isNewClick = isDown && !_wasButtonDown;
        _wasButtonDown = isDown;
        if (!isNewClick || !NativeMethods.GetCursorPos(out var cursor))
        {
            return;
        }

        if (IsInside(content, cursor))
        {
            return;
        }

        if (popup.PlacementTarget is FrameworkElement target && IsInside(target, cursor))
        {
            _closedByTarget = target;
            _closedByTargetAt = DateTime.UtcNow;
        }

        popup.IsOpen = false;
    }

    private static bool IsPressed(int vk) => (NativeMethods.GetAsyncKeyState(vk) & 0x8000) != 0;

    private static bool IsOwnWindow(IntPtr hwnd)
    {
        NativeMethods.GetWindowThreadProcessId(hwnd, out uint processId);
        return processId == (uint)Environment.ProcessId;
    }

    private static bool IsInside(FrameworkElement element, POINT cursor)
    {
        if (PresentationSource.FromVisual(element) == null)
        {
            return false;
        }

        var topLeft = element.PointToScreen(new Point(0, 0));
        var bottomRight = element.PointToScreen(new Point(element.ActualWidth, element.ActualHeight));
        return cursor.X >= topLeft.X && cursor.X < bottomRight.X && cursor.Y >= topLeft.Y && cursor.Y < bottomRight.Y;
    }

    private static TextBlock CreateText(string text, string brushKey)
    {
        var block = new TextBlock
        {
            Text = text,
            FontSize = 13,
            VerticalAlignment = VerticalAlignment.Center,
        };
        block.SetResourceReference(TextBlock.ForegroundProperty, brushKey);
        return block;
    }
}
