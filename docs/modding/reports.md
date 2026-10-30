# Bug reports

Players make a bug report in one step, and it holds everything we usually have to ask for: the versions, the system, the logs, the mods' settings and, if they want, their PC, their save and the last crash. This page covers how they make one, what is in it, and the formats other programs (the companion app, scripts) can rely on.

## Making one

| From | How |
| --- | --- |
| The game | MODS, Report page: tick what to add, Make report. Open folder, Copy summary and Report on GitHub act on the report just made. The Errors page has a "Make a bug report" button that leads there. |
| The F1 console | `report [cpu] [gpu] [ram] [save] [all] [no-crash] [show-personal]`; `report folder` opens the reports folder, `report issue` the GitHub form for the last report. |
| Outside the game | `oben report [--cpu] [--gpu] [--ram] [--save] [--all] [--no-crash] [--show-personal] [--out DIR] [--json]`, for when the game won't start. The tool ships in the release zip (`tool/linux-x64/oben`, `tool\win-x64\oben.exe`). |
| A mod | `BugReports.Make(options, done)` in `ObenFramework.Diagnostics.Reports`; `done` runs on the main thread with the `BugReport`, or null when it failed. |

In the game, what only Unity can answer is read on the main thread (about 30 ms); reading, cleaning and zipping the files runs on a worker, so the game keeps going. Reports go to `ObenLoader/reports/ObenseuerExtended-report-<yyyyMMdd-HHmmss>.zip` in the game folder; the newest ten are kept.

## What is in it

```text
report.md                     the summary, written to be pasted into an issue
system.json                   the machine and install (format below), cut down to what was ticked
logs/latest.log, previous.log, errors.log, errors-previous.log, runner.log
logs/mods/<mod>.log           every mod's own log, and the one from the session before
logs/Player.log, Player-prev.log
settings/ObenLoader/settings.json, game-check.json
settings/Mods/<mod>/*.json    every mod's settings (not mod.json, the changelog or identity files)
crash/Crash_<UTC time>/       crash.dmp and Player.log of the last crash, if it was in the last week
save/<character>/<slot>/      every file of the save, when ticked
```

- **Always:** the ObenseuerExtended version and commit, the game version, Steam build and the SHA-256 of `Assembly-CSharp.dll` and of the exe, the operating system (under Wine or Proton also the Wine version and the real system, read from Wine's `wine_get_version` and `wine_get_host_version`), the language, the mods and their states, and BepInEx if it is in the game folder: its version, whether its doorstop proxy is there, and every plugin and patcher with the GUID, name and version from its `[BepInPlugin]` and a short SHA-256.
- **When ticked:** the processor (name, threads, MHz); the graphics card (name, memory, graphics API) with the screen (resolution, mode, refresh rate); the memory; the save being played, or the newest save at the main menu.
- **On unless turned off:** the last crash. Unity's crash handler leaves `%TEMP%\Loiste Interactive\Obenseuer\Crashes\Crash_<UTC time>` with a minidump and the log; the newest one younger than a week goes in.
- Logs over 8 MB keep their newest 8 MB, starting at a whole line, and say so at the top. The report says when the zip is over GitHub's 25 MB limit for attachments.
- Co-op identity files stay out: someone with them could pose as the player in a co-op game.

## Personal info

On by default ("Hide personal info", `show-personal` turns it off). Every text file goes through `ReportRedactor`:

- User names are learned from folder paths (`C:\Users\<name>`, `/home/<name>`, `/run/media/<name>`, `/Users/<name>`), plus the account name, and then replaced with `<user>` wherever they appear, in every file. Wine's `steamuser` and folders like `Public` stay.
- Steam ids (`7656119` and ten digits) become `<steam id>`. Lobby ids are longer numbers of another range and stay.
- IPv4 addresses become `<ip>`, except loopback, `0.x.x.x` and version numbers (`Version=2.3.6.0`, `v1.2.3.4`, five or more numbers).

Binary files can't be cleaned: the save and `crash.dmp`. The summary says so when a crash dump is in the report.

## Formats

### system.json

Written by the game to `ObenLoader/system.json` at every start (after all mods have loaded) and into every report. A JSON object; unknown values are `null` or `0`. Fields are only ever added, never renamed or removed; `Format` goes up if that ever has to change.

| Field | Example |
| --- | --- |
| `Format` | `1` |
| `Written` | `"2026-10-30T01:04:07"` (local time) |
| `Build` | `"0.2.1 (6e6bac1)"`, as the pause menu shows it (`"1.1.0-pre.3 (6e6bac1)"` for a test build after 1.0.0) |
| `Loader`, `Game`, `GameBuild`, `Unity` | `"0.2.1"` (the loader's version without the commit), `"0.5.13"`, `"26179069"`, `"6000.0.77f1"` |
| `GameCodeHash`, `GameExeHash` | SHA-256 of `Assembly-CSharp.dll` and of the exe, lowercase hex |
| `Os`, `Wine`, `Host` | `"Windows 10 (10.0.19045) 64bit"`, `"11.0"`, `"Linux 7.2.3-arch1-2"` (`Wine` and `Host` are null on Windows) |
| `Language` | `"English"` |
| `Cpu`, `CpuCores`, `CpuMhz` | `"Intel(R) Core(TM) i9-9900K CPU @ 3.60GHz"`, `16`, `3600` |
| `Gpu`, `GpuVendor`, `GpuApi`, `GpuMemoryMb` | `"AMD Radeon RX 7900 XT (RADV NAVI31)"`, `"ATI"`, `"Direct3D 11.0 [level 11.1]"`, `20224` |
| `RamMb` | `32016` |
| `Display` | `"2560x1440, borderless fullscreen, 144 Hz"` |
| `Mods` | `[{ "Id", "Name", "Version", "State" }]`, `State` being `Loaded`, `Disabled`, `Failed` and so on |
| `BepInEx` | `null`, or `{ "Version", "Doorstop", "Plugins": [...], "Patchers": [...] }`, each entry `{ "File", "Guid", "Name", "Version", "Sha256" }` |

### oben report --json

Prints one JSON object when the zip is written:

```json
{
  "path": ".../ObenLoader/reports/ObenseuerExtended-report-20261030-011020.zip",
  "bytes": 974833,
  "tooBigForGitHub": false,
  "issueUrl": "https://github.com/Official-Husko/openseuer-framework/issues/new?template=bug_report.yml&...",
  "save": "Esko_Virtanen/Autosave",
  "crash": "Crash_2026-10-29_162427293",
  "systemFromGame": true,
  "notes": [],
  "summary": "## ObenseuerExtended report\n..."
}
```

`systemFromGame` is false when the game never wrote `system.json`; the report then has only what the tool can tell from the files (the mods from their `mod.json`, the tool's own view of the system). The game's hashes and BepInEx are always read fresh from disk.

### The GitHub form

`IssueLink.Bug` opens `.github/ISSUE_TEMPLATE/bug_report.yml` with fields filled from the query string; GitHub matches query parameters to the form's field ids. The ids are `mod-version`, `game-version`, `os`, `specs` and `bepinex`. Renaming one of them in the template breaks the filling, so change both together.

## The code

| Where | What |
| --- | --- |
| `src/Shared/Reports/` | Everything without Unity, compiled into the framework, the `oben` tool and the tests: `SystemSnapshot`, `ReportOptions`, `ReportFiles` (which files, under what names), `ReportAssembly` (summary and snapshot in front, reading `errors.log` back), `ReportSummary`, `IssueLink`, `ReportRedactor`, `ReportPackager` (the zip), `BepInExScan`, `BuildStamp`. |
| `src/ObenFramework/Diagnostics/Reports/` | The game side: `BugReports` (the API, `system.json` at start), `SystemReader` (Unity's `SystemInfo`, the loader, the mod list), `WineInfo`, `BugReportCommand`. |
| `src/ObenUIKit/Menu/Pages/ReportPage.cs` | The Report page. |
| `tools/ObenTool/Commands/ReportCommand.cs` | `oben report`. |
| `tests/ObenCore.Tests/Reports/` | Redaction, the summary and form link, the zip (cut logs, open files), BepInEx plugins, the file choices, the newest crash and save. |
