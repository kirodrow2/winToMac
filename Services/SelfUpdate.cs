using System.Diagnostics;
using System.IO;
using Microsoft.Win32;
using WinToMac.Settings;

namespace WinToMac.Services;

/// <summary>
/// Обновление «просто запусти новый exe»: новая копия просит работающую выйти (или закрывает её),
/// переписывает себя на место winToMac.exe, перезапускается оттуда и убирает лишние exe рядом.
/// </summary>
public static class SelfUpdate
{
    public const string MainExeName = AppSettings.ProductName + ".exe";

    private const string QuitEventName = "WinToMac.QuitRequest";
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";

    /// <summary>Попросить работающую копию выйти, а копии старых версий (без этого сигнала) — закрыть.</summary>
    public static void StopOtherInstances(Mutex singleInstance)
    {
        try
        {
            if (EventWaitHandle.TryOpenExisting(QuitEventName, out var quitRequest))
            {
                quitRequest.Set();
                quitRequest.Dispose();
            }
        }
        catch
        {
            // Нет работающей копии нового образца
        }

        // Ждём, пока работающая копия вернёт панель задач и выйдет
        bool acquired;
        try
        {
            acquired = singleInstance.WaitOne(TimeSpan.FromSeconds(5));
        }
        catch (AbandonedMutexException)
        {
            acquired = true;
        }

        if (!acquired)
        {
            KillOthers();
            try
            {
                singleInstance.WaitOne(TimeSpan.FromSeconds(3));
            }
            catch (AbandonedMutexException)
            {
                // Копию закрыли принудительно — мьютекс наш
            }
        }

        // Копии MacShell и winToMac 1.0–1.1 сигнала не знают
        KillOthers();
    }

    /// <summary>Слушать просьбу новой версии выйти.</summary>
    public static void ListenForQuitRequest(Action quit)
    {
        var quitRequest = new EventWaitHandle(false, EventResetMode.AutoReset, QuitEventName);
        var thread = new Thread(() =>
        {
            quitRequest.WaitOne();
            quit();
        })
        {
            IsBackground = true,
            Name = "winToMac quit request",
        };
        thread.Start();
    }

    /// <summary>
    /// Запущены не из winToMac.exe (например, winToMac.update.exe) — копируем себя на место winToMac.exe
    /// и перезапускаемся оттуда. true — текущей копии нужно выйти.
    /// </summary>
    public static bool RelaunchFromMainExe()
    {
        string? current = Environment.ProcessPath;
        if (current == null || Path.GetFileName(current).Equals(MainExeName, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        string folder = Path.GetDirectoryName(current)!;
        string target = Path.Combine(folder, MainExeName);
        for (int attempt = 0; attempt < 20; attempt++)
        {
            try
            {
                // Старый exe может держать антивирус или ещё не до конца вышедшая копия. Занятый exe
                // нельзя перезаписать, но можно переименовать — тогда место winToMac.exe освобождается.
                // Раньше в этом случае обновление тихо продолжало работать как winToMac.update.exe,
                // а winToMac.exe оставался старым — и при следующем запуске снова предлагал обновиться.
                if (attempt >= 4 && File.Exists(target))
                {
                    string aside = Path.Combine(folder, $"{AppSettings.ProductName}.old-{DateTime.Now:yyyyMMddHHmmss}.exe");
                    File.Move(target, aside);
                    StartupLog.Write($"Старый {MainExeName} занят — отложен как {Path.GetFileName(aside)}");
                }

                File.Copy(current, target, overwrite: true);
                Process.Start(new ProcessStartInfo(target) { UseShellExecute = true });
                StartupLog.Write($"Обновление встало на место {target}");
                return true;
            }
            catch (IOException exception)
            {
                StartupLog.Write($"Не удалось заменить {MainExeName} (попытка {attempt + 1}): {exception.Message}");
                Thread.Sleep(500);
            }
            catch (UnauthorizedAccessException exception)
            {
                // Папка только для чтения — работаем как есть
                StartupLog.Write($"Нет прав на замену {MainExeName}: {exception.Message}");
                return false;
            }
        }

        return false;
    }

    /// <summary>Убрать старые exe рядом с собой и поправить автозапуск на winToMac.exe.</summary>
    public static void CleanUpInBackground()
    {
        string? current = Environment.ProcessPath;
        if (current == null)
        {
            return;
        }

        string folder = Path.GetDirectoryName(current)!;
        string main = Path.Combine(folder, MainExeName);
        // Автозапуск всегда на winToMac.exe: временная winToMac.update.exe удаляется при следующем запуске
        FixAutostart(File.Exists(main) ? main : current);
        var thread = new Thread(() =>
        {
            // Только что закрытые копии отпускают файлы не сразу
            for (int attempt = 0; attempt < 20; attempt++)
            {
                var leftovers = Directory.EnumerateFiles(folder, "*.exe")
                    .Where(path => IsLeftover(path, current))
                    .ToList();
                bool hasOldProgramFolder = DeleteOldProgramFolder(folder);
                if (leftovers.Count == 0 && !hasOldProgramFolder)
                {
                    return;
                }

                foreach (string path in leftovers)
                {
                    try
                    {
                        File.Delete(path);
                    }
                    catch
                    {
                        // Ещё занят — попробуем позже
                    }
                }

                Thread.Sleep(3000);
            }
        })
        {
            IsBackground = true,
            Priority = ThreadPriority.BelowNormal,
        };
        thread.Start();
    }

    /// <summary>
    /// В версиях до 1.2 программа жила в подпапке Program — после переезда exe в корень она лишняя.
    /// true — папка ещё есть (что-то в ней занято).
    /// </summary>
    private static bool DeleteOldProgramFolder(string folder)
    {
        string old = Path.Combine(folder, "Program");
        if (!Directory.Exists(old))
        {
            return false;
        }

        bool isOurs = Directory.EnumerateFiles(old, "*.exe")
            .Select(Path.GetFileName)
            .Any(name => name!.StartsWith("MacShell", StringComparison.OrdinalIgnoreCase)
                || name.StartsWith(AppSettings.ProductName, StringComparison.OrdinalIgnoreCase));
        if (!isOurs)
        {
            return false;
        }

        try
        {
            Directory.Delete(old, recursive: true);
            return false;
        }
        catch
        {
            // Старая копия ещё работает или файл держит процесс администратора — удалим при следующем запуске
            return true;
        }
    }

    private static bool IsLeftover(string path, string current)
    {
        string name = Path.GetFileName(path);
        if (path.Equals(current, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return name.StartsWith("MacShell", StringComparison.OrdinalIgnoreCase)
            || name.StartsWith(AppSettings.ProductName + ".", StringComparison.OrdinalIgnoreCase)
                && !name.Equals(MainExeName, StringComparison.OrdinalIgnoreCase);
    }

    private static void KillOthers()
    {
        int self = Environment.ProcessId;
        foreach (var process in Process.GetProcesses())
        {
            try
            {
                if (process.Id != self
                    && (process.ProcessName.StartsWith("MacShell", StringComparison.OrdinalIgnoreCase)
                        || process.ProcessName.StartsWith(AppSettings.ProductName, StringComparison.OrdinalIgnoreCase)))
                {
                    process.Kill();
                    process.WaitForExit(3000);
                }
            }
            catch
            {
                // Процесс от администратора — закрыть без прав нельзя
            }
            finally
            {
                process.Dispose();
            }
        }
    }

    /// <summary>Если автозапуск включён, он должен указывать на этот winToMac.exe, а не на временную копию.</summary>
    private static void FixAutostart(string current)
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: true);
            if (key?.GetValue(AppSettings.ProductName) is string value && !value.Contains(current, StringComparison.OrdinalIgnoreCase))
            {
                key.SetValue(AppSettings.ProductName, $"\"{current}\"");
            }
        }
        catch
        {
            // Без доступа к реестру автозапуск можно поправить в настройках
        }
    }
}
