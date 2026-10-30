using System.Runtime.InteropServices;

namespace WinToMac.Native;

/// <summary>
/// Программные нажатия клавиш. Все они помечаются <see cref="Signature"/>, чтобы перехватчик
/// клавиатуры отличал их от ввода через Moonlight/Sunshine (тот тоже приходит как «программный»).
/// </summary>
public static class InputSender
{
    /// <summary>"MACS" — метка в dwExtraInfo.</summary>
    public static readonly UIntPtr Signature = new(0x4D414353);

    public const ushort VK_BACK = 0x08;
    public const ushort VK_TAB = 0x09;
    public const ushort VK_SHIFT = 0x10;
    public const ushort VK_CONTROL = 0x11;
    public const ushort VK_MENU = 0x12;
    public const ushort VK_CAPITAL = 0x14;
    public const ushort VK_ESCAPE = 0x1B;
    public const ushort VK_SPACE = 0x20;
    public const ushort VK_END = 0x23;
    public const ushort VK_HOME = 0x24;
    public const ushort VK_LEFT = 0x25;
    public const ushort VK_UP = 0x26;
    public const ushort VK_RIGHT = 0x27;
    public const ushort VK_DOWN = 0x28;
    public const ushort VK_SNAPSHOT = 0x2C;
    public const ushort VK_DELETE = 0x2E;
    public const ushort VK_LWIN = 0x5B;
    public const ushort VK_RWIN = 0x5C;
    public const ushort VK_LSHIFT = 0xA0;
    public const ushort VK_RSHIFT = 0xA1;
    public const ushort VK_LCONTROL = 0xA2;
    public const ushort VK_RCONTROL = 0xA3;
    public const ushort VK_LMENU = 0xA4;
    public const ushort VK_RMENU = 0xA5;

    private const uint INPUT_KEYBOARD = 1;
    private const uint KEYEVENTF_EXTENDEDKEY = 0x1;
    private const uint KEYEVENTF_KEYUP = 0x2;

    [StructLayout(LayoutKind.Sequential)]
    private struct MOUSEINPUT
    {
        public int dx, dy;
        public uint mouseData, dwFlags, time;
        public UIntPtr dwExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct KEYBDINPUT
    {
        public ushort wVk;
        public ushort wScan;
        public uint dwFlags;
        public uint time;
        public UIntPtr dwExtraInfo;
    }

    [StructLayout(LayoutKind.Explicit)]
    private struct InputUnion
    {
        [FieldOffset(0)] public MOUSEINPUT mi;
        [FieldOffset(0)] public KEYBDINPUT ki;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct INPUT
    {
        public uint type;
        public InputUnion u;
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint SendInput(uint count, INPUT[] inputs, int size);

    [DllImport("user32.dll")]
    private static extern uint MapVirtualKey(uint code, uint mapType);

    /// <summary>Нажатие (down/up) одной или нескольких клавиш — одним вызовом, без разрывов.</summary>
    public static void Send(params (ushort Vk, bool Up)[] keys)
    {
        var inputs = keys.Select(key => CreateInput(key.Vk, key.Up)).ToArray();
        SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<INPUT>());
    }

    public static void Down(ushort vk) => Send((vk, false));

    public static void Up(ushort vk) => Send((vk, true));

    /// <summary>Сочетание: модификаторы зажимаются по порядку, отпускаются в обратном.</summary>
    public static void Chord(params ushort[] keys)
    {
        var sequence = keys.Select(vk => (vk, false))
            .Concat(keys.Reverse().Select(vk => (vk, true)))
            .ToArray();
        Send(sequence);
    }

    private static INPUT CreateInput(ushort vk, bool up)
    {
        uint flags = up ? KEYEVENTF_KEYUP : 0;
        if (IsExtended(vk))
        {
            flags |= KEYEVENTF_EXTENDEDKEY;
        }

        return new INPUT
        {
            type = INPUT_KEYBOARD,
            u = new InputUnion
            {
                ki = new KEYBDINPUT
                {
                    wVk = vk,
                    // Скан-код нужен терминалам и играм, которые смотрят на него, а не на VK
                    wScan = (ushort)MapVirtualKey(vk, 0),
                    dwFlags = flags,
                    dwExtraInfo = Signature,
                },
            },
        };
    }

    private static bool IsExtended(ushort vk) => vk is VK_LEFT or VK_RIGHT or VK_UP or VK_DOWN
        or VK_HOME or VK_END or 0x21 or 0x22 or 0x2D or VK_DELETE
        or VK_LWIN or VK_RWIN or VK_RCONTROL or VK_RMENU or VK_SNAPSHOT;
}
