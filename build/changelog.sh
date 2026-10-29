#!/usr/bin/env bash
# Writes changelog.json, the release notes the What's New page in MODS shows, from the git history: the
# Changelog-* trailers of the commits, grouped into versions by the vX.Y.Z tags (see docs/modding/changelog.md).
# The file is generated, never edited by hand, and not committed; the kit's build copies it into
# Mods/ObenUIKit, and this script also refreshes the copy in the game folder, which a running game reloads.
#
#   build/changelog.sh                  write changelog.json and list what is in it
#   build/changelog.sh --release X.Y.Z  count the commits since the last tag as X.Y.Z, released today
#   build/changelog.sh --check          only check that changelog.json is there and up to date
#   build/changelog.sh --hooks          keep it up to date by itself: git hooks run this script after
#                                       commits, merges, rebases and branch switches
#   build/changelog.sh --no-hooks       take those hooks out again
#   --quiet                             no output (what the hooks use)
#
# OBEN_CHANGELOG_HOOKS=0 in the environment makes the hooks skip a run (release.sh sets it).
set -euo pipefail

repo=$(cd "$(dirname "$0")/.." && pwd)
cd "$repo"
out="$repo/changelog.json"
hooks=(post-commit post-merge post-rewrite post-checkout)
marker="# changelog.json (build/changelog.sh)"

die() {
    echo "changelog: $*" >&2
    exit 1
}

release=""
mode=write
quiet=false
while [ $# -gt 0 ]; do
    case $1 in
        --release)
            release=${2:-}
            [[ $release =~ ^[0-9]+\.[0-9]+\.[0-9]+$ ]] || die "--release needs a version (Major.Minor.Patch)"
            shift
            ;;
        --check) mode=check ;;
        --hooks) mode=hooks ;;
        --no-hooks) mode=no-hooks ;;
        --quiet) quiet=true ;;
        *) die "unknown option $1 (see the top of build/changelog.sh)" ;;
    esac
    shift
done

say() {
    $quiet || echo "$@"
}

hook_dir=$(git rev-parse --git-path hooks)

install_hooks() {
    mkdir -p "$hook_dir"
    for name in "${hooks[@]}"; do
        local file="$hook_dir/$name"
        if [ -f "$file" ] && ! grep -qF "$marker" "$file"; then
            say "  $name: a hook of its own is already there, left alone. Add this line to it:"
            say "    \"\$(git rev-parse --show-toplevel)/build/changelog.sh\" --quiet &"
            continue
        fi
        # post-checkout also runs for single files; only a branch switch ($3 = 1) changes the history.
        local only=""
        [ "$name" = post-checkout ] && only='[ "${3:-0}" = 1 ] || exit 0'
        cat > "$file" << EOF
#!/usr/bin/env bash
$marker
[ "\${OBEN_CHANGELOG_HOOKS:-1}" = 0 ] && exit 0
$only
script="\$(git rev-parse --show-toplevel)/build/changelog.sh"
[ -x "\$script" ] || exit 0
# In the background, so git doesn't wait for the build tool.
nohup "\$script" --quiet > /dev/null 2>&1 &
EOF
        chmod +x "$file"
        say "  $name: installed"
    done
}

remove_hooks() {
    for name in "${hooks[@]}"; do
        local file="$hook_dir/$name"
        if [ -f "$file" ] && grep -qF "$marker" "$file"; then
            rm "$file"
            say "  $name: removed"
        fi
    done
}

case $mode in
    hooks)
        say "Git hooks that keep changelog.json up to date:"
        install_hooks
        exit 0
        ;;
    no-hooks)
        remove_hooks
        exit 0
        ;;
esac

# The oben tool, built again when its sources are newer than the build.
tool="$repo/tools/ObenTool/bin/Release/net10.0/oben.dll"
if [ ! -f "$tool" ] || [ -n "$(find "$repo/tools/ObenTool" -path '*/bin' -prune -o -path '*/obj' -prune -o \
        -newer "$tool" \( -name '*.cs' -o -name '*.csproj' \) -print -quit)" ]; then
    say "Building the oben tool..."
    dotnet build "$repo/tools/ObenTool" -c Release -p:ObenDeploy=false -v quiet -nologo > /dev/null \
        || die "the oben tool didn't build (dotnet build tools/ObenTool)"
fi

args=(changelog --write)
[ -n "$release" ] && args+=(--release "$release")

# Written next to the real file and moved over it, so the game never reads half a file. Two runs at once
# (hooks after a rebase) each have their own temp file.
tmp="$out.$$.tmp"
trap 'rm -f "$tmp"' EXIT
dotnet "$tool" "${args[@]}" --out "$tmp" > /dev/null || die "oben changelog failed"

if [ "$mode" = check ]; then
    [ -f "$out" ] || die "changelog.json is missing; run build/changelog.sh"
    cmp -s "$tmp" "$out" || die "changelog.json is out of date; run build/changelog.sh"
    say "changelog.json is up to date."
    exit 0
fi

if [ -f "$out" ] && cmp -s "$tmp" "$out"; then
    rm -f "$tmp"
    say "changelog.json is already up to date."
else
    mv -f "$tmp" "$out"
    say "Wrote changelog.json."
fi

# The kit deployed in the game folder reads its own copy; refresh it so a running game shows the new notes.
game=${OBENSEUER_DIR:-}
if [ -z "$game" ] && [ -f "$repo/build/Local.props" ]; then
    game=$(sed -n 's:.*<ObenGameDir>\(.*\)</ObenGameDir>.*:\1:p' "$repo/build/Local.props" | head -n 1)
fi
if [ -n "$game" ] && [ -d "$game/Mods/ObenUIKit" ] && ! cmp -s "$out" "$game/Mods/ObenUIKit/changelog.json"; then
    cp "$out" "$game/Mods/ObenUIKit/changelog.json.tmp"
    mv -f "$game/Mods/ObenUIKit/changelog.json.tmp" "$game/Mods/ObenUIKit/changelog.json"
    say "Updated the copy in $game/Mods/ObenUIKit."
fi

# What is in it: one line per version.
if ! $quiet && command -v python3 > /dev/null; then
    python3 - "$out" << 'EOF'
import json, sys
releases = json.load(open(sys.argv[1], encoding="utf-8"))["releases"]
if not releases:
    print("  (no lines yet: no commit has a Changelog- trailer)")
for r in releases:
    name = "Version " + r["version"] if r["released"] else "Unreleased"
    counts = ", ".join(f"{len(r[k])} {k}" for k in ("added", "changed", "fixed", "removed") if r[k])
    print(f"  {name} ({r['date']}): {counts}")
EOF
fi
