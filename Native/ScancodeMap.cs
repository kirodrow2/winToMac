using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace WinToMac.Native;

/// <summary>
/// Переназначения клавиш в реестре (Scancode Map). Нужно, чтобы понять, во что превращён Caps Lock:
/// многие отключают его или делают из него F13 — тогда смену языка надо вешать на F13.
/// </summary>
public static class ScancodeMap
{
    private const ushort CapsLockScanCode = 0x3A;
    private const uint MAPVK_VSC_TO_VK_EX = 3;

    [DllImport("user32.dll")]
    private static extern uint MapVirtualKey(uint code, uint mapType);

    private const string KeyPath = @"SYSTEM\CurrentControlSet\Control\Keyboard Layout";
    private const string ValueName = "Scancode Map";

    /// <summary>Скан-код F13 — клавиша, которой нет на обычных клавиатурах: на неё удобно повесить смену языка.</summary>
    public const ushort F13ScanCode = 0x0064;

    /// <summary>Аргументы командной строки для запуска с правами администратора.</summary>
    public const string CapsToF13Argument = "--caps-lock-to-f13";
    public const string CapsRestoreArgument = "--caps-lock-restore";

    private static ushort? _capsLockReplacement;
    private static bool _isRead;

    /// <summary>
    /// VK, который выдаёт физический Caps Lock: null — не переназначен, 0 — отключён.
    /// Значение из реестра начинает действовать только после перезагрузки.
    /// </summary>
    public static ushort? CapsLockReplacement
    {
        get
        {
            if (!_isRead)
            {
                _capsLockReplacement = ReadCapsLockReplacement();
                _isRead = true;
            }

            return _capsLockReplacement;
        }
    }

    /// <summary>Перечитать реестр (после того как переназначение поменяли).</summary>
    public static void Refresh() => _isRead = false;

    /// <summary>
    /// Переназначить Caps Lock на другую клавишу (или убрать переназначение, если newScanCode = null).
    /// Пишет в HKLM, поэтому вызывается только из копии программы, запущенной от администратора.
    /// Остальные записи Scancode Map (если пользователь переназначал что-то ещё) сохраняются.
    /// </summary>
    public static void SetCapsLock(ushort? newScanCode)
    {
        using var key = Registry.LocalMachine.CreateSubKey(KeyPath, writable: true);
        var entries = new List<(ushort New, ushort Old)>();
        if (key.GetValue(ValueName) is byte[] data && data.Length >= 16)
        {
            int count = BitConverter.ToInt32(data, 8);
            for (int index = 0; index < count - 1 && 12 + index * 4 + 4 <= data.Length; index++)
            {
                ushort newCode = BitConverter.ToUInt16(data, 12 + index * 4);
                ushort oldCode = BitConverter.ToUInt16(data, 14 + index * 4);
                if (oldCode != CapsLockScanCode)
                {
                    entries.Add((newCode, oldCode));
                }
            }
        }

        if (newScanCode is ushort code)
        {
            entries.Add((code, CapsLockScanCode));
        }

        if (entries.Count == 0)
        {
            key.DeleteValue(ValueName, throwOnMissingValue: false);
            return;
        }

        // Заголовок (8 нулевых байт), число записей с завершающей, пары «новый, старый», завершающий ноль
        var bytes = new List<byte>(new byte[8]);
        bytes.AddRange(BitConverter.GetBytes(entries.Count + 1));
        foreach (var (newCode, oldCode) in entries)
        {
            bytes.AddRange(BitConverter.GetBytes(newCode));
            bytes.AddRange(BitConverter.GetBytes(oldCode));
        }

        bytes.AddRange(new byte[4]);
        key.SetValue(ValueName, bytes.ToArray(), RegistryValueKind.Binary);
    }

    private static ushort? ReadCapsLockReplacement()
    {
        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(KeyPath);
            if (key?.GetValue(ValueName) is not byte[] data || data.Length < 16)
            {
                return null;
            }

            // Заголовок 8 байт, затем число записей (включая завершающую нулевую), затем пары «новый, старый»
            int count = BitConverter.ToInt32(data, 8);
            for (int index = 0; index < count - 1 && 12 + index * 4 + 4 <= data.Length; index++)
            {
                ushort newCode = BitConverter.ToUInt16(data, 12 + index * 4);
                ushort oldCode = BitConverter.ToUInt16(data, 14 + index * 4);
                if (oldCode != CapsLockScanCode)
                {
                    continue;
                }

                if (newCode == 0)
                {
                    return 0;
                }

                // Расширенные скан-коды хранятся как E0xx
                uint scanCode = (newCode & 0xFF00) == 0xE000 ? (uint)(0xE000 | (newCode & 0xFF)) : newCode;
                return (ushort)MapVirtualKey(scanCode, MAPVK_VSC_TO_VK_EX);
            }
        }
        catch
        {
            // Нет доступа к реестру — считаем, что ничего не переназначено
        }

        return null;
    }
}
