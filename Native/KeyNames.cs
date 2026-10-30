using System.Runtime.InteropServices;
using System.Text;
using WinToMac.Localization;
using WinToMac.Settings;

namespace WinToMac.Native;

/// <summary>Человекочитаемые имена клавиш.</summary>
public static class KeyNames
{
    [DllImport("user32.dll")]
    private static extern uint MapVirtualKey(uint code, uint mapType);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetKeyNameText(int lParam, StringBuilder name, int size);

    private static readonly Dictionary<ushort, string> Names = new()
    {
        [0x08] = "Backspace", [0x09] = "Tab", [0x0D] = "Enter", [0x13] = "Pause", [0x14] = "Caps Lock",
        [0x1B] = "Esc", [0x20] = "Пробел", [0x21] = "Page Up", [0x22] = "Page Down", [0x23] = "End",
        [0x24] = "Home", [0x25] = "←", [0x26] = "↑", [0x27] = "→", [0x28] = "↓", [0x2C] = "Print Screen",
        [0x2D] = "Insert", [0x2E] = "Delete", [0x5D] = "Menu",
        [0x10] = "Shift", [0x11] = "Ctrl", [0x12] = "Alt",
        [0x5B] = "Win (левый)", [0x5C] = "Win (правый)",
        [0xA0] = "Shift (левый)", [0xA1] = "Shift (правый)",
        [0xA2] = "Ctrl (левый)", [0xA3] = "Ctrl (правый)",
        [0xA4] = "Alt (левый)", [0xA5] = "Alt (правый)",
        [0xC0] = "`", [0xBD] = "-", [0xBB] = "=", [0xDB] = "[", [0xDD] = "]", [0xDC] = "\\",
        [0xBA] = ";", [0xDE] = "'", [0xBC] = ",", [0xBE] = ".", [0xBF] = "/",
    };

    public static string Get(ushort vk)
    {
        if (Names.TryGetValue(vk, out string? name))
        {
            return L.T(name);
        }

        if (vk is >= 0x30 and <= 0x39 or >= 0x41 and <= 0x5A)
        {
            return ((char)vk).ToString();
        }

        if (vk is >= 0x70 and <= 0x87)
        {
            return "F" + (vk - 0x6F);
        }

        if (vk is >= 0x60 and <= 0x69)
        {
            return "Num " + (vk - 0x60);
        }

        uint scanCode = MapVirtualKey(vk, 0);
        var builder = new StringBuilder(64);
        if (scanCode != 0 && GetKeyNameText((int)(scanCode << 16), builder, builder.Capacity) > 0)
        {
            return builder.ToString();
        }

        return $"0x{vk:X2}";
    }

    /// <summary>К какому модификатору относится клавиша (None — не модификатор).</summary>
    public static KeyModifiers ModifierOf(ushort vk) => vk switch
    {
        0x11 or 0xA2 or 0xA3 => KeyModifiers.Ctrl,
        0x12 or 0xA4 or 0xA5 => KeyModifiers.Alt,
        0x10 or 0xA0 or 0xA1 => KeyModifiers.Shift,
        0x5B or 0x5C => KeyModifiers.Win,
        _ => KeyModifiers.None,
    };

    /// <summary>Левая клавиша модификатора — её нажимаем, когда отправляем сочетание.</summary>
    public static ushort LeftVkOf(KeyModifiers modifier) => modifier switch
    {
        KeyModifiers.Ctrl => 0xA2,
        KeyModifiers.Alt => 0xA4,
        KeyModifiers.Shift => 0xA0,
        KeyModifiers.Win => 0x5B,
        _ => 0,
    };
}
