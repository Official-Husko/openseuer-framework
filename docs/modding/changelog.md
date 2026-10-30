# Changelog

The changelog is not written by hand. It comes from the commit history: a commit with a player-facing change ends with trailer lines, and `oben changelog` collects them into `changelog.json`. The in-game What's New reads that file. Never edit `changelog.json` yourself.

## Writing trailers

Put one or more trailers at the end of the commit message, after a blank line:

```text
fix(patches): police see robbers they can actually see

The body says what changed and why, for developers. Only the trailer
below is shown to players.

Changelog-Fixed: Robbers notice police that can actually see them.
```

| Trailer | Use it for |
| --- | --- |
| `Changelog-Added:` | Something new players can use or see. |
| `Changelog-Changed:` | Something that behaves differently than before. |
| `Changelog-Fixed:` | A bug players could run into. |
| `Changelog-Removed:` | Something that is gone. |

Rules for the line:

- Write it for players: short, plain, one sentence. No class names, method names or patch names.
- Say what the player notices, not what the code does ("Robbers notice police that can actually see them", not "Fixed the line of sight check").
- Don't start with the mod's name. The commit scope already puts the line under the right mod, and a leading "Dev bridge:" is dropped anyway.
- Don't write a `Changed` or `Fixed` line for something that is new in the same release. Players never had the old version.
- Only trailers count. A plain `feat` or `fix` commit without a trailer (tooling, docs, refactors, research) never appears in the changelog. The same line in two commits is shown once.
- A commit can have several trailers, and mixed kinds.

Trailers in pushed commits can't be changed, so read the lines before pushing (see below).

## Which mod a line belongs to

The scope of the commit subject (`fix(patches): ...`) decides. `ModAreas` in `tools/ObenTool/Changelog/ModAreas.cs` maps scopes to the names players see:

| Scope | Shown as |
| --- | --- |
| `loader` | Mod loader |
| `framework` | Framework |
| `kit`, `uikit`, `ui` | UI kit |
| `bridge` | Dev bridge |
| `scripts` | Script Runner |
| `patches` | Unofficial ObenPatches |
| `tool`, `mcp` | Command-line tool |
| `coop` | ObenCoop |
| `perf` | Performance Fixes |
| `save` | ObenSave |
| `cheats` | Cheat Menu |
| `tweaks` | Game Tweaks |

A scope that isn't in the table is shown as it is, capitalized. A new mod with its own scope gets one line in `ModAreas`. A commit without a scope has its lines listed without a mod (as "General" in the game).

Inside a release the mods come in alphabetical order, lines without a mod last, and a mod's lines keep the order of their commits.

## Versions

Versions are `Major.Minor.Patch` and every release is a git tag `vX.Y.Z`. Each tag closes a release made of the commits since the previous tag. Commits after the newest tag are listed as `Unreleased` (`"released": false` in the file). Releases with no trailer lines are left out. Experimental builds are tagged `vX.Y.Z-pre.N+<commit>` ([releasing.md](releasing.md)); those tags don't close a release, so their changes stay `Unreleased` until the next stable one.

Don't create a tag to try something out: the tag is the release. Before 1.0, breaking changes bump the minor version, new features the minor version, and fixes and tweaks the patch version.

## Commands

| Command | What it does |
| --- | --- |
| `build/changelog.sh` | Writes `changelog.json` in the repo root, refreshes the copy in the game folder's `Mods/ObenUIKit` (a running game picks it up), and lists the versions in it. Builds the `oben` tool first when its sources changed. |
| `build/changelog.sh --release X.Y.Z` | The same, with the commits since the last tag counted as `X.Y.Z`, released today. `release.sh` uses it. |
| `build/changelog.sh --check` | Fails when `changelog.json` is missing or doesn't match the history. Writes nothing. |
| `build/changelog.sh --hooks` | Installs git hooks (post-commit, post-merge, post-rewrite and branch switches) that run the script in the background, so the file is always current. `--no-hooks` takes them out; a hook of your own is left alone and the script says what line to add to it. |
| `oben changelog` | Prints the changelog JSON built from the history. Writes nothing. |
| `oben changelog --write [--out FILE]` | Writes `changelog.json` in the repo root (or to `FILE`). |
| `oben changelog --release X.Y.Z --notes` | Prints markdown release notes for the commits since the last tag, as if they were released as `X.Y.Z` today. Also works as `--notes X.Y.Z`. |
| `oben changelog --release X.Y.Z` | The JSON with the unreleased commits counted as `X.Y.Z`, to see what the file will look like after the tag. |
| `oben changelog --since REF` | Prints markdown notes for the commits after `REF` (a tag or commit), nothing when none has a line for players. `experimental.sh` uses it with the previous release's tag. |

Run it from inside the repo. `--release` ignores a version that already has a tag.

## How What's New finds the file

`changelog.json` is generated, so it isn't committed (`.gitignore`); it can always be built again from the commits and tags. Run `build/changelog.sh` after committing (or install the hooks once), and `release.sh` writes it for a release. `ObenUIKit.csproj` copies it from the repo root into the kit's mod folder (`Mods/ObenUIKit/changelog.json`) when it exists at build time, and the release zip carries that copy. The What's New page reads it from there, newest release first, and again whenever the file changes. Without the file the page says "No changelog found." and nothing else breaks.

The newest published release (the first one with `"released": true`; `Unreleased` never counts) is remembered in the kit's `config.json` as `LastSeenVersion`. When a newer one is installed, the mod menu opens on What's New once at the main menu, after the kit has taken the game's look. The "Show What's New after updates" setting turns the popup off; the page stays in the menu either way.

## Checking before a release

Read the release notes the way a player would, with `oben changelog --release X.Y.Z --notes`. Look for:

- the same line twice, or two lines saying the same thing (often a feature line plus a later "made it better" line);
- a mod's name repeated inside its own line, or a line under the wrong mod;
- `Changed` or `Fixed` lines for something that is new in this release;
- code names, internal details and typos.

A wrong trailer in a commit that isn't pushed yet is fixed by rewording that commit. Formatting problems are fixed in the builder (`tools/ObenTool/Changelog/`).
