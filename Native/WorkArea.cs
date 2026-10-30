using System.Runtime.InteropServices;
using System.Windows.Threading;

namespace WinToMac.Native;

/// <summary>
/// Рабочая область основного монитора — то, куда разворачиваются окна.
/// AppBar сам по себе не всегда срабатывает (Explorer пересчитывает область по-своему),
/// поэтому область выставляется явно и раз в секунду проверяется.
/// </summary>
public static class WorkArea
{
    private const uint SPI_SETWORKAREA = 0x2F;
    private const uint SPI_GETWORKAREA = 0x30;
    private const uint SPIF_SENDCHANGE = 0x2;

    [DllImport("user32.dll")]
    private static extern bool SystemParametersInfo(uint action, uint param, ref RECT rect, uint winIni);

    private static int _top;
    private static int _bottom;
    private static DispatcherTimer? _timer;

    /// <summary>Сколько раз уже подгоняли окно: если программа упрямо возвращает своё положение, отступаем.</summary>
    private static readonly Dictionary<IntPtr, int> FitAttempts = new();

    /// <summary>Сколько физических пикселей сверху занято верхней панелью.</summary>
    public static void SetTop(int pixels)
    {
        _top = pixels;
        Apply();
    }

    /// <summary>Сколько физических пикселей снизу занято доком (0 — док не резервирует место).</summary>
    public static void SetBottom(int pixels)
    {
        _bottom = pixels;
        Apply();
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
        var (width, height) = NativeMethods.GetPrimaryScreenSize();
        var full = new RECT { Left = 0, Top = 0, Right = width, Bottom = height };
        SystemParametersInfo(SPI_SETWORKAREA, 0, ref full, SPIF_SENDCHANGE);
    }

    private static void Apply()
    {
        if (_timer == null)
        {
            _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            _timer.Tick += (_, _) => Apply();
            _timer.Start();
        }

        var desired = GetDesired();
        var current = new RECT();
        SystemParametersInfo(SPI_GETWORKAREA, 0, ref current, 0);
        if (current.Left != desired.Left || current.Top != desired.Top
            || current.Right != desired.Right || current.Bottom != desired.Bottom)
        {
            SystemParametersInfo(SPI_SETWORKAREA, 0, ref desired, SPIF_SENDCHANGE);
        }

        FitMaximizedWindows(desired);
    }

    private static RECT GetDesired()
    {
        var (width, height) = NativeMethods.GetPrimaryScreenSize();
        return new RECT { Left = 0, Top = _top, Right = width, Bottom = height - _bottom };
    }

    /// <summary>
    /// Окна, развёрнутые до запуска оболочки (или самовольно), уходят под панели —
    /// подгоняем их под рабочую область, не снимая состояния «развёрнуто».
    /// </summary>
    private static void FitMaximizedWindows(RECT area)
    {
        var (width, height) = NativeMethods.GetPrimaryScreenSize();
        foreach (var window in WindowEnumerator.GetTaskbarWindows(withDetails: false))
        {
            if (!NativeMethods.IsZoomed(window.Handle) || !NativeMethods.GetWindowRect(window.Handle, out var rect))
            {
                continue;
            }

            // Полноэкранные игры и видео не трогаем
            if (NativeMethods.IsFullScreen(window.Handle))
            {
                continue;
            }

            // Окно на другом мониторе
            if (rect.Left >= width || rect.Right <= 0 || rect.Top >= height || rect.Bottom <= 0)
            {
                continue;
            }

            // У развёрнутого окна рамка выходит за рабочую область на свою толщину — её сохраняем
            int border = Math.Max(0, area.Left - rect.Left);
            int expectedTop = area.Top - border;
            int expectedBottom = area.Bottom + border;
            if (Math.Abs(rect.Top - expectedTop) <= 2 && Math.Abs(rect.Bottom - expectedBottom) <= 2)
            {
                continue;
            }

            int attempts = FitAttempts.GetValueOrDefault(window.Handle);
            if (attempts >= 3)
            {
                continue;
            }

            FitAttempts[window.Handle] = attempts + 1;
            NativeMethods.SetWindowPos(window.Handle, IntPtr.Zero, rect.Left, expectedTop, rect.Width,
                expectedBottom - expectedTop, NativeMethods.SWP_NOZORDER | NativeMethods.SWP_NOACTIVATE);
        }
    }
}
