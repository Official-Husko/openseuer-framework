#!/bin/sh
# Checks an Obenseuer install against a hash list from hash-game.sh and lists every file that is changed or
# missing. Handy when the game or a mod misbehaves and you want to rule out a broken install.
#
#   verify-game.sh [game folder] [list.json]
#
# The game folder defaults to $OBENSEUER_DIR, else the current folder. Without a list, the one in
# hashes/ next to this script whose Assembly-CSharp.dll matches the game is used. Files ObenseuerExtended
# adds are never in the lists; files its installer changed are checked through the original it kept
# (*.obenbak). Exits with 0 when everything matches, 1 when something doesn't, 2 when it can't check.
set -eu

here=$(cd "$(dirname "$0")" && pwd)
game=${1:-${OBENSEUER_DIR:-.}}
list=${2:-}
[ "$game" != "-h" ] && [ "$game" != "--help" ] || { sed -n '2,10p' "$0" | sed 's/^# \{0,1\}//'; exit 0; }
[ -d "$game/Obenseuer_Data" ] || { echo "verify-game: $game isn't an Obenseuer folder (no Obenseuer_Data in it). Pass the game folder." >&2; exit 2; }
game=$(cd "$game" && pwd)

if command -v sha256sum >/dev/null 2>&1; then
    sha() { sha256sum "$1" | cut -d' ' -f1; }
else
    sha() { shasum -a 256 "$1" | cut -d' ' -f1; }
fi

# The list's entries as "hash path" lines (one entry per line, as hash-game writes them).
entries() {
    tr -d '\r' < "$1" | sed -n 's/^    "\(.*\)": "\([0-9a-f]\{64\}\)",\{0,1\}$/\2 \1/p' | sed 's/\\"/"/g; s/\\\\/\\/g'
}

code="Obenseuer_Data/Managed/Assembly-CSharp.dll"
if [ -z "$list" ]; then
    mine=$(sha "$game/$code")
    for candidate in "$here"/hashes/*.json; do
        [ -f "$candidate" ] || continue
        if entries "$candidate" | grep -q "^$mine $code\$"; then
            list=$candidate
            break
        fi
    done
    [ -n "$list" ] || { echo "verify-game: no list in $here/hashes matches this game's code (Assembly-CSharp.dll $(printf '%.12s' "$mine")). Its version may be newer than these lists; pass a list made with hash-game.sh." >&2; exit 2; }
fi
[ -f "$list" ] || { echo "verify-game: no list at $list" >&2; exit 2; }

version=$(tr -d '\r' < "$list" | sed -n 's/^  "version": "\(.*\)",$/\1/p')
echo "Checking $game against $(basename "$list") (game version ${version:-unknown})..."
report=$(mktemp)
trap 'rm -f "$report"' EXIT
entries "$list" | while read -r hash path; do
    file="$game/$path"
    [ -f "$file.obenbak" ] && file="$file.obenbak"
    if [ ! -f "$file" ]; then
        echo "missing  $path"
    elif [ "$(sha "$file")" != "$hash" ]; then
        echo "changed  $path"
    fi
done > "$report"

total=$(entries "$list" | wc -l | tr -d ' ')
problems=$(wc -l < "$report" | tr -d ' ')
if [ "$problems" -eq 0 ]; then
    echo "All $total files match."
    exit 0
fi
cat "$report"
echo "$problems of $total files don't match. Verify the game's files in your store (Steam: Properties > Installed Files > Verify) or reinstall it."
exit 1
