using System.Runtime;
using System.Runtime.InteropServices;
using System.Windows.Threading;

namespace WinToMac.Services;

/// <summary>
/// Программа почти всё время ждёт: после запуска и закрытия окон (настройки, галерея, список приложений)
/// остаются ненужные объекты и страницы памяти. Раз в несколько минут уплотняем кучу и отдаём
/// неиспользуемые страницы системе — нужные Windows вернёт по первому обращению.
/// </summary>
public static class MemoryTrimmer
{
    private static DispatcherTimer? _timer;

    [DllImport("psapi.dll")]
    private static extern bool EmptyWorkingSet(IntPtr process);

    public static void Start()
    {
        // Первый раз — когда док, меню и иконки уже загрузились
        _timer = new DispatcherTimer(DispatcherPriority.ApplicationIdle) { Interval = TimeSpan.FromSeconds(30) };
        _timer.Tick += (_, _) =>
        {
            _timer.Interval = TimeSpan.FromMinutes(5);
            Trim();
        };
        _timer.Start();
    }

    public static void Trim()
    {
        try
        {
            GCSettings.LargeObjectHeapCompactionMode = GCLargeObjectHeapCompactionMode.CompactOnce;
            GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, blocking: true, compacting: true);
            GC.WaitForPendingFinalizers();
            EmptyWorkingSet(System.Diagnostics.Process.GetCurrentProcess().Handle);
        }
        catch
        {
            // Не получилось — просто подождём следующего раза
        }
    }
}
