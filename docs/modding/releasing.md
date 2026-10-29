# Releasing

A release is a git tag `vX.Y.Z`, a zip players unpack and install from, and release notes made from the commit trailers. `build/package.sh` makes the zip, `build/release.sh` does the whole local part of a release. Nothing is pushed unless you ask for it.

## The player package

```sh
build/package.sh            # version from Directory.Build.props
build/package.sh 0.2.0      # or a version of your own
```

It builds the player mods in Release without deploying them into the game, publishes `oben` self-contained and as one file for `linux-x64` and `win-x64` (players need no .NET), and writes `dist/ObenseuerExtended-<version>/` and the `.zip` next to it. Set `OBENSEUER_DIR` if the game isn't in the default place, the build needs its assemblies.

```text
ObenseuerExtended-0.1.0/
  Mods/<each player mod>/      the mod's dll, pdb and mod.json
  tool/linux-x64/oben          the tool, with payload/ next to it (loader, Harmony, Cecil)
  tool/win-x64/oben.exe        same for Windows
  install.sh  install.bat      copy the mods into the game and hook in the loader
  uninstall.sh  uninstall.bat  take the loader out again
  README.txt  LICENSE          and THIRD-PARTY-NOTICES.md / changelog.json when they exist
```

### Which mods are in it

Every folder in `src/` or `mods/` with a `mod.json`, except those marked `"devOnly": true`. DevBridge and ScriptRunner are marked: the bridge opens a local port and the script runner compiles and runs code sent to the game, which is fine for someone making mods and not something to put on a player's machine by default. The loader reads `mod.json` and ignores the field. A new mod is in the package as soon as it has a `mod.json` and a project named like its folder, so mark a debugging-only mod `devOnly`.

### The installer

The scripts only call the tool: `oben install --mods <unpacked>/Mods`. The tool finds the game itself (`--game`, `OBENSEUER_DIR`, Heroic, every Steam library), copies the loader payload into `Obenseuer_Data/Managed`, registers the loader with Unity (keeping `.obenbak` copies of the originals) and copies the mods into the game's `Mods` folder. Files that only exist in the game, like a mod's `config.json`, are left alone, so installing again updates and keeps the settings. `uninstall` removes the loader and restores Unity's startup files; the `Mods` folder stays unless `--remove-mods` is passed. Both scripts pass extra arguments on (for example `--game`).

The payload (`ObenLoader.dll`, `0Harmony.dll`, `Mono.Cecil.dll`) is looked up in `payload/` beside the executable (`AppContext.BaseDirectory`), so the project marks those files `ExcludeFromSingleFile`. `package.sh` fails if one of them is missing from a published tool.

To try the installer without touching a real game, make a folder with an empty `Obenseuer.exe`, an `Obenseuer_Data/Managed` folder and copies of `ScriptingAssemblies.json` and `RuntimeInitializeOnLoads.json` in `Obenseuer_Data`, unpack the zip and run `install.sh --game <that folder>`.

## Making a release

Only when the owner asks for one.

1. Look at what changed (`git log v<last>..HEAD`, or the whole history before the first tag) and decide `Major.Minor.Patch`. Before 1.0, breaking changes and new features bump the minor version, fixes and tweaks the patch version.
2. Preview: `build/release.sh X.Y.Z --dry-run`. It changes nothing, lists what would stop a real release (dirty tree, a tag that exists, a version that isn't newer) and prints the release notes (`oben changelog --release X.Y.Z --notes`).
3. Read every line of the notes the way a player would (the checklist is in [changelog.md](changelog.md)): duplicates, a mod's name repeated in its line, "Changed" or "Fixed" for something new in this release, code names, typos. Fix problems in the builder (`tools/ObenTool/Changelog/`) or, for commits that aren't pushed yet, by rewording them, then dry-run again.
4. Release: `build/release.sh X.Y.Z`. It runs `dotnet test ObenseuerExtended.slnx`, sets `<Version>` in `Directory.Build.props`, commits `chore(release): X.Y.Z`, writes `changelog.json` with that version released (`build/changelog.sh --release X.Y.Z`; the changelog hooks skip that commit), builds the package from that commit and tags `vX.Y.Z`. If a step fails before the tag exists, the release commit is taken back.
5. Look at `dist/ObenseuerExtended-X.Y.Z.zip`, then publish.

The script does not push and does not create the GitHub release on its own. At the end it prints the two commands:

```sh
git push origin <branch> vX.Y.Z
gh release create vX.Y.Z dist/ObenseuerExtended-X.Y.Z.zip --title "ObenseuerExtended X.Y.Z" --notes-file <notes>
```

`build/release.sh X.Y.Z --publish` runs them for you after the local part, asking first (`--yes` skips the question). It needs `gh` logged in, checked before anything is changed.

Never push or release without being asked, and never tag by hand to try something out: the tag is the release.
