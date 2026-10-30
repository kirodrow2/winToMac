using System.Runtime.InteropServices;
using System.Text;
using WinToMac.Localization;

namespace WinToMac.Native;

/// <summary>
/// Встраивание окна между обоями и значками рабочего стола (приём WorkerW, как в Lively Wallpaper).
///
/// Сообщение 0x052C просит проводник создать окно WorkerW под значками. Дальше две схемы:
/// - до Windows 11 24H2: WorkerW — отдельное окно верхнего уровня, наше окно становится его дочерним;
/// - Windows 11 24H2 и новее: значки (SHELLDLL_DefView) и WorkerW — дочерние окна Progman,
///   а сам Progman рисуется через DirectComposition. Наше окно должно стать дочерним Progman,
///   встать между значками и WorkerW и быть «слоистым» (WS_EX_LAYERED) — иначе его просто не видно.
/// </summary>
public static class DesktopWallpaperHost
{
    private const uint SMTO_NORMAL = 0x0;
    private const uint SPI_SETDESKWALLPAPER = 0x0014;
    private const uint SPI_GETDESKWALLPAPER = 0x0073;
    private const long WS_POPUP = 0x80000000L;
    private const long WS_EX_LAYERED = 0x00080000;
    private const uint LWA_ALPHA = 0x2;

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr FindWindowEx(IntPtr parent, IntPtr childAfter, string? className, string? windowName);

    [DllImport("user32.dll")]
    private static extern IntPtr SendMessageTimeout(IntPtr hwnd, uint msg, IntPtr wParam, IntPtr lParam, uint flags, uint timeout, out IntPtr result);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SetParent(IntPtr child, IntPtr parent);

    [DllImport("user32.dll", EntryPoint = "GetParent")]
    private static extern IntPtr GetParentWindow(IntPtr hwnd);

    [DllImport("user32.dll")]
    private static extern bool ScreenToClient(IntPtr hwnd, ref POINT point);

    [DllImport("user32.dll")]
    private static extern bool SetLayeredWindowAttributes(IntPtr hwnd, uint colorKey, byte alpha, uint flags);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern bool SystemParametersInfo(uint action, uint param, StringBuilder value, uint winIni);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern bool SystemParametersInfo(uint action, uint param, string value, uint winIni);

    private readonly record struct Host(IntPtr Parent, IntPtr IconsView, IntPtr Worker, bool IsRaisedDesktop);

    /// <summary>Что произошло при последнем встраивании — показывается в галерее обоев.</summary>
    public static string LastStatus { get; private set; } = "";

    /// <summary>Сделать окно «обоями». false — проводник не дал окно под значками.</summary>
    public static bool Attach(IntPtr hwnd)
    {
        var host = FindHost();
        if (host == null)
        {
            LastStatus = L.T("не найдено окно рабочего стола (Progman)");
            return false;
        }

        long style = NativeMethods.GetWindowLongPtr(hwnd, NativeMethods.GWL_STYLE).ToInt64();
        NativeMethods.SetWindowLongPtr(hwnd, NativeMethods.GWL_STYLE, new IntPtr((style & ~WS_POPUP) | NativeMethods.WS_CHILD));

        if (host.Value.IsRaisedDesktop)
        {
            NativeMethods.AddExtendedStyle(hwnd, WS_EX_LAYERED);
            SetLayeredWindowAttributes(hwnd, 0, 255, LWA_ALPHA);
        }

        if (SetParent(hwnd, host.Value.Parent) == IntPtr.Zero && Marshal.GetLastWin32Error() != 0)
        {
            LastStatus = L.T("Windows не разрешила встроить окно (SetParent)");
            return false;
        }

        _host = host;
        Place(hwnd);
        LastStatus = host.Value.IsRaisedDesktop
            ? L.T("Windows 11 24H2+: окно встроено в Progman под значки")
            : L.T("классическая схема: окно встроено в WorkerW");
        return true;
    }

    private static Host? _host;

    /// <summary>Встроено по схеме Windows 11 24H2+ (окно должно рисоваться программно).</summary>
    public static bool IsRaisedDesktop => _host?.IsRaisedDesktop == true;

    /// <summary>Растянуть окно на основной монитор и поставить его под значки.</summary>
    public static void Place(IntPtr hwnd)
    {
        IntPtr parent = GetParentWindow(hwnd);
        var (width, height) = NativeMethods.GetPrimaryScreenSize();
        var origin = new POINT { X = 0, Y = 0 };
        if (parent != IntPtr.Zero)
        {
            ScreenToClient(parent, ref origin);
        }

        if (_host is { IsRaisedDesktop: true } host)
        {
            EnsureLayered(hwnd);

            // Порядок внутри Progman: значки → наше окно → WorkerW (системные обои)
            NativeMethods.SetWindowPos(hwnd, host.IconsView, origin.X, origin.Y, width, height,
                NativeMethods.SWP_NOACTIVATE | NativeMethods.SWP_SHOWWINDOW);
            if (host.Worker != IntPtr.Zero)
            {
                NativeMethods.SetWindowPos(host.Worker, hwnd, 0, 0, 0, 0,
                    NativeMethods.SWP_NOMOVE | NativeMethods.SWP_NOSIZE | NativeMethods.SWP_NOACTIVATE);
            }

            return;
        }

        NativeMethods.SetWindowPos(hwnd, IntPtr.Zero, origin.X, origin.Y, width, height,
            NativeMethods.SWP_NOZORDER | NativeMethods.SWP_NOACTIVATE | NativeMethods.SWP_SHOWWINDOW);
    }

    /// <summary>
    /// WPF при показе окна переписывает расширенные стили и снимает WS_EX_LAYERED,
    /// а без него 24H2+ окно внутри Progman не рисует. Поэтому флаг проверяется при каждой расстановке.
    /// </summary>
    private static void EnsureLayered(IntPtr hwnd)
    {
        long exStyle = NativeMethods.GetWindowLongPtr(hwnd, NativeMethods.GWL_EXSTYLE).ToInt64();
        if ((exStyle & WS_EX_LAYERED) != 0)
        {
            return;
        }

        NativeMethods.AddExtendedStyle(hwnd, WS_EX_LAYERED);
        SetLayeredWindowAttributes(hwnd, 0, 255, LWA_ALPHA);
    }

    /// <summary>
    /// После закрытия живых обоев может остаться их последний кадр —
    /// повторная установка текущих обоев Windows заставляет проводник перерисоваться.
    /// </summary>
    public static void RefreshSystemWallpaper()
    {
        var path = new StringBuilder(1024);
        if (SystemParametersInfo(SPI_GETDESKWALLPAPER, (uint)path.Capacity, path, 0))
        {
            SystemParametersInfo(SPI_SETDESKWALLPAPER, 0, path.ToString(), 0);
        }
    }

    private static Host? FindHost()
    {
        IntPtr progman = NativeMethods.FindWindow("Progman", null);
        if (progman == IntPtr.Zero)
        {
            return null;
        }

        SendMessageTimeout(progman, 0x052C, new IntPtr(0xD), new IntPtr(0x1), SMTO_NORMAL, 1000, out _);
        SendMessageTimeout(progman, 0x052C, IntPtr.Zero, IntPtr.Zero, SMTO_NORMAL, 1000, out _);

        IntPtr iconsView = FindWindowEx(progman, IntPtr.Zero, "SHELLDLL_DefView", null);
        if (iconsView != IntPtr.Zero)
        {
            // Windows 11 24H2 и новее
            IntPtr worker = FindWindowEx(progman, IntPtr.Zero, "WorkerW", null);
            return new Host(progman, iconsView, worker, IsRaisedDesktop: true);
        }

        // Классическая схема: WorkerW — отдельное окно сразу после окна со значками
        IntPtr classicWorker = IntPtr.Zero;
        NativeMethods.EnumWindows((top, _) =>
        {
            if (FindWindowEx(top, IntPtr.Zero, "SHELLDLL_DefView", null) != IntPtr.Zero)
            {
                classicWorker = FindWindowEx(IntPtr.Zero, top, "WorkerW", null);
            }

            return true;
        }, IntPtr.Zero);

        return classicWorker != IntPtr.Zero
            ? new Host(classicWorker, IntPtr.Zero, IntPtr.Zero, IsRaisedDesktop: false)
            : new Host(progman, IntPtr.Zero, IntPtr.Zero, IsRaisedDesktop: false);
    }
}
