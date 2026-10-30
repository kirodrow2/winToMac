# Privacy policy

**winToMac does not collect, store or send any personal data or usage statistics. There is no telemetry, no analytics and no tracking.**

All settings are kept locally on your computer in `%APPDATA%\winToMac\settings.json`.
Your own wallpaper videos stay where you put them (by default `Videos\winToMac Wallpapers`).

## Network connections

The program connects to the internet only in these cases:

| When | Where | What is sent |
|---|---|---|
| Checking for updates — a minute after start and then every 12 hours (can be turned off in Settings → "Check for updates automatically") or when you click "Check for updates…" | `api.github.com` (GitHub Releases of this repository) | A standard HTTPS request with the program version in the User-Agent. No identifiers. |
| You click "Update" | `github.com` / `objects.githubusercontent.com` | Download of the new release archive. |
| You choose to download an Apple Aerial wallpaper in the gallery | `sylvan.apple.com`, `a1.phobos.apple.com` | Download of the public Aerial catalog and the chosen video. |
| You click a link (website, instructions, Telegram, donation) | The opened site, in your default browser | Whatever that site normally receives from a browser. |

Live wallpapers are rendered locally by the Microsoft Edge WebView2 runtime with background networking disabled; preset wallpapers do not load anything from the internet.
The Edge WebView2 runtime itself is a Windows component and is updated by Microsoft according to Microsoft's own policies.

## Website

The website `kirodrow2.github.io/winToMac` is hosted on GitHub Pages. To show the number of downloads, your browser requests public release information from `api.github.com`. The site uses no cookies and no analytics; your language choice is remembered only in your browser's local storage.
GitHub may keep standard server logs — see the [GitHub Privacy Statement](https://docs.github.com/site-policy/privacy-policies/github-general-privacy-statement).

## Donations

Donations are made directly in your own crypto wallet. The program and the website never see your wallet or payment details.

## Contact

Questions: Telegram [@KKIIIROO](https://t.me/KKIIIROO) or [GitHub Issues](https://github.com/kirodrow2/winToMac/issues).

---

# Политика конфиденциальности

**winToMac не собирает, не хранит и не отправляет никаких персональных данных и статистики использования. Нет телеметрии, аналитики и слежки.**

Настройки хранятся только на вашем компьютере: `%APPDATA%\winToMac\settings.json`. Ваши видео для обоев остаются там, куда вы их положили.

Программа выходит в интернет только:
- для проверки обновлений на `api.github.com` — через минуту после запуска и раз в 12 часов (отключается в настройках) или по кнопке «Проверить обновления…»; отправляется только обычный HTTPS-запрос с версией программы;
- для скачивания обновления с GitHub — когда вы нажали «Обновить»;
- для скачивания обоев Apple Aerial с серверов Apple — только когда вы сами их выбрали;
- когда вы открываете ссылку (сайт, инструкция, Telegram) — в вашем браузере.

Сайт размещён на GitHub Pages, без cookies и аналитики; для счётчика скачиваний браузер запрашивает публичные данные о выпусках с `api.github.com`.
