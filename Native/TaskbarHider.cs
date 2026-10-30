using System.Runtime.InteropServices;
using System.Windows.Threading;

namespace WinToMac.Native;

/// <summary>
/// Прячет штатную панель задач Windows на основном мониторе и возвращает её обратно при выходе.
/// Панель переводится в режим автоскрытия (чтобы не занимала место) и скрывается;
/// таймер следит, чтобы Windows не показала её снова.
/// </summary>
public static class TaskbarHider
{
    private const uint ABM_GETSTATE = 4;
    private const uint ABM_SETSTATE = 10;
    private const int ABS_AUTOHIDE = 1;

    private static DispatcherTimer? _timer;
    private static int? _originalState;
    private static IntPtr _tray;

    public static bool IsHidden => _timer != null;

    public static void Hide()
    {
        IntPtr tray = FindExplorerTaskbar();
        if (tray == IntPtr.Zero || _timer != null)
        {
            return;
        }

        var data = new APPBARDATA { cbSize = Marshal.SizeOf<APPBARDATA>(), hWnd = tray };
        var settings = WinToMac.Settings.AppSettings.Current;
        if (settings.TaskbarOriginalState == null)
        {
            settings.TaskbarOriginalState = (int)NativeMethods.SHAppBarMessage(ABM_GETSTATE, ref data).ToUInt32();
            WinToMac.Settings.AppSettings.Save();
        }

        _originalState = settings.TaskbarOriginalState;

        data.lParam = new IntPtr(ABS_AUTOHIDE);
        NativeMethods.SHAppBarMessage(ABM_SETSTATE, ref data);
        NativeMethods.ShowWindow(tray, NativeMethods.SW_HIDE);
        _tray = tray;

        _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(400) };
        _timer.Tick += (_, _) =>
        {
            // Explorer мог перезапуститься — тогда ищем окно заново
            if (!NativeMethods.IsWindow(_tray))
            {
                _tray = FindExplorerTaskbar();
            }

            IntPtr current = _tray;
            if (current != IntPtr.Zero && NativeMethods.IsWindowVisible(current))
            {
                NativeMethods.ShowWindow(current, NativeMethods.SW_HIDE);
            }
        };
        _timer.Start();
    }

    /// <summary>
    /// Окно панели задач именно проводника: служба трея (ManagedShell) создаёт своё окно
    /// с тем же классом Shell_TrayWnd, и FindWindow может вернуть его.
    /// </summary>
    private static IntPtr FindExplorerTaskbar()
    {
        IntPtr found = IntPtr.Zero;
        NativeMethods.EnumWindows((hwnd, _) =>
        {
            if (NativeMethods.GetWindowClass(hwnd) != "Shell_TrayWnd")
            {
                return true;
            }

            NativeMethods.GetWindowThreadProcessId(hwnd, out uint processId);
            string? path = NativeMethods.GetProcessPath(processId);
            if (path != null && path.EndsWith("\\explorer.exe", StringComparison.OrdinalIgnoreCase))
            {
                found = hwnd;
                return false;
            }

            return true;
        }, IntPtr.Zero);
        return found;
    }

    public static void Restore()
    {
        try
        {
            _timer?.Stop();
        }
        catch (InvalidOperationException)
        {
            // Вызов не из UI-потока (выход процесса) — таймер умрёт вместе с ним
        }

        _timer = null;

        IntPtr tray = FindExplorerTaskbar();
        if (tray == IntPtr.Zero)
        {
            return;
        }

        if (_originalState is int state)
        {
            var data = new APPBARDATA
            {
                cbSize = Marshal.SizeOf<APPBARDATA>(),
                hWnd = tray,
                lParam = new IntPtr(state),
            };
            NativeMethods.SHAppBarMessage(ABM_SETSTATE, ref data);
            _originalState = null;
            try
            {
                WinToMac.Settings.AppSettings.Current.TaskbarOriginalState = null;
                WinToMac.Settings.AppSettings.Save();
            }
            catch
            {
                // Не смогли записать — при следующем запуске возьмём сохранённое значение
            }
        }

        NativeMethods.ShowWindow(tray, NativeMethods.SW_SHOW);
    }
}
