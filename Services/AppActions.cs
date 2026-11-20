using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Text;

namespace WinToMac.Services;

/// <summary>Действия из контекстного меню приложения — те же, что в меню «Пуск» Windows.</summary>
public static class AppActions
{
    public static void RunAsAdministrator(string launchTarget)
    {
        try
        {
            if (launchTarget.StartsWith("shell:", StringComparison.OrdinalIgnoreCase))
            {
                // У элементов «Все приложения» (и классических, и из Store) есть свой глагол runas
                InvokeShellVerb(launchTarget, "runas");
            }
            else
            {
                Process.Start(new ProcessStartInfo(launchTarget) { UseShellExecute = true, Verb = "runas" });
            }
        }
        catch (Win32Exception)
        {
            // Запрос UAC отклонён
        }
        catch (COMException)
        {
            // Приложение не поддерживает запуск от администратора
        }
    }

    /// <summary>Ярлык на рабочем столе — через тот же объект оболочки, что и при перетаскивании из «Пуска».</summary>
    public static void CreateDesktopShortcut(string name, string launchTarget)
    {
        string desktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
        string path = GetFreeShortcutPath(desktop, name);

        if (launchTarget.EndsWith(".lnk", StringComparison.OrdinalIgnoreCase) && File.Exists(launchTarget))
        {
            File.Copy(launchTarget, path);
            return;
        }

        if (SHParseDisplayName(launchTarget, IntPtr.Zero, out IntPtr pidl, 0, out _) != 0)
        {
            return;
        }

        try
        {
            var link = (IShellLinkW)new ShellLink();
            link.SetIDList(pidl);
            ((IPersistFile)link).Save(path, true);
            Marshal.ReleaseComObject(link);
        }
        finally
        {
            Marshal.FreeCoTaskMem(pidl);
        }
    }

    public static void OpenFileLocation(string filePath)
    {
        Process.Start(new ProcessStartInfo("explorer.exe", $"/select,\"{filePath}\"") { UseShellExecute = true });
    }

    /// <summary>Стандартное окно «Свойства» Windows для файла.</summary>
    public static void ShowProperties(string filePath)
    {
        var info = new SHELLEXECUTEINFO
        {
            cbSize = Marshal.SizeOf<SHELLEXECUTEINFO>(),
            lpVerb = "properties",
            lpFile = filePath,
            nShow = 5,
            fMask = SEE_MASK_INVOKEIDLIST,
        };
        ShellExecuteEx(ref info);
    }

    private static string GetFreeShortcutPath(string folder, string name)
    {
        string safeName = string.Concat(name.Select(symbol => Path.GetInvalidFileNameChars().Contains(symbol) ? '_' : symbol));
        string path = Path.Combine(folder, safeName + ".lnk");
        for (int number = 2; File.Exists(path); number++)
        {
            path = Path.Combine(folder, $"{safeName} ({number}).lnk");
        }

        return path;
    }

    private static void InvokeShellVerb(string parsingName, string verb)
    {
        Type shellType = Type.GetTypeFromProgID("Shell.Application") ?? throw new COMException("Shell.Application");
        dynamic shell = Activator.CreateInstance(shellType)!;
        dynamic folder = shell.NameSpace("shell:AppsFolder");
        dynamic? item = folder.ParseName(parsingName["shell:AppsFolder\\".Length..]);
        if (item == null)
        {
            throw new COMException(parsingName);
        }

        item.InvokeVerb(verb);
    }

    private const uint SEE_MASK_INVOKEIDLIST = 0x0000000C;

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct SHELLEXECUTEINFO
    {
        public int cbSize;
        public uint fMask;
        public IntPtr hwnd;
        public string? lpVerb;
        public string? lpFile;
        public string? lpParameters;
        public string? lpDirectory;
        public int nShow;
        public IntPtr hInstApp;
        public IntPtr lpIDList;
        public string? lpClass;
        public IntPtr hkeyClass;
        public uint dwHotKey;
        public IntPtr hIcon;
        public IntPtr hProcess;
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern bool ShellExecuteEx(ref SHELLEXECUTEINFO info);

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern int SHParseDisplayName(string name, IntPtr bindContext, out IntPtr pidl, uint attributesIn, out uint attributesOut);

    [ComImport, Guid("00021401-0000-0000-C000-000000000046")]
    private class ShellLink
    {
    }

    /// <summary>Нужны только первые методы — порядок в таблице должен совпадать с IShellLinkW.</summary>
    [ComImport, InterfaceType(ComInterfaceType.InterfaceIsIUnknown), Guid("000214F9-0000-0000-C000-000000000046")]
    private interface IShellLinkW
    {
        void GetPath([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder file, int length, IntPtr findData, uint flags);

        void GetIDList(out IntPtr pidl);

        void SetIDList(IntPtr pidl);
    }
}
