#!/usr/bin/env bash
# Every mod in src/ and mods/ has its own Major.Minor.Patch version in its mod.json (all started at 1.0.0 with
# the pack's first stable release). A mod that changed gets a new one with the release that ships the change,
# by the same rule as the pack's version (docs/modding/releasing.md): the commits that touched the mod's folder
# since its version last changed decide it, a breaking change (type!: or a "BREAKING CHANGE" line) bumps the
# major version, a feature (feat) the minor version, anything else the patch version. Markdown files don't
# count. A project whose DLL ships in the mod's folder (a ProjectReference with Private="true", like
# ObenCoop.Net in ObenCoop) counts as part of the mod. A version raised by hand in a commit counts as the new
# start, so it isn't raised twice.
#
#   build/mod-versions.sh            prints every mod's version and, for the ones that changed, the next one
#   build/mod-versions.sh --apply    writes the new versions into the mod.json files and prints them
set -euo pipefail

repo=$(cd "$(dirname "$0")/.." && pwd)
cd "$repo"

apply=false
for arg in "$@"; do
    case $arg in
        --apply) apply=true ;;
        *) echo "mod-versions: unknown option $arg (build/mod-versions.sh [--apply])" >&2; exit 1 ;;
    esac
done

for manifest in src/*/mod.json mods/*/mod.json; do
    [ -f "$manifest" ] || continue
    dir=$(dirname "$manifest")
    id=$(sed -n 's:.*"id"[[:space:]]*\:[[:space:]]*"\([^"]*\)".*:\1:p' "$manifest" | head -n 1)
    current=$(sed -n 's:.*"version"[[:space:]]*\:[[:space:]]*"\([^"]*\)".*:\1:p' "$manifest" | head -n 1)
    [[ $current =~ ^[0-9]+\.[0-9]+\.[0-9]+$ ]] || { echo "mod-versions: $manifest has no Major.Minor.Patch version" >&2; exit 1; }

    # The commit that last set the version (uncommitted edits to it count as set now).
    if ! git diff --quiet HEAD -G'"version"[[:space:]]*:' -- "$manifest"; then
        echo "$id $current (set, not committed yet)"
        continue
    fi
    since=$(git log -1 --format=%H -G'"version"[[:space:]]*:' -- "$manifest")
    range=${since:+$since..}HEAD
    paths=("$dir")
    for shipped in $(sed -n 's:.*<ProjectReference Include="\([^"]*\)"[^>]*Private="true".*:\1:p' "$dir"/*.csproj 2>/dev/null); do
        paths+=("$(realpath --relative-to="$repo" "$dir/$(dirname "$shipped")")")
    done
    subjects=$(git log --no-merges --format=%s "$range" -- "${paths[@]}" ':(exclude,glob)**/*.md')
    if [ -z "$subjects" ]; then
        echo "$id $current"
        continue
    fi
    bodies=$(git log --no-merges --format=%b "$range" -- "${paths[@]}" ':(exclude,glob)**/*.md')
    IFS=. read -r major minor patch <<< "$current"
    if grep -qE '^[a-z]+(\([^)]*\))?!:' <<< "$subjects" || grep -q '^BREAKING CHANGE' <<< "$bodies"; then
        next="$((major + 1)).0.0" reason="a breaking change"
    elif grep -qE '^feat(\([^)]*\))?:' <<< "$subjects"; then
        next="$major.$((minor + 1)).0" reason="new features"
    else
        next="$major.$minor.$((patch + 1))" reason="fixes and other work only"
    fi
    count=$(grep -c . <<< "$subjects")
    if $apply; then
        build/set-version.sh --manifest "$next" "$manifest"
        echo "$id $current -> $next ($reason, $count commits)"
    else
        echo "$id $current -> $next ($reason, $count commits)"
    fi
done
