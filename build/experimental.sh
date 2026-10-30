#!/usr/bin/env bash
# Publishes an experimental build of ObenseuerExtended as a GitHub pre-release.
#
#   build/experimental.sh [--dry-run] [--local]
#
# The version is the last stable release (the newest vX.Y.Z tag, or <Version> in Directory.Build.props before the
# first one) and the commit: "0.1.0 (57a4977)", titled "ObenseuerExtended v0.1.0 (57a4977)", the zip named
# ObenseuerExtended-0.1.0-57a4977_pre-release.zip. The pause menu shows the same string, so a player can tell which
# build is installed.
#
# The tag is numbered, v0.1.0-pre.5+57a4977, because GitHub orders releases by the version in the tag and ignores
# everything after "+": with the commit alone every pre-release would be the same version and land in any order.
# A pre-release comes before its version in that order, so the tags name the version the pre-releases lead up to:
# <Version> before the first stable release, the next patch version after one (v0.1.1-pre.1 follows v0.1.0). Checks that the tree is clean and that something is new since the last
# release of either kind, runs the tests, writes changelog.json, builds the package from HEAD, tags it, pushes the
# branch and the tag and creates the pre-release. The notes are the player lines since the previous release and
# every commit since then.
#
# Experimental builds go out every few commits or when a milestone is done. Stable releases are build/release.sh
# X.Y.Z, only when the owner asks for one.
#
# --dry-run changes nothing: it reports what it finds and prints the notes, skipping the tests.
# --local   stops after the package: no tag, nothing pushed.
set -euo pipefail

repo=$(cd "$(dirname "$0")/.." && pwd)
cd "$repo"

die() {
    echo "experimental: $*" >&2
    exit 1
}

dry_run=false
local_only=false
for arg in "$@"; do
    case $arg in
        --dry-run) dry_run=true ;;
        --local) local_only=true ;;
        *) die "unknown option $arg (build/experimental.sh [--dry-run] [--local])" ;;
    esac
done

# A real run stops at a problem; a dry run lists them and carries on so the notes can still be read.
problems=0
problem() {
    if $dry_run; then
        echo "experimental: problem: $*" >&2
        problems=$((problems + 1))
    else
        die "$*"
    fi
}

[ -z "$(git status --porcelain)" ] || problem "the working tree has uncommitted changes"
branch=$(git branch --show-current)
[ -n "$branch" ] || problem "not on a branch (a detached head can't be pushed)"

stable=$(git tag --list 'v*' --sort=-v:refname | grep -E '^v[0-9]+\.[0-9]+\.[0-9]+$' | head -n 1 || true)
if [ -n "$stable" ]; then
    base=${stable#v}
else
    base=$(sed -n 's:.*<Version>\(.*\)</Version>.*:\1:p' Directory.Build.props | head -n 1)
fi
[[ $base =~ ^[0-9]+\.[0-9]+\.[0-9]+$ ]] || die "can't tell the last version (got '$base')"
# The same abbreviation build/Revision.targets stamps into the assemblies.
commit=$(git describe --always --abbrev=7 --exclude='*')
version="$base ($commit)"
name="ObenseuerExtended-$base-${commit}_pre-release"

# Pre-releases published from another clone count too.
git fetch -q --tags origin 2> /dev/null || true
if [ -n "$stable" ]; then
    IFS=. read -r major minor patch <<< "$base"
    upcoming="$major.$minor.$((patch + 1))"
else
    upcoming=$base
fi
last_number=$(git tag --list "v$upcoming-pre.*" | sed -nE "s/^v[0-9.]+-pre\.([0-9]+)(\+.*)?$/\1/p" | sort -n | tail -n 1)
tag="v$upcoming-pre.$((${last_number:-0} + 1))+$commit"

released=$(git tag --points-at HEAD --list 'v*' | head -n 1)
[ -z "$released" ] || problem "this commit is already released as $released"
previous=$(git describe --tags --abbrev=0 --match 'v*' 2>/dev/null || true)
if [ -n "$previous" ]; then
    since=$previous
    since_text="since $previous"
else
    since=$(git rev-list --max-parents=0 HEAD | tail -n 1)
    since_text="since the start"
fi
count=$(git rev-list --count --no-merges "$since..HEAD")
[ "$count" -gt 0 ] || problem "nothing new $since_text"

dotnet build tools/ObenTool -c Release -p:ObenDeploy=false -v quiet -nologo > /dev/null
oben() { "$repo/tools/ObenTool/bin/Release/net10.0/oben" "$@"; }

notes=$(mktemp)
trap 'rm -f "$notes"' EXIT
{
    echo "A pre-release: the newest changes, each checked by itself but not tried together as a release yet. Back up your saves before playing with it. The pause menu shows \`ObenseuerExtended v$version\` when this build is installed."
    echo
    echo "## What's new $since_text"
    echo
    lines=$(oben changelog --since "$since")
    if [ -n "$lines" ]; then
        echo "$lines"
    else
        echo "Nothing players will notice: tools, documentation or work under the hood."
    fi
    echo
    echo "<details><summary>All $count commits $since_text</summary>"
    echo
    git log --no-merges -n 300 --format='- `%h` %s' "$since..HEAD"
    [ "$count" -le 300 ] || echo "- ... and $((count - 300)) older ones"
    echo
    echo "</details>"
    echo
    echo "---"
    echo
    echo "**Installing:** download \`$name.zip\`, unpack it, close the game and run \`install.bat\` (Windows) or \`install.sh\` (Linux)."
} > "$notes"

echo "Pre-release $version from ${branch:-a detached head}, $count commits $since_text"
echo "----- notes -----"
cat "$notes"
echo "-----------------"

if $dry_run; then
    if [ "$problems" -gt 0 ]; then
        echo "Dry run: nothing was changed. $problems problem(s) above would stop a real run."
    else
        echo "Dry run: nothing was changed. A real run tags $tag and publishes $name.zip."
    fi
    exit 0
fi

$local_only || gh auth status > /dev/null 2>&1 || die "gh isn't logged in (gh auth login)"

echo "Running the tests..."
dotnet test ObenseuerExtended.slnx -c Release -p:ObenDeploy=false -v quiet -nologo

# The kit's build copies changelog.json into the package, so it should list everything up to this commit.
build/changelog.sh --quiet
# Built with the plain version; build/Revision.targets adds the commit to every assembly, without "-dirty".
build/package.sh "$base" "$name"
zip="dist/$name.zip"
[ -f "$zip" ] || die "the package wasn't made"

if $local_only; then
    echo "Packaged $zip. Nothing was tagged or pushed."
    exit 0
fi

# Verbatim, or git drops the notes' "## What's new" heading as a comment.
git tag -a --cleanup=verbatim "$tag" -m "ObenseuerExtended $version, pre-release" -m "$(cat "$notes")"
if ! git push origin "$branch" "$tag"; then
    git tag -d "$tag" > /dev/null
    die "pushing failed; the tag was taken back and nothing was published"
fi
if ! gh release create "$tag" "$zip" --prerelease --title "ObenseuerExtended v$version" --notes-file "$notes"; then
    die "the tag is pushed but the release wasn't made; run: gh release create '$tag' '$zip' --prerelease --title 'ObenseuerExtended v$version' --notes-file <notes>"
fi
echo "Published the pre-release $version ($tag)."
