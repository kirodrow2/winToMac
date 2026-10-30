# Code signing policy

> **Status: winToMac releases are currently not code-signed.**
> Windows SmartScreen may show an "unknown publisher" warning on first launch — click **More info → Run anyway**.
> Updates installed from inside the app are not affected by this warning.
> We plan to sign releases (for example through the [SignPath Foundation](https://signpath.org/) program for open-source projects) once the project is eligible. This page describes the rules that will apply to signed releases.

## What is signed

Only `winToMac.exe` built from this repository. The program is distributed exclusively through
[GitHub Releases](https://github.com/kirodrow2/winToMac/releases) of this repository (`winToMac.zip`)
and updates itself only from the same place.

## How releases are built

- Every release is built by GitHub Actions ([`.github/workflows/release.yml`](.github/workflows/release.yml)) on a GitHub-hosted Windows runner from a version tag `vX.Y.Z`.
- Releases are never built on a personal computer.
- The build uses only the source code of this repository and NuGet packages listed in [`winToMac.csproj`](winToMac.csproj) (ManagedShell, Microsoft.Web.WebView2) plus the official .NET 8 SDK.
- Release notes for every version are stored in [`release-notes/`](release-notes).

## Team roles

| Role | Members |
|---|---|
| Committers and reviewers | [@kirodrow2](https://github.com/kirodrow2) |
| Approvers (approve each signing request) | [@kirodrow2](https://github.com/kirodrow2) |

All maintainers use two-factor authentication on GitHub.
Contributions from other people are accepted only through pull requests reviewed by a maintainer.

## Privacy

This program will not transfer any information to other networked systems unless specifically requested by the user or the person installing or operating it, except for the update check described in the [privacy policy](PRIVACY.md), which can be turned off in the settings.

## Reporting a problem

If you find a signed file that you believe is not built from this repository, or any security issue, please report it via [GitHub Issues](https://github.com/kirodrow2/winToMac/issues) or Telegram [@KKIIIROO](https://t.me/KKIIIROO).
