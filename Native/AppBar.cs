using System.Windows.Interop;

namespace WinToMac.Native;

public enum AppBarEdge : uint
{
    Top = 1,
    Bottom = 3,
}

/// <summary>
/// Регистрация окна как AppBar: система резервирует под него полосу экрана,
/// и развёрнутые окна её не перекрывают.
/// </summary>
public sealed class AppBar : IDisposable
{
    private const uint ABM_NEW = 0;
    private const uint ABM_REMOVE = 1;
    private const uint ABM_QUERYPOS = 2;
    private const uint ABM_SETPOS = 3;
    private const int ABN_POSCHANGED = 1;
    private const int ABN_FULLSCREENAPP = 2;

    private readonly IntPtr _hwnd;
    private readonly AppBarEdge _edge;
    private readonly uint _callbackMessage;
    private int _reservedSize;
    private bool _registered;

    /// <summary>Вызывается со значением true, когда на основном мониторе открылось полноэкранное приложение.</summary>
    public event Action<bool>? FullScreenChanged;

    /// <summary>Вызывается, когда система просит пересчитать положение (сменилось разрешение и т.п.).</summary>
    public event Action? PositionChanged;

    public AppBar(HwndSource source, AppBarEdge edge)
    {
        _hwnd = source.Handle;
        _edge = edge;
        _callbackMessage = NativeMethods.RegisterWindowMessage("winToMacAppBarMessage_" + edge);
        source.AddHook(WndProc);
    }

    /// <summary>Зарезервировать полосу высотой sizePx физических пикселей (0 — ничего не резервировать).</summary>
    public void Reserve(int sizePx)
    {
        _reservedSize = sizePx;

        if (!_registered)
        {
            var data = CreateData();
            data.uCallbackMessage = _callbackMessage;
            NativeMethods.SHAppBarMessage(ABM_NEW, ref data);
            _registered = true;
        }

        var (width, height) = NativeMethods.GetPrimaryScreenSize();
        var bar = CreateData();
        bar.uEdge = (uint)_edge;
        bar.rc.Left = 0;
        bar.rc.Right = width;

        if (_edge == AppBarEdge.Top)
        {
            bar.rc.Top = 0;
            bar.rc.Bottom = sizePx;
        }
        else
        {
            bar.rc.Top = height - sizePx;
            bar.rc.Bottom = height;
        }

        NativeMethods.SHAppBarMessage(ABM_QUERYPOS, ref bar);

        // QUERYPOS мог сдвинуть одну из сторон — восстанавливаем нужную толщину полосы
        if (_edge == AppBarEdge.Top)
        {
            bar.rc.Bottom = bar.rc.Top + sizePx;
        }
        else
        {
            bar.rc.Top = bar.rc.Bottom - sizePx;
        }

        NativeMethods.SHAppBarMessage(ABM_SETPOS, ref bar);
    }

    public void Dispose()
    {
        if (!_registered)
        {
            return;
        }

        var data = CreateData();
        NativeMethods.SHAppBarMessage(ABM_REMOVE, ref data);
        _registered = false;
    }

    private APPBARDATA CreateData() => new()
    {
        cbSize = System.Runtime.InteropServices.Marshal.SizeOf<APPBARDATA>(),
        hWnd = _hwnd,
    };

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (_registered && msg == _callbackMessage)
        {
            switch (wParam.ToInt32())
            {
                case ABN_POSCHANGED:
                    Reserve(_reservedSize);
                    PositionChanged?.Invoke();
                    break;
                case ABN_FULLSCREENAPP:
                    FullScreenChanged?.Invoke(lParam != IntPtr.Zero);
                    break;
            }

            handled = true;
        }

        return IntPtr.Zero;
    }
}
