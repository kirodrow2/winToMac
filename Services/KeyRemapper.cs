using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using WinToMac.Native;
using WinToMac.Settings;
using static WinToMac.Native.InputSender;

namespace WinToMac.Services;

public enum InputSource
{
    /// <summary>Клавиатура этого ПК.</summary>
    Local,

    /// <summary>Программный ввод — так приходят нажатия с Mac через Moonlight/Sunshine.</summary>
    Remote,
}

/// <summary>
/// Сочетания клавиш как на Mac.
///
/// ⌘ работает как зажатый Ctrl (поэтому ⌘+клик, ⌘+C и прочее работают везде), а несколько
/// сочетаний, у которых на Windows другая клавиша, переводятся отдельно: ⌘Q, ⌘Tab, ⌘←/→ и т.д.
/// ⌥ (Option) «откладывается»: если следом нажата стрелка или Backspace — это переход по словам,
/// иначе клавиша отправляется как есть.
///
/// Источник ввода определяется на каждом нажатии: Sunshine вводит клавиши программно (флаг injected),
/// поэтому с Mac через Moonlight и с клавиатуры ПК могут действовать разные раскладки модификаторов.
///
/// Всё состояние меняется только в потоке хука.
/// </summary>
public static class KeyRemapper
{
    private const long CapsHoldMilliseconds = 450;
    private const uint WM_INPUTLANGCHANGEREQUEST = 0x0050;

    private enum ShortcutResult
    {
        Pass,
        Swallow,
        SwallowDownOnly,
    }

    [DllImport("user32.dll")]
    private static extern short GetAsyncKeyState(int vk);

    private static KeyboardHook? _hook;

    // ⌘
    private static ushort _commandVk;
    private static bool _commandPassThrough;
    private static bool _ctrlHeld;
    private static bool _switcherActive;

    // ⌥
    private static ushort _optionVk;
    private static bool _optionPassThrough;
    private static bool _optionInjected;
    private static bool _optionUsed;

    // Caps Lock
    private static bool _capsActive;
    private static bool _capsToggled;
    private static long _capsDownAt;

    private static bool _physicalCtrl;

    /// <summary>Физически зажатые клавиши (без нажатий, которые отправил сам winToMac).</summary>
    private static readonly HashSet<ushort> PhysicalDown = new();

    /// <summary>Пока задан — идёт запись сочетания в настройках: все нажатия уходят сюда и дальше не идут.</summary>
    private static volatile Action<KeyEvent>? _recorder;

    /// <summary>Клавиши, нажатие которых заменено действием, — их отпускание тоже съедаем.</summary>
    private static readonly HashSet<ushort> SwallowUp = new();

    public static InputSource LastSource { get; private set; } = InputSource.Local;

    /// <summary>Клавиша смены языка: заданная вручную, иначе то, во что Caps Lock превращён в реестре.</summary>
    public static ushort GetLanguageKey(AppSettings settings) =>
        settings.LanguageKey ?? (ScancodeMap.CapsLockReplacement is ushort replacement and not 0 ? replacement : VK_CAPITAL);

    /// <summary>Начать запись сочетания: колбэк вызывается в потоке хука.</summary>
    public static void BeginRecording(Action<KeyEvent> onKey) => _recorder = onKey;

    public static void EndRecording() => _recorder = null;

    public static void Start() => _hook ??= new KeyboardHook(Handle);

    public static void Stop()
    {
        ReleaseAll();
        _hook?.Dispose();
        _hook = null;
    }

    private static bool Handle(KeyEvent e)
    {
        ushort vk = e.Vk;
        bool isRepeat = e.IsDown && PhysicalDown.Contains(vk);
        if (e.IsDown)
        {
            PhysicalDown.Add(vk);
        }
        else
        {
            PhysicalDown.Remove(vk);
        }

        if (_recorder is { } recorder)
        {
            ReleaseAll();
            recorder(e);
            return true;
        }

        var settings = AppSettings.Current;
        if (!settings.MacKeyboard)
        {
            ReleaseAll();
            return false;
        }

        var source = e.IsInjected ? InputSource.Remote : InputSource.Local;
        if (e.IsDown)
        {
            LastSource = source;
        }

        if (!e.IsDown && SwallowUp.Remove(vk))
        {
            return true;
        }

        // Свои правила важнее встроенных «маковских»: пользователь настроил их явно
        if (e.IsDown && TryCustomMapping(vk, isRepeat, settings))
        {
            SwallowUp.Add(vk);
            return true;
        }

        if (vk is VK_LCONTROL or VK_RCONTROL or VK_CONTROL)
        {
            _physicalCtrl = e.IsDown;
        }

        if (vk == GetLanguageKey(settings) && (settings.CapsLockSwitchesLanguage || _capsActive))
        {
            return HandleCapsLock(e, settings);
        }

        if (_commandVk != 0 && vk == _commandVk)
        {
            return HandleCommandKey(e);
        }

        if (_optionVk != 0 && vk == _optionVk)
        {
            return HandleOptionKey(e);
        }

        if (!e.IsDown)
        {
            return false;
        }

        var commandKey = source == InputSource.Remote ? settings.RemoteCommandKey : settings.LocalCommandKey;
        if (_commandVk == 0 && IsCommandKey(vk, commandKey))
        {
            return BeginCommand(vk, settings);
        }

        if (_optionVk == 0 && settings.OptionWordNavigation && IsOptionKey(vk, commandKey))
        {
            return BeginOption(vk, settings);
        }

        if (_commandVk != 0 && !_commandPassThrough)
        {
            switch (TryCommandShortcut(vk, settings))
            {
                case ShortcutResult.Swallow:
                    SwallowUp.Add(vk);
                    return true;
                case ShortcutResult.SwallowDownOnly:
                    return true;
            }
        }

        if (_optionVk != 0 && !_optionPassThrough)
        {
            return HandleKeyWithOption(vk);
        }

        return false;
    }

    // ---------- ⌘ Command ----------

    private static bool IsCommandKey(ushort vk, CommandKey commandKey) => commandKey switch
    {
        CommandKey.Alt => vk == VK_LMENU,
        CommandKey.Win => vk is VK_LWIN or VK_RWIN,
        _ => false,
    };

    /// <summary>⌥ — соседняя с ⌘ клавиша: при ⌘=Alt это Win, иначе Alt.</summary>
    private static bool IsOptionKey(ushort vk, CommandKey commandKey) => commandKey == CommandKey.Alt
        ? vk == VK_LWIN
        : vk == VK_LMENU;

    private static bool BeginCommand(ushort vk, AppSettings settings)
    {
        _commandVk = vk;
        _commandPassThrough = !IsRemapAllowed(settings);
        if (_commandPassThrough)
        {
            return false;
        }

        Down(VK_LCONTROL);
        _ctrlHeld = true;
        return true;
    }

    private static bool HandleCommandKey(KeyEvent e)
    {
        if (_commandPassThrough)
        {
            if (!e.IsDown)
            {
                _commandVk = 0;
            }

            return false;
        }

        if (e.IsDown)
        {
            // Автоповтор зажатой ⌘
            return true;
        }

        if (_switcherActive)
        {
            Up(VK_LMENU);
        }
        else if (_ctrlHeld)
        {
            Up(VK_LCONTROL);
        }

        _switcherActive = false;
        _ctrlHeld = false;
        _commandVk = 0;
        return true;
    }

    private static ShortcutResult TryCommandShortcut(ushort vk, AppSettings settings)
    {
        bool shift = (GetAsyncKeyState(VK_SHIFT) & 0x8000) != 0;

        // ⌘Tab — переключение приложений: держим Alt, пока зажата ⌘
        if (vk == VK_TAB)
        {
            if (_switcherActive)
            {
                return ShortcutResult.Pass;
            }

            Up(VK_LCONTROL);
            _ctrlHeld = false;
            Send((VK_LMENU, false), (VK_TAB, false));
            _switcherActive = true;
            return ShortcutResult.SwallowDownOnly;
        }

        if (_switcherActive)
        {
            return ShortcutResult.Pass;
        }

        switch (vk)
        {
            case (ushort)'Q':
                if (_physicalCtrl)
                {
                    // ⌃⌘Q — заблокировать экран
                    NativeMethods.LockWorkStation();
                }
                else
                {
                    WithoutCtrl(() => Chord(VK_LMENU, 0x73)); // Alt+F4
                }

                return ShortcutResult.Swallow;

            case (ushort)'H':
            case (ushort)'M':
                WindowEnumerator.Minimize(NativeMethods.GetForegroundWindow());
                return ShortcutResult.Swallow;

            case VK_SPACE when settings.CommandSpaceOpensLauncher:
                Application.Current?.Dispatcher.BeginInvoke(() => App.Current.ShowLauncher());
                return ShortcutResult.Swallow;

            // ⌘←/→ — начало/конец строки, ⌘↑/↓ — начало/конец документа (Shift выделяет)
            case VK_LEFT:
                WithoutCtrl(() => Chord(VK_HOME));
                return ShortcutResult.Swallow;
            case VK_RIGHT:
                WithoutCtrl(() => Chord(VK_END));
                return ShortcutResult.Swallow;
            case VK_UP:
                Chord(VK_HOME);
                return ShortcutResult.Swallow;
            case VK_DOWN:
                Chord(VK_END);
                return ShortcutResult.Swallow;

            // ⌘⌫ — удалить до начала строки
            case VK_BACK:
                WithoutCtrl(() => Send(
                    (VK_LSHIFT, false), (VK_HOME, false), (VK_HOME, true), (VK_LSHIFT, true),
                    (VK_BACK, false), (VK_BACK, true)));
                return ShortcutResult.Swallow;

            // ⌘⇧Z — повторить
            case (ushort)'Z' when shift:
                Send((VK_LSHIFT, true), ((ushort)'Y', false), ((ushort)'Y', true), (VK_LSHIFT, false));
                return ShortcutResult.Swallow;

            // ⌘⇧3 — снимок экрана в файл, ⌘⇧4 / ⌘⇧5 — снимок области
            case (ushort)'3' when shift:
                WithoutCtrl(() => Send((VK_LSHIFT, true), (VK_LWIN, false), (VK_SNAPSHOT, false),
                    (VK_SNAPSHOT, true), (VK_LWIN, true), (VK_LSHIFT, false)));
                return ShortcutResult.Swallow;
            case (ushort)'4' when shift:
            case (ushort)'5' when shift:
                WithoutCtrl(() => Send((VK_LWIN, false), ((ushort)'S', false), ((ushort)'S', true), (VK_LWIN, true)));
                return ShortcutResult.Swallow;

            // ⌘⌥Esc — диспетчер задач
            case VK_ESCAPE when _optionVk != 0:
                _optionUsed = true;
                Send((VK_LSHIFT, false), (VK_ESCAPE, false), (VK_ESCAPE, true), (VK_LSHIFT, true));
                return ShortcutResult.Swallow;
        }

        return ShortcutResult.Pass;
    }

    /// <summary>Выполнить действие с временно отпущенным Ctrl (который мы держим за ⌘).</summary>
    private static void WithoutCtrl(Action action)
    {
        if (_ctrlHeld)
        {
            Up(VK_LCONTROL);
        }

        action();

        if (_ctrlHeld)
        {
            Down(VK_LCONTROL);
        }
    }

    // ---------- ⌥ Option ----------

    private static bool BeginOption(ushort vk, AppSettings settings)
    {
        _optionVk = vk;
        _optionPassThrough = !IsRemapAllowed(settings);
        _optionInjected = false;
        _optionUsed = false;
        return !_optionPassThrough;
    }

    private static bool HandleOptionKey(KeyEvent e)
    {
        if (_optionPassThrough)
        {
            if (!e.IsDown)
            {
                _optionVk = 0;
            }

            return false;
        }

        if (e.IsDown)
        {
            return true;
        }

        if (_optionInjected)
        {
            Up(_optionVk);
        }
        else if (!_optionUsed && _optionVk == VK_LWIN)
        {
            // Одиночное нажатие Win по-прежнему открывает «Пуск»
            Chord(VK_LWIN);
        }

        _optionVk = 0;
        return true;
    }

    private static bool HandleKeyWithOption(ushort vk)
    {
        if (!_optionInjected)
        {
            switch (vk)
            {
                // ⌥←/→ — по словам, ⌥⌫ / ⌥Del — удалить слово
                case VK_LEFT:
                case VK_RIGHT:
                case VK_BACK:
                case VK_DELETE:
                    _optionUsed = true;
                    if (_ctrlHeld)
                    {
                        Chord(vk);
                    }
                    else
                    {
                        Send((VK_LCONTROL, false), (vk, false), (vk, true), (VK_LCONTROL, true));
                    }

                    SwallowUp.Add(vk);
                    return true;

                // Win+L программно не работает — блокируем экран сами
                case (ushort)'L' when _optionVk == VK_LWIN:
                    _optionUsed = true;
                    NativeMethods.LockWorkStation();
                    SwallowUp.Add(vk);
                    return true;
            }

            // Любая другая клавиша: ⌥ работает как обычный Alt/Win (Win+E, Win+Shift+S, Alt+Tab…)
            Send((_optionVk, false), (vk, false));
            _optionInjected = true;
            return true;
        }

        return false;
    }

    // ---------- Caps Lock ----------

    private static bool HandleCapsLock(KeyEvent e, AppSettings settings)
    {
        if (e.IsDown)
        {
            if (!_capsActive)
            {
                if (!IsRemapAllowed(settings))
                {
                    return false;
                }

                _capsActive = true;
                _capsToggled = false;
                _capsDownAt = Environment.TickCount64;
                return true;
            }

            // Удержание — обычный Caps Lock, как на Mac
            if (settings.LanguageKeyHoldForCaps && !_capsToggled
                && Environment.TickCount64 - _capsDownAt >= CapsHoldMilliseconds)
            {
                Chord(VK_CAPITAL);
                _capsToggled = true;
            }

            return true;
        }

        if (!_capsActive)
        {
            return false;
        }

        _capsActive = false;
        if (!_capsToggled)
        {
            if (Environment.TickCount64 - _capsDownAt >= CapsHoldMilliseconds)
            {
                if (settings.LanguageKeyHoldForCaps)
                {
                    Chord(VK_CAPITAL);
                }
            }
            else
            {
                SwitchLanguage(settings);
            }
        }

        return true;
    }

    private static void SwitchLanguage(AppSettings settings)
    {
        if (settings.LanguageSwitch == LanguageSwitchMethod.WinSpace)
        {
            SendShortcut(new KeyShortcut { Modifiers = KeyModifiers.Win, Key = VK_SPACE });
            return;
        }

        if (settings.LanguageSwitch == LanguageSwitchMethod.Shortcut && !settings.LanguageShortcut.IsEmpty)
        {
            SendShortcut(settings.LanguageShortcut);
            return;
        }

        // INPUTLANGCHANGE_FORWARD + HKL_NEXT — следующая раскладка из списка
        NativeMethods.PostMessage(NativeMethods.GetForegroundWindow(), WM_INPUTLANGCHANGEREQUEST,
            new IntPtr(2), new IntPtr(1));
    }

    // ---------- Свои правила ----------

    private static bool TryCustomMapping(ushort vk, bool isRepeat, AppSettings settings)
    {
        if (settings.KeyMappings.Count == 0)
        {
            return false;
        }

        var held = GetPhysicalModifiers(KeyNames.ModifierOf(vk));
        string? exeName = null;
        foreach (var mapping in settings.KeyMappings)
        {
            if (mapping.From.IsEmpty || !KeyMatches(mapping.From.Key, vk) || ModifiersWithoutKey(mapping.From) != held)
            {
                continue;
            }

            if (!string.IsNullOrWhiteSpace(mapping.App))
            {
                exeName ??= GetForegroundExeName() ?? "";
                if (!string.Equals(mapping.App.Trim(), exeName, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }
            }

            // Отложенный ⌥ участвовал в правиле — при отпускании он уже не должен открывать «Пуск»
            _optionUsed = true;

            // Автоповтор повторяет только сочетания; действия выполняются один раз
            if (isRepeat && mapping.Action != MappingAction.Shortcut)
            {
                return true;
            }

            Execute(mapping, settings);
            return true;
        }

        return false;
    }

    private static void Execute(KeyMapping mapping, AppSettings settings)
    {
        switch (mapping.Action)
        {
            case MappingAction.Shortcut:
                if (!mapping.To.IsEmpty)
                {
                    SendShortcut(mapping.To);
                }

                break;
            case MappingAction.SwitchLanguage:
                SwitchLanguage(settings);
                break;
            case MappingAction.OpenLauncher:
                Application.Current?.Dispatcher.BeginInvoke(() => App.Current.ShowLauncher());
                break;
            case MappingAction.ShowDesktop:
                Application.Current?.Dispatcher.BeginInvoke(() => App.Current.ShowDesktop());
                break;
            case MappingAction.ToggleCapsLock:
                Chord(VK_CAPITAL);
                break;
        }
    }

    /// <summary>Левая/правая версия совпадает с «общей» (Shift записан — подходит любой Shift).</summary>
    private static bool KeyMatches(ushort expected, ushort actual) => expected == actual
        || expected == VK_SHIFT && actual is VK_LSHIFT or VK_RSHIFT
        || expected == VK_CONTROL && actual is VK_LCONTROL or VK_RCONTROL
        || expected == VK_MENU && actual is VK_LMENU or VK_RMENU;

    /// <summary>Модификаторы сочетания без группы самой клавиши (у «Win + Shift» клавиша — Shift).</summary>
    private static KeyModifiers ModifiersWithoutKey(KeyShortcut shortcut) =>
        shortcut.Modifiers & ~KeyNames.ModifierOf(shortcut.Key);

    private static KeyModifiers GetPhysicalModifiers(KeyModifiers exclude)
    {
        var result = KeyModifiers.None;
        foreach (ushort down in PhysicalDown)
        {
            result |= KeyNames.ModifierOf(down);
        }

        return result & ~exclude;
    }

    /// <summary>
    /// Отправить сочетание «начисто»: лишние зажатые сейчас модификаторы (в том числе Ctrl,
    /// который winToMac держит за ⌘) на время отпускаются, а потом возвращаются.
    /// </summary>
    private static void SendShortcut(KeyShortcut shortcut)
    {
        var target = shortcut.Modifiers | KeyNames.ModifierOf(shortcut.Key);
        ushort[] modifierVks = { VK_LCONTROL, VK_RCONTROL, VK_LMENU, VK_RMENU, VK_LSHIFT, VK_RSHIFT, VK_LWIN, VK_RWIN };
        var heldExtra = modifierVks
            .Where(modifierVk => (GetAsyncKeyState(modifierVk) & 0x8000) != 0 && !target.HasFlag(KeyNames.ModifierOf(modifierVk)))
            .ToList();

        var toPress = new List<ushort>();
        foreach (var modifier in new[] { KeyModifiers.Ctrl, KeyModifiers.Alt, KeyModifiers.Shift, KeyModifiers.Win })
        {
            ushort modifierVk = KeyNames.LeftVkOf(modifier);
            if (shortcut.Modifiers.HasFlag(modifier) && KeyNames.ModifierOf(shortcut.Key) != modifier
                && (GetAsyncKeyState(modifierVk) & 0x8000) == 0)
            {
                toPress.Add(modifierVk);
            }
        }

        var sequence = new List<(ushort, bool)>();
        sequence.AddRange(heldExtra.Select(modifierVk => (modifierVk, true)));
        sequence.AddRange(toPress.Select(modifierVk => (modifierVk, false)));
        sequence.Add((shortcut.Key, false));
        sequence.Add((shortcut.Key, true));
        sequence.AddRange(Enumerable.Reverse(toPress).Select(modifierVk => (modifierVk, true)));
        sequence.AddRange(heldExtra.Select(modifierVk => (modifierVk, false)));
        Send(sequence.ToArray());
    }

    // ---------- Общее ----------

    private static string? GetForegroundExeName()
    {
        NativeMethods.GetWindowThreadProcessId(NativeMethods.GetForegroundWindow(), out uint processId);
        string? path = NativeMethods.GetProcessPath(processId);
        return path == null ? null : Path.GetFileName(path);
    }

    /// <summary>В полноэкранных играх и программах из списка исключений клавиши не трогаем.</summary>
    private static bool IsRemapAllowed(AppSettings settings)
    {
        IntPtr foreground = NativeMethods.GetForegroundWindow();
        if (settings.KeyboardSkipFullScreen && NativeMethods.IsFullScreen(foreground))
        {
            return false;
        }

        if (settings.KeyboardExcludedApps.Count == 0)
        {
            return true;
        }

        NativeMethods.GetWindowThreadProcessId(foreground, out uint processId);
        string? path = NativeMethods.GetProcessPath(processId);
        if (path == null)
        {
            return true;
        }

        string exeName = Path.GetFileName(path);
        return !settings.KeyboardExcludedApps.Any(app => string.Equals(app.Trim(), exeName, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>Отпустить всё, что winToMac держит зажатым (выключение функции, выход).</summary>
    private static void ReleaseAll()
    {
        if (_switcherActive)
        {
            Up(VK_LMENU);
        }

        if (_ctrlHeld)
        {
            Up(VK_LCONTROL);
        }

        if (_optionInjected && _optionVk != 0)
        {
            Up(_optionVk);
        }

        _commandVk = 0;
        _optionVk = 0;
        _ctrlHeld = false;
        _switcherActive = false;
        _optionInjected = false;
        _capsActive = false;
        SwallowUp.Clear();
    }
}
