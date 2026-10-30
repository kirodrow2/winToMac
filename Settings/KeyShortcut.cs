using System.Text.Json.Serialization;
using WinToMac.Native;
using WinToMac.Localization;

namespace WinToMac.Settings;

[Flags]
public enum KeyModifiers
{
    None = 0,
    Ctrl = 1,
    Alt = 2,
    Shift = 4,
    Win = 8,
}

/// <summary>Сочетание: модификаторы (без различия левый/правый) + основная клавиша.</summary>
public sealed class KeyShortcut
{
    public KeyModifiers Modifiers { get; set; }

    /// <summary>
    /// Основная клавиша (VK). Может быть и модификатором — тогда это сочетание из одних модификаторов,
    /// например Win+Shift (Modifiers = Win, Key = левый Shift).
    /// </summary>
    public ushort Key { get; set; }

    [JsonIgnore]
    public bool IsEmpty => Key == 0;

    public override string ToString()
    {
        if (IsEmpty)
        {
            return L.T("не задано");
        }

        var parts = new List<string>();
        var keyGroup = KeyNames.ModifierOf(Key);
        foreach (var modifier in new[] { KeyModifiers.Ctrl, KeyModifiers.Alt, KeyModifiers.Shift, KeyModifiers.Win })
        {
            if (Modifiers.HasFlag(modifier) && modifier != keyGroup)
            {
                parts.Add(modifier.ToString());
            }
        }

        parts.Add(KeyNames.Get(Key));
        return string.Join(" + ", parts);
    }
}

public enum MappingAction
{
    /// <summary>Нажать другое сочетание.</summary>
    Shortcut,
    SwitchLanguage,
    OpenLauncher,
    ShowDesktop,
    ToggleCapsLock,

    /// <summary>Ничего не делать (отключить клавишу).</summary>
    Disable,
}

/// <summary>Правило переназначения, как в PowerToys Keyboard Manager.</summary>
public sealed class KeyMapping
{
    public KeyShortcut From { get; set; } = new();
    public MappingAction Action { get; set; } = MappingAction.Shortcut;
    public KeyShortcut To { get; set; } = new();

    /// <summary>Только в этой программе (имя exe); пусто — везде.</summary>
    public string? App { get; set; }
}
