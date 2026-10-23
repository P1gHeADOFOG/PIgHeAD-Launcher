# PIgHeAD - Minecraft Launcher · beta v0.2.2

[简体中文](README.md) | **English**

This launcher is under development and currently intended only for the author and friends. Do not redistribute it.

A Minecraft Java Edition launcher built with C#, WPF, .NET 10 and MVVM.

Download: [beta v0.2.2 for Windows x64](https://github.com/P1gHeADOFOG/PIgHeAD-Launcher/releases/tag/v0.2.2-beta). The full `PIgHeAD-v0.2.2-beta-win-x64.exe` includes .NET 10 and runs directly. The smaller `-lite.exe` requires .NET 10 Desktop Runtime x64. Verify downloads with `SHA256SUMS.txt` on the release page. According to project records, Minecraft API approval is pending and real Microsoft authentication has not passed acceptance testing.

Update notes: [English](docs/Update-Notes-v0.2.2.en.md) | [简体中文](docs/Update-Notes-v0.2.2.md). This release includes themes and bilingual UI, automatic download catalogs, startup animation, local version details/mod browsing/deletion, and portable workspace improvements.

Further documentation: [launch integration](docs/Launch-Integration-Changes.md), [advanced features](docs/Advanced-Features.md), [imports and repair](docs/Release-v0.1.4beta.md), and [portable workspaces](docs/Portable-Workspaces.md). These technical documents are currently in Chinese. The [user agreement](docs/User-Agreement.en.md) and [privacy notice](docs/Disclaimer.en.md) are also available in English.

## Features

- Rounded window with dark and light themes. Switch theme or Chinese/English in the title bar or Settings; choices apply immediately and persist in `PHL/appearance.json` beside the launcher.
- Checkboxes use rounded corners and the theme accent. Home displays a daytime landscape in light mode and dusk in dark mode.
- Home, Versions, Downloads, Accounts and Settings navigation. Home adapts its hero and cards to the available height so the launch controls and status remain visible without scrolling. Minimum window size: 1000 × 640 WPF logical pixels.
- Animated startup with the pig icon, a gentle zoom, fade and pixel indicators. Respects both the launcher animation setting and Windows animation preferences.
- Original pig artwork embedded as the sidebar, window and application icons.
- Scan local versions and download or repair official release versions, dependencies and matching Java runtimes.
- Launch with saved directory and memory settings; check Java compatibility, display progress and exit codes, and keep console logs.
- Clearly marked offline identities and Microsoft system-browser authorization with PKCE. The default public Client ID is `2d1fa88a-c9ad-41ef-a0e0-a6f661a7356a`; change or clear it in Settings. Offline launch and downloads remain available with an empty ID.
- Cancel downloads or launch preparation without terminating a running game. Conflicting installs and duplicate launches are blocked while a game runs.
- First use requires separate consent to the draft agreement and disclaimer. Both documents are available in Chinese and English. Document changes invalidate prior consent. Publisher: PIgHeAD_OFOG; contact: p1gheadofog@gmail.com.
- Advanced settings for game window size and mode, automatic memory, version isolation, JVM/game arguments, process priority and game language.
- Downloads preloads official versions at startup. Click one of the six recent release buttons to install immediately, or choose another release from the full list. Selecting a loader or Minecraft version automatically loads compatible builds. Failed requests can be retried.
- Automatic installation of vanilla Minecraft, Forge, NeoForge, Fabric and Quilt; official website and local installer entry points for OptiFine. Loaders install individually and are not combined automatically.
- Drag and drop complete ZIP modpacks, Modrinth `.mrpack` files or game folders. Detect `.minecraft`, retain imported instance layouts, and repair missing files from Versions.
- Versions manages local installations only, with a Go to Downloads link for new installations. Each version has details for its Minecraft version, loader, Java requirement, directories and size. View mods appears only for detected Forge, NeoForge, Fabric or Quilt installations and reads names, IDs, versions and filenames from the actual working directory.
- Deleting a version requires reviewing its directory and confirming before moving it to the Windows Recycle Bin. Data inside that directory moves with it. Shared libraries/assets, root saves and separate instance folders remain. Base versions with installed dependents cannot be deleted.
- Version `beta v0.2.2` displayed in the sidebar and Settings. Buttons and navigation support optional press animations.

## Build and run

Requires Windows, an IDE supporting .NET 10 WPF, and the SDK specified in `global.json`. The first restore needs NuGet access.

```powershell
dotnet restore PIgHeAD.sln
dotnet build PIgHeAD.sln --no-restore
dotnet test PIgHeAD.sln --no-build
dotnet run --project PIgHeAD.Launcher
```

The solution uses Debug/Release with Any CPU. Select the runtime identifier when publishing. If an existing launcher holds the Debug output open, close it or build Release and run `dotnet run --project PIgHeAD.Launcher -c Release --no-build`.

For a standalone Windows x64 EXE:

```powershell
powershell -File tools/Publish-Release.ps1 -SingleFile -OutputDirectory artifacts\standalone
```

Output: `artifacts\standalone\PIgHeAD-v0.2.2-beta-win-x64.exe`. Copy that EXE alone; companion DLLs and a separate .NET installation are unnecessary. The script embeds bilingual documentation, third-party notices and build information, and generates SHA-256 checksums. Choose an unused output directory. The publish profile is `PIgHeAD.Launcher/Properties/PublishProfiles/SingleFile.pubxml`.

Native runtime libraries extract to the Windows user's temporary `.net` cache on first launch. Place the EXE beside `.minecraft` and, where available, a matching Java directory such as `java21`. The launcher detects nearby games and creates `PHL` for its data. Game library records, consent and DPAPI-encrypted sign-in caches live beside the EXE; each game's settings and logs live in that game's `PHL`. Select an imported folder at the top of Versions and use Switch folder to load its profile.

For a smaller framework-dependent EXE, add `-FrameworkDependent` and use a fresh output directory such as `artifacts\standalone-lite`. Its name ends in `-lite.exe` and it requires **.NET 10 Desktop Runtime x64**. Both variants disable trimming and retain Chinese and English satellite resources. The self-contained build also enables single-file compression. See the portable workspace documentation for relocation and privacy details. DPAPI caches cannot be moved between Windows user accounts.

## Launch an existing game

1. Enter the actual game root in Settings and save. It should contain `versions`, `libraries` and `assets`; do not select a single version folder.
2. To reuse the official launcher's game directory, open `%APPDATA%\.minecraft` in Explorer and copy its expanded absolute path.
3. Java can be left empty. The launcher searches installed runtimes, nearby Java directories, JAVA_HOME and PATH. A custom path must point to `java.exe` or `javaw.exe`.
4. Open Versions, refresh local versions and select one.
5. In Accounts, enter an offline player name or complete Microsoft authentication.
6. Return Home and select Launch Game.
7. For a complete local installation, turn off Download missing files and matching Java to avoid game-file downloads. Microsoft session renewal still requires internet access.

Local means version metadata was found; it does not guarantee every file exists. Missing files can be downloaded during preparation or repaired separately. Java is checked when actually launching, so Home does not claim an untested runtime is ready.

## Download a version

1. Save the target game directory.
2. Open Downloads. Versions preload at startup; slow requests show a loading indicator and failed requests offer a retry.
3. Click a recent version button to install it, or choose another release and select Download & install.
4. For a mod loader, select its component and Minecraft version. Compatible builds load automatically and a recommended/first build is selected. Select Download & install when ready.
5. Watch progress or cancel in Downloads. Reinstalling checks existing files and completes missing ones. Successful installation refreshes local versions and selects the new installation.

Downloads use CmlLib's Mojang/Microsoft distribution services without adding unconfigured third-party mirrors. Compatible loader catalogs use their official sources. OptiFine still requires downloading the author's installer and confirming its destination in the external installer window. An installer exiting does not prove installation succeeded.

## Microsoft sign-in

See [Microsoft application registration and authentication](docs/Microsoft-Authentication.md).

- The project supplies a default public desktop Client ID. Missing fields use that default; explicitly empty or custom IDs are retained.
- Authorization Code + PKCE opens the default browser. No Client Secret or password entry in the launcher is required.
- A verified identity is shown only after Microsoft, Xbox, ownership and Minecraft profile checks succeed.
- Expired Microsoft authentication prompts another sign-in; it never silently switches to an offline identity.
- According to maintainer records dated October 21, 2026, the project application has Entra registration and desktop callbacks but has not applied for or received Minecraft Services API approval. Microsoft OAuth registration alone does not grant that access. The Accounts page displays this status.

## Settings and logs

Game settings: `game-folder\PHL\settings.json`. For a root named `.minecraft`, PHL is in its parent folder. Beside the EXE, `PHL\launcher.json` retains the last profile, `PHL\library.json` records game directories, and `PHL\appearance.json` stores launcher theme and language.

```json
{
  "gameDirectory": "C:\\Games\\Minecraft",
  "javaPath": "",
  "maximumMemoryMb": 4096,
  "microsoftClientId": "2d1fa88a-c9ad-41ef-a0e0-a6f661a7356a"
}
```

- Paths must be absolute. Memory supports 512–65536 MiB; that range does not imply the computer has that much RAM.
- Missing settings use defaults. Corrupt game settings retain a backup, and saves use a temporary file in the same directory before replacement.
- Page navigation retains unsaved drafts. Home uses saved settings. Changing the game directory invalidates the local version list.
- Launcher language and Minecraft language are independent. Changing the interface language does not modify the game's language setting.
- Offline player names currently last for the application session.
- Game logs live in the game's `PHL\logs`; Downloads shows the latest path.
- Microsoft caches are separated by Client ID and protected with current-user Windows DPAPI. Passwords and tokens are not stored in settings JSON.

## Project layout

```text
PIgHeAD.Launcher/
  Bootstrap/                 Service and view-model composition
  Models/                    Settings, versions, sessions and results
  Services/
    Abstractions/            Replaceable service interfaces
    Accounts/                PKCE, protected caches and offline identities
    Consent/                 Embedded documents and consent records
    Desktop/                 File dialogs, official links and agreement windows
    Minecraft/               Installs, modpacks, Java and process logs
    Presentation/            Theme preferences and live localization
    Settings/                Portable JSON settings and workspace paths
  ViewModels/                Page state and asynchronous commands
  Views/                     WPF windows, pages and animation controls
  Resources/
    Images/                  Embedded PNG and ICO artwork
    Styles/                  Palettes, controls, icons and scenery
    Strings.en.json          English interface copy and status templates
tests/
  PIgHeAD.Launcher.Tests/     Unit tests and WPF composition tests
  PIgHeAD.Launcher.ProcessProbe/
                             Controlled process for launch verification
tools/Publish-Release.ps1     Packaging and checksums
```

Views invoke view-model commands through bindings. Business services implement replaceable interfaces; desktop dialogs are centralized. CmlLib handles Minecraft formats and dependency rules. Other dependencies include CmlLib.Core.Auth.Microsoft, XboxAuthNet.Game.Msal and CmlLib.Core.Installer.Forge, including NeoForge. Fabric/Quilt use their official Meta APIs without an additional UI framework.

## Verification

Tests cover settings and recovery, navigation/bindings, account and version state, cancellation and failures, operation locks, Java parsing, offline UUIDs, DPAPI and controlled process arguments, logs and exit codes. New checks cover automatic catalogs, stale loader responses, appearance persistence, live translations, bilingual consent, both themes and Home layouts at 1000 × 640, 1135 × 720 and 1280 × 820.

DPAPI tests require the current Windows user profile. Run them in a normal user context if a sandbox or service account cannot access DPAPI; encryption must not fall back to plaintext.

Set `PIGHEAD_TEST_ARTIFACTS` to export rendered page previews. Tests do not open Minecraft, download full games, sign in to real accounts or accept agreements for the user. Full loader installs, real game graphics and authenticated server access still require acceptance testing. Project records continue to mark Minecraft API approval as pending.

Use in mainland China is prohibited.
