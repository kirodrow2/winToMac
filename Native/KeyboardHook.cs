using System.Runtime.InteropServices;

namespace WinToMac.Native;

/// <summary>Одно событие клавиатуры из низкоуровневого хука.</summary>
public readonly struct KeyEvent
{
    public ushort Vk { get; init; }
    public bool IsDown { get; init; }

    /// <summary>Нажатие отправлено программой, а не клавиатурой (так приходит ввод от Sunshine/Moonlight).</summary>
    public bool IsInjected { get; init; }

    /// <summary>Нажатие отправил сам winToMac — такие не трогаем.</summary>
    public bool IsOwn { get; init; }
}

/// <summary>
/// Низкоуровневый хук клавиатуры (WH_KEYBOARD_LL) в отдельном потоке со своим циклом сообщений:
/// если UI-поток задумается, Windows не снимет хук по таймауту и ввод не будет тормозить.
/// </summary>
public sealed class KeyboardHook : IDisposable
{
    private const int WH_KEYBOARD_LL = 13;
    private const int WM_KEYDOWN = 0x0100;
    private const int WM_SYSKEYDOWN = 0x0104;
    private const uint LLKHF_INJECTED = 0x10;
    private const uint WM_QUIT = 0x0012;

    [StructLayout(LayoutKind.Sequential)]
    private struct KBDLLHOOKSTRUCT
    {
        public uint vkCode;
        public uint scanCode;
        public uint flags;
        public uint time;
        public UIntPtr dwExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MSG
    {
        public IntPtr hwnd;
        public uint message;
        public IntPtr wParam;
        public IntPtr lParam;
        public uint time;
        public POINT pt;
        public uint lPrivate;
    }

    private delegate IntPtr HookProc(int code, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SetWindowsHookEx(int hookId, HookProc proc, IntPtr module, uint threadId);

    [DllImport("user32.dll")]
    private static extern bool UnhookWindowsHookEx(IntPtr hook);

    [DllImport("user32.dll")]
    private static extern IntPtr CallNextHookEx(IntPtr hook, int code, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern int GetMessage(out MSG msg, IntPtr hwnd, uint min, uint max);

    [DllImport("user32.dll")]
    private static extern bool PostThreadMessage(uint threadId, uint msg, IntPtr wParam, IntPtr lParam);

    [DllImport("kernel32.dll")]
    private static extern uint GetCurrentThreadId();

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr GetModuleHandle(string? name);

    private readonly Func<KeyEvent, bool> _handler;
    // Делегат держим в поле, иначе сборщик мусора уберёт его из-под Windows
    private readonly HookProc _proc;
    private readonly Thread _thread;
    private uint _threadId;
    private IntPtr _hook;

    /// <param name="handler">Возвращает true, если событие нужно «съесть» и не пускать дальше.</param>
    public KeyboardHook(Func<KeyEvent, bool> handler)
    {
        _handler = handler;
        _proc = HookCallback;
        _thread = new Thread(Run) { IsBackground = true, Name = "winToMac keyboard hook" };
        _thread.Start();
    }

    public void Dispose()
    {
        if (_threadId != 0)
        {
            PostThreadMessage(_threadId, WM_QUIT, IntPtr.Zero, IntPtr.Zero);
        }
    }

    private void Run()
    {
        _threadId = GetCurrentThreadId();
        _hook = SetWindowsHookEx(WH_KEYBOARD_LL, _proc, GetModuleHandle(null), 0);
        while (GetMessage(out _, IntPtr.Zero, 0, 0) > 0)
        {
            // Сообщений у потока нет — цикл нужен только для того, чтобы Windows вызывала хук
        }

        UnhookWindowsHookEx(_hook);
    }

    private IntPtr HookCallback(int code, IntPtr wParam, IntPtr lParam)
    {
        if (code >= 0)
        {
            var data = Marshal.PtrToStructure<KBDLLHOOKSTRUCT>(lParam);
            int message = wParam.ToInt32();
            var keyEvent = new KeyEvent
            {
                Vk = (ushort)data.vkCode,
                IsDown = message is WM_KEYDOWN or WM_SYSKEYDOWN,
                IsInjected = (data.flags & LLKHF_INJECTED) != 0,
                IsOwn = data.dwExtraInfo == InputSender.Signature,
            };

            try
            {
                if (!keyEvent.IsOwn && _handler(keyEvent))
                {
                    return new IntPtr(1);
                }
            }
            catch
            {
                // Ошибка в обработчике не должна блокировать клавиатуру
            }
        }

        return CallNextHookEx(_hook, code, wParam, lParam);
    }
}
