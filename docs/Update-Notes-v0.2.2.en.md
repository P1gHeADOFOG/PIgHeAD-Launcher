# PIgHeAD · beta v0.2.2 Update Notes

[简体中文](Update-Notes-v0.2.2.md) | **English**

Release date: October 22, 2026 · Git tag: `v0.2.2-beta` · Windows x64 prerelease

[Release and downloads](https://github.com/P1gHeADOFOG/PIgHeAD-Launcher/releases/tag/v0.2.2-beta) · [README](../README.en.md)

## Interface and themes

- Rounded windows, buttons, inputs and cards, with native rounded corners and shadows on Windows 11.
- New light mode with a daytime Home landscape: blue sky, sunshine, clouds and green hills. Dark mode retains the dusk landscape.
- Checkboxes now use the theme accent, rounded corners and consistent checked, indeterminate, disabled and keyboard focus states.
- Live Chinese/English switching in the title bar and Settings. Pages, status messages, file dialog labels and consent documents include English.
- Theme and interface language persist in `PHL/appearance.json` beside the launcher and remain shared across game profiles. Minecraft's game language is configured separately.
- Animated startup with the pig icon, zoom, fade and pixel indicators. Respects the launcher and Windows animation preferences.
- Home adapts to the available height, keeping launch controls, download options and status visible without scrolling. Minimum window size: 1000 × 640 WPF logical pixels.

## Downloads

- Official release versions preload at startup; entering Downloads no longer requires manual refresh.
- Click one of six recent version buttons to install immediately, or use the full version dropdown.
- Selecting Forge, NeoForge, Fabric, Quilt or another Minecraft version automatically loads compatible builds. Stale responses are discarded and failed requests can be retried.
- Catalog loading has separate state and does not occupy the installation lock. Slow or failed catalog requests do not prevent launching an existing local game.
- Vanilla and loaders continue to use official sources. OptiFine retains its official website and external installer workflow; loaders are not combined automatically.

## Local version management

- Versions manages local installations only. Online catalogs and direct install actions have been replaced with Go to Downloads.
- Each installation has Details and Delete actions. Details include Minecraft version, loader and version, Java requirement, version type, release time, version directory size and actual directories.
- View mods appears only when Forge, NeoForge, Fabric or Quilt is detected from local metadata. Vanilla and unrecognized loaders hide the entry; version names are not used to guess support.
- Reads enabled JARs in the actual working directory's `mods` folder. Fabric, Quilt, Forge, NeoForge and legacy `mcmod.info` metadata show names, IDs, versions, filenames and read status.
- Mod browsing respects shared folders, version isolation and imported layouts. It reads metadata without executing JARs or requiring an external API key; it does not verify compatibility.
- Deletion requires a dialog reviewing the exact directory and scope, then moves it to the Windows Recycle Bin. Saves, mods and settings inside that directory move with it. Shared libraries/assets, root data and separate instance folders remain.
- Base versions with installed dependents cannot be deleted. Path escapes, inheritance cycles and links are rejected. Conflicting operations are blocked during deletion or other file operations.

## Portable workspaces and distribution

- Full and lite variants are single EXE files. Full includes .NET; lite requires .NET 10 Desktop Runtime x64.
- Place the EXE beside a modpack to detect its `.minecraft` and matching local Java. Settings, logs and library records follow the PHL workspace layout.
- Switch registered game folders and restore paths and Java hints relative to a moved modpack. Authentication caches remain protected with current-user Windows DPAPI.
- Consent documents include Chinese and English, with both languages covered by the content hash. Document changes require renewed consent. Old user-directory data and authentication caches are not automatically deleted or copied.
- Bilingual READMEs, update notes and third-party licenses are packaged with the release. Display version: `beta v0.2.2`; assembly/file version: `0.2.2.0`; informational version: `0.2.2-beta`.

## Downloads and upgrading

| File | Purpose |
| --- | --- |
| `PIgHeAD-v0.2.2-beta-win-x64.exe` | Full standalone EXE, includes .NET 10 |
| `PIgHeAD-v0.2.2-beta-win-x64-lite.exe` | Smaller EXE, requires .NET 10 Desktop Runtime x64 |
| `SHA256SUMS.txt` | SHA-256 checksums for release files |
| `README.md` / `README.en.md` | Chinese and English documentation |
| `Update-Notes-v0.2.2.md` / `Update-Notes-v0.2.2.en.md` | Chinese and English update notes |

Close the old launcher, place your chosen new EXE in the original modpack directory, and retain `.minecraft`, Java and PHL. Keep only the launcher file you intend to use to avoid opening an older version. You do not need both variants. Moving only the EXE to a new location creates a separate PHL workspace there.

## Verification and known limitations

- Release build passes with zero warnings and errors; all 120 automated tests pass.
- Coverage includes themes and bilingual bindings, Home at 1000 × 640 / 1135 × 720 / 1280 × 820, automatic catalogs, cancellation and stale responses, local details, mod formats and damaged metadata, deletion confirmation and scope, inherited dependencies, portable workspaces and DPAPI.
- Deletion tests use temporary directories and simulated recycling, without deleting user games. Full game downloads, actual Windows Recycle Bin operations, real game windows and authenticated servers still require acceptance testing.
- According to project records, Minecraft Services API approval remains pending. Microsoft OAuth registration does not grant Minecraft API access; real authenticated sign-in has not passed acceptance testing.
- This remains beta software. Readable mod metadata does not prove compatibility or safety; unrecognized files retain filenames and read status. See the README for project use restrictions.
