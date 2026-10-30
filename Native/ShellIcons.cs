using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace WinToMac.Native;

/// <summary>
/// Большие иконки через IShellItemImageFactory — один механизм для exe, ярлыков,
/// UWP-приложений (shell:AppsFolder\...) и спецпапок вроде корзины.
/// </summary>
public static class ShellIcons
{
    public const string RecycleBin = "::{645FF040-5081-101B-9F08-00AA002F954E}";

    private const int SIIGBF_BIGGERSIZEOK = 0x1;
    private const int SIIGBF_ICONONLY = 0x4;

    private static readonly ConcurrentDictionary<string, BitmapSource?> Cache = new();

    [StructLayout(LayoutKind.Sequential)]
    private struct SIZE
    {
        public int cx, cy;
    }

    [ComImport]
    [Guid("bcc18b79-ba16-442f-80c4-8a59c30c463b")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IShellItemImageFactory
    {
        [PreserveSig]
        int GetImage(SIZE size, int flags, out IntPtr bitmap);
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct BITMAP
    {
        public int bmType;
        public int bmWidth;
        public int bmHeight;
        public int bmWidthBytes;
        public ushort bmPlanes;
        public ushort bmBitsPixel;
        public IntPtr bmBits;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct BITMAPINFOHEADER
    {
        public uint biSize;
        public int biWidth;
        public int biHeight;
        public ushort biPlanes;
        public ushort biBitCount;
        public uint biCompression;
        public uint biSizeImage;
        public int biXPelsPerMeter;
        public int biYPelsPerMeter;
        public uint biClrUsed;
        public uint biClrImportant;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct DIBSECTION
    {
        public BITMAP dsBm;
        public BITMAPINFOHEADER dsBmih;
        public uint dsBitfield0, dsBitfield1, dsBitfield2;
        public IntPtr dshSection;
        public uint dsOffset;
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode, PreserveSig = true)]
    private static extern int SHCreateItemFromParsingName(
        string path,
        IntPtr bindContext,
        [MarshalAs(UnmanagedType.LPStruct)] Guid riid,
        [MarshalAs(UnmanagedType.Interface)] out IShellItemImageFactory factory);

    [DllImport("gdi32.dll")]
    private static extern int GetObject(IntPtr handle, int size, ref DIBSECTION dib);

    /// <summary>Иконка по пути/shell-имени. Возвращает null, если оболочка её не отдала.</summary>
    public static BitmapSource? Get(string source, int size = 256)
    {
        if (string.IsNullOrWhiteSpace(source))
        {
            return null;
        }

        return GetCached(source + "|" + size, () => Load(source, size, SIIGBF_ICONONLY | SIIGBF_BIGGERSIZEOK));
    }

    public static Task<BitmapSource?> GetAsync(string source, int size = 256) =>
        RunOnShellThread(() => Get(source, size));

    /// <summary>Миниатюра содержимого файла (кадр видео, картинка), а не значок типа файла.</summary>
    public static Task<BitmapSource?> GetThumbnailAsync(string path, int size = 320) =>
        RunOnShellThread(() => GetCached("thumb|" + path + "|" + size, () => Load(path, size, SIIGBF_BIGGERSIZEOK)));

    /// <summary>
    /// Неудачу не запоминаем: сразу после входа в систему оболочка часто ещё не готова отдать иконку,
    /// и закешированный null оставлял значок в доке пустым до перезапуска.
    /// </summary>
    private static BitmapSource? GetCached(string key, Func<BitmapSource?> load)
    {
        if (Cache.TryGetValue(key, out var cached))
        {
            return cached;
        }

        var loaded = load();
        if (loaded != null)
        {
            Cache[key] = loaded;
        }

        return loaded;
    }

    private static readonly System.Collections.Concurrent.BlockingCollection<Action> ShellQueue = StartShellThreads();

    /// <summary>
    /// Shell-COM (IShellItemImageFactory) надёжно работает только в STA: из пула потоков (MTA)
    /// часть иконок молча не грузится. Поэтому держим пару собственных STA-потоков с очередью.
    /// </summary>
    private static System.Collections.Concurrent.BlockingCollection<Action> StartShellThreads()
    {
        var queue = new System.Collections.Concurrent.BlockingCollection<Action>();
        for (int i = 0; i < 2; i++)
        {
            var thread = new Thread(() =>
            {
                foreach (var work in queue.GetConsumingEnumerable())
                {
                    work();
                }
            })
            {
                IsBackground = true,
                Name = "winToMac shell icons",
            };
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
        }

        return queue;
    }

    private static Task<BitmapSource?> RunOnShellThread(Func<BitmapSource?> load)
    {
        var completion = new TaskCompletionSource<BitmapSource?>(TaskCreationOptions.RunContinuationsAsynchronously);
        ShellQueue.Add(() =>
        {
            try
            {
                completion.SetResult(load());
            }
            catch
            {
                completion.SetResult(null);
            }
        });
        return completion.Task;
    }

    private static BitmapSource? Load(string source, int size, int flags)
    {
        try
        {
            int hr = SHCreateItemFromParsingName(
                source, IntPtr.Zero, typeof(IShellItemImageFactory).GUID, out var factory);
            if (hr != 0 || factory == null)
            {
                return null;
            }

            try
            {
                hr = factory.GetImage(
                    new SIZE { cx = size, cy = size }, flags, out IntPtr hbitmap);
                if (hr != 0 || hbitmap == IntPtr.Zero)
                {
                    return null;
                }

                try
                {
                    return ToBitmapSource(hbitmap, isThumbnail: (flags & SIIGBF_ICONONLY) == 0);
                }
                finally
                {
                    NativeMethods.DeleteObject(hbitmap);
                }
            }
            finally
            {
                Marshal.ReleaseComObject(factory);
            }
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Imaging.CreateBitmapSourceFromHBitmap теряет альфа-канал, поэтому копируем пиксели DIB вручную.
    /// </summary>
    private static BitmapSource? ToBitmapSource(IntPtr hbitmap, bool isThumbnail)
    {
        var dib = new DIBSECTION();
        if (GetObject(hbitmap, Marshal.SizeOf<DIBSECTION>(), ref dib) == 0 || dib.dsBm.bmBits == IntPtr.Zero)
        {
            return null;
        }

        int width = dib.dsBm.bmWidth;
        int height = dib.dsBm.bmHeight;
        int stride = dib.dsBm.bmWidthBytes;
        if (dib.dsBm.bmBitsPixel != 32)
        {
            return null;
        }

        var pixels = new byte[stride * height];
        // Значки приходят снизу вверх, а миниатюры (кадры видео, картинки) — уже сверху вниз,
        // хотя заголовок у обоих одинаковый
        bool bottomUp = dib.dsBmih.biHeight > 0 && !isThumbnail;
        for (int row = 0; row < height; row++)
        {
            int sourceRow = bottomUp ? height - 1 - row : row;
            Marshal.Copy(dib.dsBm.bmBits + sourceRow * stride, pixels, row * stride, stride);
        }

        // Старые иконки без альфа-канала приходят полностью «прозрачными» — делаем их непрозрачными
        bool hasAlpha = false;
        for (int i = 3; i < pixels.Length; i += 4)
        {
            if (pixels[i] != 0)
            {
                hasAlpha = true;
                break;
            }
        }

        if (!hasAlpha)
        {
            for (int i = 3; i < pixels.Length; i += 4)
            {
                pixels[i] = 255;
            }
        }

        var bitmap = BitmapSource.Create(width, height, 96, 96, PixelFormats.Pbgra32, null, pixels, stride);
        bitmap.Freeze();
        return bitmap;
    }
}
