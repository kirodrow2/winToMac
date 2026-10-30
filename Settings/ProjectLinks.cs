using System.IO;
using System.Reflection;

namespace WinToMac.Settings;

/// <summary>Способ поддержать проект: ссылка на страницу доната или адрес кошелька для копирования.</summary>
public sealed record DonationLink(string Title, string? Url = null, string? Address = null)
{
    public bool IsConfigured => !string.IsNullOrWhiteSpace(Url) || !string.IsNullOrWhiteSpace(Address);
}

/// <summary>
/// Ссылки проекта. Пустые пункты в программе не показываются —
/// перед выпуском достаточно вписать свои адреса сюда и пересобрать.
/// </summary>
public static class ProjectLinks
{
    /// <summary>Страница проекта (GitHub, сайт). Пусто — кнопка не показывается.</summary>
    public const string Website = "";

    /// <summary>Связь с автором: пожелания и новые функции.</summary>
    public const string Telegram = "https://t.me/KKIIIROO";

    public const string TelegramName = "@KKIIIROO";

    public static readonly DonationLink[] Donations =
    {
        new("Boosty"),
        new("Ko-fi"),
        new("Patreon"),
        new("USDT (TRC20)", Address: "TVFPXbERNMH5u6DS2vAqEWt9gBCuKhMcbZ"),
        new("TON"),
    };

    public static IEnumerable<DonationLink> ConfiguredDonations => Donations.Where(link => link.IsConfigured);

    /// <summary>Инструкция лежит рядом с exe в папке Instructions.</summary>
    public static string InstructionsPath => Path.Combine(AppContext.BaseDirectory, "Instructions", "index.html");

    public static string Version =>
        Assembly.GetEntryAssembly()?.GetName().Version is { } version ? $"{version.Major}.{version.Minor}.{version.Build}" : "";
}
