using System.Drawing;
using WinToMac.Localization;
using WinToMac.Settings;
using Forms = System.Windows.Forms;

namespace WinToMac.UI;

/// <summary>
/// Значок в трее Windows для режима «только клавиатура»: дока и строки меню нет,
/// поэтому вернуть их, открыть настройки или выйти можно только отсюда.
/// </summary>
public static class KeyboardOnlyTray
{
    private static Forms.NotifyIcon? _icon;

    public static void Show()
    {
        if (_icon != null)
        {
            return;
        }

        var menu = new Forms.ContextMenuStrip();
        menu.Items.Add(L.T("Показать док и строку меню"), null, (_, _) => App.Current.SetKeyboardOnly(false));
        menu.Items.Add(L.T("Все приложения"), null, (_, _) => App.Current.ShowLauncher());
        menu.Items.Add(L.T("Настройки winToMac…"), null, (_, _) => App.Current.ShowSettings());
        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add(L.T("Выйти из winToMac"), null, (_, _) => App.Current.Quit());

        _icon = new Forms.NotifyIcon
        {
            Icon = LoadIcon(),
            Text = L.T("winToMac — только клавиатура"),
            ContextMenuStrip = menu,
            Visible = true,
        };
        _icon.MouseClick += (_, e) =>
        {
            if (e.Button == Forms.MouseButtons.Left)
            {
                App.Current.ShowSettings();
            }
        };
    }

    public static void Hide()
    {
        if (_icon == null)
        {
            return;
        }

        try
        {
            _icon.Visible = false;
            _icon.ContextMenuStrip?.Dispose();
            _icon.Dispose();
        }
        catch
        {
            // Выход в любом случае; призрак значка Windows уберёт при наведении
        }

        _icon = null;
    }

    private static Icon LoadIcon()
    {
        try
        {
            if (Environment.ProcessPath is { } exe && Icon.ExtractAssociatedIcon(exe) is { } icon)
            {
                return icon;
            }
        }
        catch
        {
            // Ниже — стандартный значок
        }

        return SystemIcons.Application;
    }
}
