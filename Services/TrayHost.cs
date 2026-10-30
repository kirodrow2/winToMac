using System.Collections;
using ManagedShell;
using ManagedShell.WindowsTray;

namespace WinToMac.Services;

/// <summary>
/// Служба трея на ManagedShell (та же, что в RetroBar): принимает значки Shell_NotifyIcon
/// от программ и подтягивает уже существующие из трея проводника.
/// </summary>
public static class TrayHost
{
    private static ShellManager? _manager;

    /// <summary>Системные значки Windows, которые в верхней панели уже есть свои (звук, сеть, батарея).</summary>
    private static readonly HashSet<string> SystemIconGuids = new(StringComparer.OrdinalIgnoreCase)
    {
        Normalize(NotificationArea.VOLUME_GUID),
        Normalize(NotificationArea.NETWORK_GUID),
        Normalize(NotificationArea.POWER_GUID),
    };

    public static NotificationArea? Area => _manager?.NotificationArea;

    public static void Start()
    {
        try
        {
            var config = ShellManager.DefaultShellConfig;
            config.EnableTasksService = false;
            config.AutoStartTasksService = false;
            config.EnableTrayService = true;
            config.AutoStartTrayService = true;
            _manager = new ShellManager(config);
        }
        catch
        {
            // Без трея оболочка всё равно работает
            _manager = null;
        }
    }

    public static void Stop()
    {
        try
        {
            _manager?.Dispose();
        }
        catch
        {
            // Выходим в любом случае
        }

        _manager = null;
    }

    /// <summary>Значки, которые имеет смысл показывать: без скрытых самим приложением и без системных.</summary>
    public static List<NotifyIcon> GetIcons()
    {
        var result = new List<NotifyIcon>();
        if (Area?.TrayIcons is not IEnumerable icons)
        {
            return result;
        }

        foreach (var item in icons)
        {
            if (item is NotifyIcon icon && !icon.IsHidden && !SystemIconGuids.Contains(Normalize(icon.GUID)))
            {
                result.Add(icon);
            }
        }

        return result;
    }

    private static string Normalize(object? guid) => (guid?.ToString() ?? "").Trim('{', '}').ToLowerInvariant();
}
