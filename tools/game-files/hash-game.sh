#!/bin/sh
# Writes the SHA-256 hash of every file of an Obenseuer install to a JSON list, for verify-game.sh.
#
#   hash-game.sh [game folder] [list.json] [--version 0.5.13] [--build 26179069] [--exclude 'pattern' ...]
#
# The game folder defaults to $OBENSEUER_DIR, else the current folder. The list defaults to
# hashes/<version>.json next to this script. What ObenseuerExtended adds (Mods, ObenLoader, the loader's
# DLLs) and store-specific files (Steam, GOG, Epic integration) are left out; files the installer changed
# are hashed from the original it kept (*.obenbak). --exclude takes shell patterns of more paths to skip,
# relative to the game folder ("Logs/*").
set -eu

here=$(cd "$(dirname "$0")" && pwd)
game=""
out=""
version=""
build=""
excludes=""

while [ $# -gt 0 ]; do
    case "$1" in
        --version) version=$2; shift 2 ;;
        --build) build=$2; shift 2 ;;
        --exclude) excludes="$excludes
$2"; shift 2 ;;
        -h|--help) sed -n '2,11p' "$0" | sed 's/^# \{0,1\}//'; exit 0 ;;
        *) if [ -z "$game" ]; then game=$1; else out=$1; fi; shift ;;
    esac
done

[ -n "$game" ] || game=${OBENSEUER_DIR:-.}
[ -d "$game/Obenseuer_Data" ] || { echo "hash-game: $game isn't an Obenseuer folder (no Obenseuer_Data in it). Pass the game folder." >&2; exit 1; }
game=$(cd "$game" && pwd)
[ -n "$version" ] || version=unknown
[ -n "$out" ] || out="$here/hashes/$version.json"
mkdir -p "$(dirname "$out")"

if command -v sha256sum >/dev/null 2>&1; then
    sha() { sha256sum "$1" | cut -d' ' -f1; }
else
    sha() { shasum -a 256 "$1" | cut -d' ' -f1; }
fi

skip() {
    case "$1" in
        Mods/*|ObenLoader/*|BepInEx/*|*.obenbak) return 0 ;;
        Obenseuer_Data/Managed/ObenLoader.*|Obenseuer_Data/Managed/0Harmony.dll|Obenseuer_Data/Managed/Mono.Cecil*.dll) return 0 ;;
        # Store integration differs between Steam, GOG and Epic copies of the same game version.
        */steam_api*|steam_api*|*/steam_appid.txt|steam_appid.txt|*/steam_settings/*|*/Galaxy*.dll|*goggame-*|*/EOSSDK*) return 0 ;;
    esac
    old_ifs=$IFS
    IFS='
'
    for pattern in $excludes; do
        # shellcheck disable=SC2254
        case "$1" in $pattern) IFS=$old_ifs; return 0 ;; esac
    done
    IFS=$old_ifs
    return 1
}

tmp=$(mktemp)
trap 'rm -f "$tmp"' EXIT
count=0
echo "Hashing $game ..."
(cd "$game" && find . -type f | sed 's|^\./||' | LC_ALL=C sort) | while IFS= read -r file; do
    skip "$file" && continue
    source="$game/$file"
    # The installer keeps the original of each file it changes; that is the game's file.
    [ -f "$source.obenbak" ] && source="$source.obenbak"
    escaped=$(printf '%s' "$file" | sed 's/\\/\\\\/g; s/"/\\"/g')
    printf '    "%s": "%s",\n' "$escaped" "$(sha "$source")"
done > "$tmp"
count=$(wc -l < "$tmp" | tr -d ' ')

{
    echo "{"
    echo "  \"game\": \"Obenseuer\","
    echo "  \"version\": \"$version\","
    [ -z "$build" ] || echo "  \"steamBuild\": \"$build\","
    echo "  \"files\": {"
    sed '$ s/,$//' "$tmp"
    echo "  }"
    echo "}"
} > "$out"
echo "Wrote $count files to $out"
