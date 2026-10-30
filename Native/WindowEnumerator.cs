using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;

namespace WinToMac.Native;

/// <summary>Окно верхнего уровня, которое в Windows показалось бы на панели задач.</summary>
public sealed class WindowInfo
{
    public IntPtr Handle { get; init; }
    public string Title { get; init; } = "";
    public string? ExePath { get; init; }
    public string? AppId { get; init; }

    public string? ExeName => ExePath == null ? null : Path.GetFileName(ExePath).ToLowerInvariant();
}

public static class WindowEnumerator
{
    private static readonly HashSet<string> IgnoredClasses = new(StringComparer.OrdinalIgnoreCase)
    {
        "Progman", "WorkerW", "Shell_TrayWnd", "Shell_SecondaryTrayWnd",
        "Windows.UI.Core.CoreWindow", "XamlExplorerHostIslandWindow",
    };

    private static readonly uint OwnProcessId = (uint)Environment.ProcessId;

    /// <param name="withDetails">false — только хэндлы, без пути к exe и AppId (дешевле).</param>
    public static List<WindowInfo> GetTaskbarWindows(bool withDetails = true)
    {
        var result = new List<WindowInfo>();
        NativeMethods.EnumWindows((hwnd, _) =>
        {
            if (IsTaskbarWindow(hwnd, out uint processId))
            {
                result.Add(withDetails
                    ? new WindowInfo
                    {
                        Handle = hwnd,
                        Title = NativeMethods.GetWindowTitle(hwnd),
                        ExePath = NativeMethods.GetProcessPath(processId),
                        AppId = WindowAppId.Get(hwnd) ?? WindowAppId.GetForProcess(processId),
                    }
                    : new WindowInfo { Handle = hwnd });
            }

            return true;
        }, IntPtr.Zero);
        return result;
    }

    private static bool IsTaskbarWindow(IntPtr hwnd, out uint processId)
    {
        processId = 0;
        if (!NativeMethods.IsWindowVisible(hwnd) || NativeMethods.GetWindowTextLength(hwnd) == 0)
        {
            return false;
        }

        NativeMethods.GetWindowThreadProcessId(hwnd, out processId);
        if (processId == OwnProcessId)
        {
            return false;
        }

        long exStyle = NativeMethods.GetWindowLongPtr(hwnd, NativeMethods.GWL_EXSTYLE).ToInt64();
        bool isAppWindow = (exStyle & NativeMethods.WS_EX_APPWINDOW) != 0;
        if (!isAppWindow)
        {
            if ((exStyle & NativeMethods.WS_EX_TOOLWINDOW) != 0)
            {
                return false;
            }

            if (NativeMethods.GetWindow(hwnd, NativeMethods.GW_OWNER) != IntPtr.Zero)
            {
                return false;
            }
        }

        // «Спрятанные» окна UWP и окна с других виртуальных рабочих столов
        if (NativeMethods.DwmGetWindowAttribute(hwnd, NativeMethods.DWMWA_CLOAKED, out int cloaked, sizeof(int)) == 0
            && cloaked != 0)
        {
            return false;
        }

        return !IgnoredClasses.Contains(NativeMethods.GetWindowClass(hwnd));
    }

    /// <summary>Вывести окно на передний план (с развёртыванием из свёрнутого состояния).</summary>
    public static void Activate(IntPtr hwnd)
    {
        if (NativeMethods.IsIconic(hwnd))
        {
            NativeMethods.ShowWindow(hwnd, NativeMethods.SW_RESTORE);
        }

        if (NativeMethods.SetForegroundWindow(hwnd) && NativeMethods.GetForegroundWindow() == hwnd)
        {
            return;
        }

        // Windows запрещает фоновому процессу забирать фокус; нажатие Alt снимает ограничение
        InputSender.Chord(InputSender.VK_MENU);
        NativeMethods.SetForegroundWindow(hwnd);
    }

    public static void Minimize(IntPtr hwnd) => NativeMethods.ShowWindow(hwnd, NativeMethods.SW_MINIMIZE);

    public static void Close(IntPtr hwnd) =>
        NativeMethods.PostMessage(hwnd, NativeMethods.WM_CLOSE, IntPtr.Zero, IntPtr.Zero);

    /// <summary>Имя приложения для активного окна — как жирная надпись в строке меню macOS.</summary>
    public static string GetAppDisplayName(string? exePath, string fallback)
    {
        if (exePath == null)
        {
            return fallback;
        }

        // Док опрашивает окна каждую секунду — читать версию файла каждый раз дорого
        return DisplayNames.GetOrAdd(exePath, path => ReadDisplayName(path));
    }

    private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, string> DisplayNames =
        new(StringComparer.OrdinalIgnoreCase);

    private static string ReadDisplayName(string exePath)
    {
        try
        {
            var info = FileVersionInfo.GetVersionInfo(exePath);
            if (!string.IsNullOrWhiteSpace(info.FileDescription))
            {
                return info.FileDescription.Trim();
            }

            if (!string.IsNullOrWhiteSpace(info.ProductName))
            {
                return info.ProductName.Trim();
            }
        }
        catch
        {
            // Нет доступа к файлу — покажем имя процесса
        }

        return Path.GetFileNameWithoutExtension(exePath);
    }
}

/// <summary>Чтение AppUserModelID окна через его property store.</summary>
internal static class WindowAppId
{
    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    private struct PROPERTYKEY
    {
        public Guid fmtid;
        public uint pid;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct PROPVARIANT
    {
        public ushort vt;
        public ushort reserved1, reserved2, reserved3;
        public IntPtr pointer;
        public IntPtr padding;
    }

    [ComImport]
    [Guid("886d8eeb-8cf2-4446-8d02-cdba1dbdcf99")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IPropertyStore
    {
        [PreserveSig]
        int GetCount(out uint count);

        [PreserveSig]
        int GetAt(uint index, out PROPERTYKEY key);

        [PreserveSig]
        int GetValue(ref PROPERTYKEY key, out PROPVARIANT value);
    }

    [DllImport("shell32.dll")]
    private static extern int SHGetPropertyStoreForWindow(
        IntPtr hwnd,
        [MarshalAs(UnmanagedType.LPStruct)] Guid riid,
        [MarshalAs(UnmanagedType.Interface)] out IPropertyStore store);

    [DllImport("ole32.dll")]
    private static extern int PropVariantClear(ref PROPVARIANT value);

    private const ushort VT_LPWSTR = 31;

    private static PROPERTYKEY _appUserModelIdKey = new()
    {
        fmtid = new Guid("9F4C2855-9F79-4B39-A8D0-E1D42DE1D5F3"),
        pid = 5,
    };

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetApplicationUserModelId(IntPtr process, ref int length, System.Text.StringBuilder id);

    private static readonly System.Collections.Concurrent.ConcurrentDictionary<uint, string?> ProcessIds = new();

    /// <summary>
    /// AppUserModelID процесса из Microsoft Store/MSIX (Claude, Teams, WhatsApp…). Их окна часто не задают
    /// его сами, а exe лежит в закрытой папке WindowsApps — без этого у значка в доке не будет иконки.
    /// </summary>
    public static string? GetForProcess(uint processId) =>
        ProcessIds.GetOrAdd(processId, id =>
        {
            IntPtr handle = NativeMethods.OpenProcess(NativeMethods.PROCESS_QUERY_LIMITED_INFORMATION, false, id);
            if (handle == IntPtr.Zero)
            {
                return null;
            }

            try
            {
                int length = 256;
                var builder = new System.Text.StringBuilder(length);
                return GetApplicationUserModelId(handle, ref length, builder) == 0 ? builder.ToString() : null;
            }
            catch
            {
                return null;
            }
            finally
            {
                NativeMethods.CloseHandle(handle);
            }
        });

    public static string? Get(IntPtr hwnd)
    {
        try
        {
            if (SHGetPropertyStoreForWindow(hwnd, typeof(IPropertyStore).GUID, out var store) != 0 || store == null)
            {
                return null;
            }

            try
            {
                if (store.GetValue(ref _appUserModelIdKey, out var value) != 0)
                {
                    return null;
                }

                string? result = value.vt == VT_LPWSTR ? Marshal.PtrToStringUni(value.pointer) : null;
                PropVariantClear(ref value);
                return string.IsNullOrEmpty(result) ? null : result;
            }
            finally
            {
                Marshal.ReleaseComObject(store);
            }
        }
        catch
        {
            return null;
        }
    }
}

/// <summary>Иконка, которую окно показывает само (как на панели задач) — запасной вариант для любых приложений.</summary>
public static class WindowIcon
{
    private const uint WM_GETICON = 0x007F;
    private const int ICON_BIG = 1;
    private const int ICON_SMALL2 = 2;
    private const int GCLP_HICON = -14;
    private const uint SMTO_ABORTIFHUNG = 0x0002;

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr SendMessageTimeout(
        IntPtr hwnd, uint message, IntPtr wParam, IntPtr lParam, uint flags, uint timeout, out IntPtr result);

    [DllImport("user32.dll", EntryPoint = "GetClassLongPtrW")]
    private static extern IntPtr GetClassLongPtr(IntPtr hwnd, int index);

    public static System.Windows.Media.Imaging.BitmapSource? Get(IntPtr hwnd)
    {
        try
        {
            IntPtr icon = Ask(hwnd, ICON_BIG);
            if (icon == IntPtr.Zero)
            {
                icon = GetClassLongPtr(hwnd, GCLP_HICON);
            }

            if (icon == IntPtr.Zero)
            {
                icon = Ask(hwnd, ICON_SMALL2);
            }

            if (icon == IntPtr.Zero)
            {
                return null;
            }

            // Иконка принадлежит окну — копируем, но не уничтожаем
            var bitmap = System.Windows.Interop.Imaging.CreateBitmapSourceFromHIcon(
                icon, System.Windows.Int32Rect.Empty, System.Windows.Media.Imaging.BitmapSizeOptions.FromEmptyOptions());
            bitmap.Freeze();
            return bitmap;
        }
        catch
        {
            return null;
        }
    }

    private static IntPtr Ask(IntPtr hwnd, int kind) =>
        SendMessageTimeout(hwnd, WM_GETICON, (IntPtr)kind, IntPtr.Zero, SMTO_ABORTIFHUNG, 200, out IntPtr icon) != IntPtr.Zero
            ? icon
            : IntPtr.Zero;
}
