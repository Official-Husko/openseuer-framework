#!/usr/bin/env bash
# Prepares a stable release of ObenseuerExtended, 1.0.0 or later, only when the owner asks for one.
#
#   build/release.sh <X.Y.Z> [--dry-run] [--publish] [--yes]
#
# build/next-version.sh says which version the commits since the last release call for.
# Checks that the tree is clean, runs the tests, sets <Version> in Directory.Build.props, commits
# "chore(release): X.Y.Z", writes changelog.json with that version released (build/changelog.sh), builds
# the package from that commit and tags vX.Y.Z. Nothing leaves the machine: pushing and the GitHub
# release only happen with --publish (and a confirmation, unless --yes). Without it the script prints
# the commands.
# If a step fails before the tag exists, the release commit is taken back.
#
# --dry-run changes nothing: it reports what it finds and prints the release notes, skipping the tests.
set -euo pipefail

repo=$(cd "$(dirname "$0")/.." && pwd)
cd "$repo"

die() {
    echo "release: $*" >&2
    exit 1
}

version=${1:-}
[ -n "$version" ] && [[ $version != -* ]] || die "usage: build/release.sh <X.Y.Z> [--dry-run] [--publish] [--yes]"
shift
dry_run=false
publish=false
assume_yes=false
for arg in "$@"; do
    case $arg in
        --dry-run) dry_run=true ;;
        --publish) publish=true ;;
        --yes) assume_yes=true ;;
        *) die "unknown option $arg" ;;
    esac
done
[[ $version =~ ^[0-9]+\.[0-9]+\.[0-9]+$ ]] || die "$version isn't a version (Major.Minor.Patch)"
# Versions before 1.0.0 are pre-releases from build/experimental.sh (the versioning rule in docs/modding/releasing.md).
[ "${version%%.*}" -ge 1 ] || die "$version is before 1.0.0: those are pre-releases, published with build/experimental.sh"

# A real run stops at a problem; a dry run lists them and carries on so the notes can still be read.
problems=0
problem() {
    if $dry_run; then
        echo "release: problem: $*" >&2
        problems=$((problems + 1))
    else
        die "$*"
    fi
}

[ -z "$(git status --porcelain)" ] || problem "the working tree has uncommitted changes"
branch=$(git branch --show-current)
# Numbered releases are vX.Y.Z tags (0.x ones are pre-releases); test builds after 1.0.0 are vX.Y.Z-pre.N+<commit>.
last=$(git tag --list 'v*' --sort=-v:refname | grep -E '^v[0-9]+\.[0-9]+\.[0-9]+$' | head -n 1 || true)
last=${last#v}
! git rev-parse -q --verify "refs/tags/v$version" > /dev/null || problem "v$version already exists"
if [ -n "$last" ]; then
    newest=$(printf '%s\n%s\n' "$last" "$version" | sort -V | tail -n 1)
    { [ "$newest" = "$version" ] && [ "$version" != "$last" ]; } || problem "$version isn't newer than the last release ($last)"
fi

dotnet build tools/ObenTool -c Release -p:ObenDeploy=false -v quiet -nologo > /dev/null
oben() { "$repo/tools/ObenTool/bin/Release/net10.0/oben" "$@"; }

# The notes: the changelog lines for this version and how to install.
notes=$(mktemp)
trap 'rm -f "$notes"' EXIT
{
    oben changelog --release "$version" --notes
    echo
    echo "---"
    echo
    echo "**Installing:** download \`ObenseuerExtended-$version.zip\`, unpack it, close the game and run \`install.bat\` (Windows) or \`install.sh\` (Linux)."
} > "$notes"

echo "Releasing $version (last release: ${last:-none}) from ${branch:-a detached head}"
echo "----- notes -----"
cat "$notes"
echo "-----------------"

if $dry_run; then
    if [ "$problems" -gt 0 ]; then
        echo "Dry run: nothing was changed. $problems problem(s) above would stop a real release."
    else
        echo "Dry run: nothing was changed."
    fi
    exit 0
fi

if $publish; then
    gh auth status > /dev/null 2>&1 || die "gh isn't logged in (gh auth login)"
    [ -n "$branch" ] || die "--publish needs a branch, not a detached head"
fi

echo "Running the tests..."
dotnet test ObenseuerExtended.slnx -c Release -p:ObenDeploy=false -v quiet -nologo

# Local part: undone if anything fails before the tag exists.
start=$(git rev-parse HEAD)
rollback() {
    echo "release: failed, taking back the release commit" >&2
    git tag -d "v$version" > /dev/null 2>&1 || true
    git reset -q --hard "$start"
}
trap 'rollback' ERR

# The changelog hooks would write the file again after the commit, before the tag exists, so they sit
# this one out; the release writes it itself.
export OBEN_CHANGELOG_HOOKS=0
sed -i "s|<Version>[^<]*</Version>|<Version>$version</Version>|" Directory.Build.props
git add Directory.Build.props
git commit -q -m "chore(release): $version" -m "Sets the version to $version."
build/changelog.sh --release "$version" --quiet

# Built from the release commit, so the assemblies carry its hash without "-dirty".
build/package.sh "$version"
zip="dist/ObenseuerExtended-$version.zip"
[ -f "$zip" ] || false

# Verbatim, or git drops the notes' "### Added" headings as comments.
git tag -a --cleanup=verbatim "v$version" -m "ObenseuerExtended $version" -m "$(cat "$notes")"
trap - ERR
echo
echo "Release $version is committed, packaged ($zip) and tagged v$version. Nothing has been pushed."

push_cmd="git push origin $branch v$version"
release_cmd="gh release create v$version $zip --title \"ObenseuerExtended $version\" --notes-file <notes>"
if ! $publish; then
    echo "To publish it:"
    echo "  $push_cmd"
    echo "  $release_cmd"
    exit 0
fi

echo "About to run:"
echo "  $push_cmd"
echo "  $release_cmd"
if ! $assume_yes; then
    read -r -p "Publish v$version? [y/N] " answer
    [[ $answer == [yY]* ]] || die "stopped; the commit, package and tag stay local"
fi
git push origin "$branch" "v$version"
gh release create "v$version" "$zip" --title "ObenseuerExtended $version" --notes-file "$notes"
echo "Released v$version."
