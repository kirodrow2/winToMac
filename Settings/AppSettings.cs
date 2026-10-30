using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace WinToMac.Settings;

public enum ThemeKind
{
    Light,
    Dark,
}

/// <summary>Какая физическая клавиша работает как ⌘ Command.</summary>
public enum CommandKey
{
    /// <summary>Alt — на обычной ПК-клавиатуре стоит там же, где ⌘ на Mac.</summary>
    Alt,

    /// <summary>Win — так ⌘ приходит с Mac через Moonlight и с клавиатур Apple.</summary>
    Win,

    /// <summary>⌘ уже приходит как Ctrl (например, в Moonlight включена замена) — переназначать не нужно.</summary>
    Ctrl,
}

public enum LanguageSwitchMethod
{
    /// <summary>Win+Пробел — работает во всех программах.</summary>
    WinSpace,

    /// <summary>Сообщение активному окну — без всплывающей подсказки Windows, но не во всех программах.</summary>
    Message,

    /// <summary>Своё сочетание — то, что настроено в Windows для смены языка (Alt+Shift, Ctrl+Shift…).</summary>
    Shortcut,
}

/// <summary>Приложение, закреплённое в доке (оно же «избранное» в списке приложений).</summary>
public sealed class PinnedApp
{
    public string Name { get; set; } = "";

    /// <summary>Что запускать: путь к exe/lnk или "shell:AppsFolder\&lt;AppId&gt;".</summary>
    public string LaunchTarget { get; set; } = "";

    /// <summary>AppUserModelID, если есть — по нему сопоставляются окна UWP и современных приложений.</summary>
    public string? AppId { get; set; }

    /// <summary>Имя exe в нижнем регистре — по нему сопоставляются окна классических приложений.</summary>
    public string? ExeName { get; set; }

    /// <summary>Откуда брать иконку (путь или shell-имя).</summary>
    public string IconSource { get; set; } = "";
}

public sealed class AppSettings
{
    // --- Док ---
    public double DockIconSize { get; set; } = 52;
    public bool DockMagnification { get; set; } = true;
    public double DockMagnifiedSize { get; set; } = 96;
    public bool DockAutoHide { get; set; }
    public bool DockReserveSpace { get; set; } = true;
    public double DockBackgroundOpacity { get; set; } = 0.55;
    public bool DockShowRunningIndicator { get; set; } = true;
    public bool DockShowUnpinnedRunning { get; set; } = true;
    public bool DockShowTrash { get; set; } = true;
    public bool DockShowDesktopButton { get; set; } = true;

    // --- Верхняя панель ---
    public double TopBarHeight { get; set; } = 28;
    public double TopBarFontSize { get; set; } = 13;
    public bool TopBarBlur { get; set; } = true;
    public bool ClockShowSeconds { get; set; }
    public bool ClockShowDate { get; set; } = true;
    public bool ShowBatteryPercent { get; set; } = true;
    public bool ShowTrayIcons { get; set; } = true;

    /// <summary>Значки трея, скрытые пользователем (NotifyIcon.Identifier).</summary>
    public List<string> HiddenTrayIcons { get; set; } = new();

    // --- Клавиатура как на Mac ---
    public bool MacKeyboard { get; set; } = true;
    public CommandKey LocalCommandKey { get; set; } = CommandKey.Alt;
    public CommandKey RemoteCommandKey { get; set; } = CommandKey.Win;
    public bool CapsLockSwitchesLanguage { get; set; } = true;

    /// <summary>Клавиша смены языка (VK). null — Caps Lock или то, во что он переназначен в реестре.</summary>
    public ushort? LanguageKey { get; set; }

    public bool LanguageKeyHoldForCaps { get; set; } = true;
    public LanguageSwitchMethod LanguageSwitch { get; set; } = LanguageSwitchMethod.WinSpace;
    public KeyShortcut LanguageShortcut { get; set; } = new() { Modifiers = KeyModifiers.Alt, Key = 0xA0 };

    /// <summary>Свои переназначения клавиш и сочетаний.</summary>
    public List<KeyMapping> KeyMappings { get; set; } = new();
    public bool CommandSpaceOpensLauncher { get; set; } = true;
    public bool OptionWordNavigation { get; set; } = true;
    public bool KeyboardSkipFullScreen { get; set; } = true;

    /// <summary>Программы, в которых клавиши не переназначаются (имена exe).</summary>
    public List<string> KeyboardExcludedApps { get; set; } = new() { "mstsc.exe", "vmconnect.exe", "moonlight.exe" };

    // --- Живые обои ---
    public bool WallpaperEnabled { get; set; }

    /// <summary>"preset:имя" — встроенная анимация, иначе путь к видео или GIF.</summary>
    public string? WallpaperSource { get; set; }

    public bool WallpaperMuted { get; set; } = true;
    public bool WallpaperPauseWhenCovered { get; set; } = true;

    /// <summary>Свои видео и GIF, добавленные из других папок.</summary>
    public List<string> CustomWallpapers { get; set; } = new();

    [JsonIgnore]
    public static string WallpaperFolder => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.MyVideos), ProductName + " Wallpapers");

    // --- Общее ---
    /// <summary>Язык интерфейса (en, ru, zh…). null — язык системы.</summary>
    public string? UiLanguage { get; set; }

    public ThemeKind Theme { get; set; } = ThemeKind.Dark;
    public bool HideWindowsTaskbar { get; set; } = true;

    /// <summary>
    /// Режим панели задач до того, как winToMac её спрятал. Хранится в настройках, чтобы его не потерять,
    /// если копию программы закрыли принудительно (обновление, диспетчер задач).
    /// </summary>
    public int? TaskbarOriginalState { get; set; }

    public List<PinnedApp> PinnedApps { get; set; } = new();

    /// <summary>Название программы: папки настроек и обоев, автозапуск, заголовки.</summary>
    public const string ProductName = "winToMac";

    [JsonIgnore]
    public static string Folder => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), ProductName);

    [JsonIgnore]
    public static string FilePath => Path.Combine(Folder, "settings.json");

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
    };

    public static AppSettings Current { get; private set; } = new();

    /// <summary>Срабатывает после любого изменения настроек — окна перерисовываются на лету.</summary>
    public static event Action? Changed;

    public static void Load()
    {
        try
        {
            if (File.Exists(FilePath))
            {
                Current = JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(FilePath), JsonOptions) ?? new();
            }
        }
        catch
        {
            // Битый файл не должен ронять оболочку — стартуем с настройками по умолчанию
            Current = new();
        }
    }

    public static void Save()
    {
        Directory.CreateDirectory(Folder);
        File.WriteAllText(FilePath, JsonSerializer.Serialize(Current, JsonOptions));
    }

    /// <summary>Сохранить и уведомить окна.</summary>
    public static void Apply()
    {
        Save();
        Changed?.Invoke();
    }

    public static void Reset()
    {
        var pinned = Current.PinnedApps;
        var hiddenTrayIcons = Current.HiddenTrayIcons;
        Current = new AppSettings { PinnedApps = pinned, HiddenTrayIcons = hiddenTrayIcons };
        Apply();
    }
}
