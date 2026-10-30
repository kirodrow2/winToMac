using System.Diagnostics;

namespace WinToMac.Native;

/// <summary>Системные действия: питание, вызов штатных панелей Windows.</summary>
public static class SystemActions
{
    public static void OpenUri(string uri)
    {
        try
        {
            Process.Start(new ProcessStartInfo(uri) { UseShellExecute = true });
        }
        catch
        {
            // Нечего показывать пользователю: просто ничего не откроется
        }
    }

    public static void Sleep() => NativeMethods.SetSuspendState(false, false, false);

    public static void Lock() => NativeMethods.LockWorkStation();

    public static void Restart() => RunShutdown("/r /t 0");

    public static void ShutDown() => RunShutdown("/s /t 0");

    public static void SignOut() => RunShutdown("/l");

    /// <summary>Сочетание Win + клавиша (Win+N — уведомления, Win+Пробел — раскладка).</summary>
    public static void PressWinKey(byte? key = null)
    {
        if (key is byte k)
        {
            InputSender.Chord(InputSender.VK_LWIN, k);
        }
        else
        {
            InputSender.Chord(InputSender.VK_LWIN);
        }
    }

    public static void OpenNotifications() => PressWinKey((byte)'N');

    /// <summary>Ctrl+Esc открывает «Пуск» надёжнее, чем одиночное нажатие Win.</summary>
    public static void OpenStartMenu() => InputSender.Chord(InputSender.VK_CONTROL, InputSender.VK_ESCAPE);

    private static void RunShutdown(string arguments)
    {
        Process.Start(new ProcessStartInfo("shutdown.exe", arguments)
        {
            CreateNoWindow = true,
            UseShellExecute = false,
        });
    }
}
