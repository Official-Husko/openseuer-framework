# Releasing

A release is a git tag, a zip players unpack and install from, and release notes made from the commit trailers.

## Versions

Versions are `Major.Minor.Patch`, and every release gets its own:

- **Before 1.0.0** (now) every release is a GitHub pre-release from `build/experimental.sh`, every few commits or when a milestone is done. The major version stays 0. A release with a new feature (any `feat` commit) or a breaking change (`type!:`, or a `BREAKING CHANGE` line in the body) bumps the minor version, 0.2.0 to 0.3.0; one with only fixes, performance, docs, build or other work bumps the patch version, 0.2.0 to 0.2.1. Commit types decide it, so keep them honest.
- **1.0.0** is the first stable release: when every module in PROGRESS.md is at 100%, and only when the owner says so, with `build/release.sh 1.0.0`.
- **From 1.0.0 on** a breaking change bumps the major version, a feature the minor version, the rest the patch version. Stable releases come from `build/release.sh` when the owner asks; `build/experimental.sh` publishes test builds of the coming version in between, tagged `v1.1.0-pre.N+<commit>`.

`build/next-version.sh` prints the version the commits since the last numbered release call for, and why (`0.2.0 new features`); `--minor` or `--patch` overrides it, and the release scripts take the same flags. The five pre-releases before the rule were tagged `v0.1.0-pre.1` to `pre.5` (GitHub orders releases by the version in the tag and ignores what follows `+`, so they needed a number), and their titles read `v0.1.0 (<commit>)` or `v0.1.0-pre.5 (<commit>)`. They count as 0.1.0, so the first numbered release is 0.2.0: without a numbered tag, `next-version.sh` bumps from the version in `Directory.Build.props` by the commits since the newest of those tags.

`<Version>` in `Directory.Build.props` is never set by hand: the release scripts commit it ("chore(release): X.Y.Z"). `build/package.sh` makes the zip. The pause menu shows `ObenseuerExtended` and `v0.2.1 (6e6bac1)`, the version and the commit read from the kit's assembly (`build/Revision.targets` stamps the commit into every assembly, with `-dirty` for uncommitted changes), so a player's report or a screenshot says which build it was, and after a hot reload the line tells whether the change loaded. Release titles use the same "ObenseuerExtended v0.2.1 (6e6bac1)".

## The player package

```sh
build/package.sh            # version from Directory.Build.props
build/package.sh 0.2.0      # or a version of your own
build/package.sh 0.2.0 ObenseuerExtended-0.2.0-57a4977_pre-release   # and a name for the folder and zip
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

## Pre-releases

```sh
build/next-version.sh             # the version the commits call for
build/experimental.sh --dry-run   # what it would do, and the notes
build/experimental.sh             # test, commit the version, package, tag, push and publish
build/experimental.sh --local     # test and package only (the version commit is taken back)
build/experimental.sh --patch     # a patch version even though there are feat commits
```

Publish one every few commits, or when a milestone is done, from a clean tree on `main`. Before 1.0.0 the script:

1. Works out the version with `build/next-version.sh`.
2. Stops when the tree has uncommitted changes, the commit is already released, the tag exists, or nothing is new since the previous release.
3. Runs the tests, sets `<Version>` and commits "chore(release): X.Y.Z", writes `changelog.json` with that version released (`build/changelog.sh --release X.Y.Z`), and builds `dist/ObenseuerExtended-X.Y.Z-<commit>_pre-release.zip` from that commit.
4. Tags `vX.Y.Z`, pushes the branch and the tag in one go (`--atomic`), and creates a GitHub pre-release titled "ObenseuerExtended vX.Y.Z (<commit>)". Until the push succeeds everything is local, and a failed step takes the version commit and the tag back; if only the GitHub release fails it prints the command to make it.

After 1.0.0 it publishes test builds of the coming version without a version commit: `X.Y.Z-pre.N` (one more than the highest number used for that version), built with that version so the game shows it, tagged `vX.Y.Z-pre.N+<commit>`. Those tags don't count as releases in `changelog.json` or for `release.sh`.

The notes start with a warning to back up saves, then the player lines since the previous release (`oben changelog --since <tag>`) and every commit since then (the newest 300) in a folded list.

## Making a stable release

Only when the owner asks for one, and from 1.0.0 on (`release.sh` refuses a 0.x version: those are pre-releases).

1. Look at what changed (`git log v<last>..HEAD`) and decide `Major.Minor.Patch`; `build/next-version.sh` says what the commit types call for. The first one is 1.0.0, when everything in PROGRESS.md is at 100%.
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

Stable releases (1.0.0 and later) only happen when the owner asks; pre-releases go out through `experimental.sh` as described above. Never tag or push by hand to try something out: the tag is the release.

## The wiki

The [GitHub wiki](https://github.com/Official-Husko/openseuer-framework/wiki) is for players: installing, each mod, reporting bugs, troubleshooting. Its pages live in `wiki/` (`Home.md`, `_Sidebar.md`, `_Footer.md` and a file per page, named like the page with dashes for spaces) and are edited there with the code. `build/wiki.sh` replaces the wiki's pages with them and pushes (`--dry-run` shows the changes first). Run it after a release, or when a page changed; a page that describes something new should go out with the release that brings it.

