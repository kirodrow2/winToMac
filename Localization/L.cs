using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text.Json;

namespace WinToMac.Localization;

/// <summary>
/// Перевод интерфейса. Ключ — русский текст из кода: так строки читаются прямо в коде,
/// а переводы лежат в Localization/*.json (встроены в exe). Нет перевода — берётся английский, затем русский.
/// </summary>
public static class L
{
    public static readonly (string Code, string Name)[] Languages =
    {
        ("en", "English"), ("ru", "Русский"), ("zh", "中文"), ("es", "Español"), ("hi", "हिन्दी"),
        ("ar", "العربية"), ("pt", "Português"), ("fr", "Français"), ("de", "Deutsch"), ("ja", "日本語"),
    };

    private static Dictionary<string, string>? _table;
    private static Dictionary<string, string>? _english;

    public static string Code { get; private set; } = "ru";

    /// <summary>Культура выбранного языка — для дат в строке меню и календаре.</summary>
    public static CultureInfo Culture { get; private set; } = CultureInfo.CurrentCulture;

    public static bool IsRightToLeft => Code == "ar";

    /// <summary>Сменить язык. null — язык системы.</summary>
    public static void Load(string? code)
    {
        code = Languages.Any(language => language.Code == code) ? code! : DetectSystemLanguage();
        Code = code;
        _english ??= Read("en");
        _table = code switch
        {
            "ru" => null,
            "en" => _english,
            _ => Read(code),
        };

        try
        {
            Culture = CultureInfo.GetCultureInfo(code == "zh" ? "zh-CN" : code);
        }
        catch (CultureNotFoundException)
        {
            Culture = CultureInfo.CurrentCulture;
        }
    }

    public static string T(string russian)
    {
        if (Code == "ru")
        {
            return russian;
        }

        if (_table != null && _table.TryGetValue(russian, out string? text) && text.Length > 0)
        {
            return text;
        }

        return _english != null && _english.TryGetValue(russian, out string? english) && english.Length > 0 ? english : russian;
    }

    public static string T(string russian, params object[] arguments) => string.Format(T(russian), arguments);

    public static string NameOf(string code) => Languages.FirstOrDefault(language => language.Code == code).Name ?? code;

    /// <summary>Белорусам и казахстанцам удобнее русский, остальным неподдерживаемым — английский.</summary>
    private static string DetectSystemLanguage()
    {
        string system = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;
        if (Languages.Any(language => language.Code == system))
        {
            return system;
        }

        return system is "be" or "kk" or "uk" or "ky" or "uz" ? "ru" : "en";
    }

    private static Dictionary<string, string>? Read(string code)
    {
        try
        {
            // Ищем по окончанию имени: префикс ресурса зависит от RootNamespace и его регистра
            var assembly = Assembly.GetExecutingAssembly();
            string? name = assembly.GetManifestResourceNames()
                .FirstOrDefault(resource => resource.EndsWith($".Localization.{code}.json", StringComparison.OrdinalIgnoreCase));
            using var stream = name == null ? null : assembly.GetManifestResourceStream(name);
            if (stream == null)
            {
                return null;
            }

            using var reader = new StreamReader(stream);
            return JsonSerializer.Deserialize<Dictionary<string, string>>(reader.ReadToEnd());
        }
        catch
        {
            return null;
        }
    }
}
