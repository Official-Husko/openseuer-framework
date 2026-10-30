#!/usr/bin/env bash
# Publishes the next version of ObenseuerExtended as a GitHub pre-release.
#
#   build/experimental.sh [--dry-run] [--local] [--minor | --patch]
#
# Until 1.0.0 every pre-release is a version of its own (docs/modding/releasing.md, worked out by
# build/next-version.sh): a feature bumps the minor version, fixes and other work the patch version, 0.2.0 -> 0.2.1.
# The release sets <Version> in Directory.Build.props, commits "chore(release): 0.2.1", writes changelog.json with
# that version released, builds the package from that commit, tags v0.2.1, pushes the branch and the tag and creates
# the pre-release, titled "ObenseuerExtended v0.2.1 (6e6bac1)" with the release commit, the zip named
# ObenseuerExtended-0.2.1-6e6bac1_pre-release.zip. The pause menu and bug reports show the same "v0.2.1 (6e6bac1)".
#
# From 1.0.0 on the stable releases come from build/release.sh, only when the owner asks, and this publishes test
# builds of the coming version in between: numbered and tagged v1.1.0-pre.3+6e6bac1 (GitHub orders releases by the
# version in the tag and ignores what follows "+"), built and titled as "v1.1.0-pre.3 (6e6bac1)", without a commit.
#
# Checks that the tree is clean and that something is new since the last release of either kind, runs the tests and
# publishes. The notes are the player lines since the previous release and every commit since then.
#
# --dry-run        changes nothing: it reports what it finds and prints the notes, skipping the tests.
# --local          stops after the package: no tag, nothing pushed (a version commit is taken back).
# --minor/--patch  overrides the version bump the commits call for.
set -euo pipefail

repo=$(cd "$(dirname "$0")/.." && pwd)
cd "$repo"

die() {
    echo "experimental: $*" >&2
    exit 1
}

dry_run=false
local_only=false
bump_flag=()
notes_ref=refs/notes/changelog
for arg in "$@"; do
    case $arg in
        --dry-run) dry_run=true ;;
        --local) local_only=true ;;
        --minor | --patch) bump_flag=("$arg") ;;
        *) die "unknown option $arg (build/experimental.sh [--dry-run] [--local] [--minor | --patch])" ;;
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

# Releases published from another clone count too.
git fetch -q --tags origin 2> /dev/null || true
# So do changelog lines added to pushed commits as notes (docs/modding/changelog.md).
if git fetch -q origin "+$notes_ref:refs/notes/changelog-origin" 2> /dev/null; then
    git notes --ref=changelog merge -q -s cat_sort_uniq refs/notes/changelog-origin 2> /dev/null || true
fi
read -r next reason < <(build/next-version.sh "${bump_flag[@]}")
[[ $next =~ ^[0-9]+\.[0-9]+\.[0-9]+$ ]] || die "can't tell the next version (got '$next')"
if [ "${next%%.*}" -eq 0 ]; then
    numbered=true
    label=$next
    ! git rev-parse -q --verify "refs/tags/v$label" > /dev/null || problem "v$label already exists"
else
    numbered=false
    last_number=$(git tag --list "v$next-pre.*" | sed -nE "s/^v[0-9.]+-pre\.([0-9]+)(\+.*)?$/\1/p" | sort -n | tail -n 1)
    label="$next-pre.$((${last_number:-0} + 1))"
fi

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
start=$(git rev-parse HEAD)
count=$(git rev-list --count --no-merges "$since..$start")
[ "$count" -gt 0 ] || problem "nothing new $since_text"

dotnet build tools/ObenTool -c Release -p:ObenDeploy=false -v quiet -nologo > /dev/null
oben() { "$repo/tools/ObenTool/bin/Release/net10.0/oben" "$@"; }

# The release commit doesn't exist yet in a numbered release: its hash is filled in once it does.
notes=$(mktemp)
trap 'rm -f "$notes"' EXIT
write_notes() {
    local commit=$1
    {
        echo "A pre-release: the newest changes, each checked by itself but not tried together as a stable release yet. Back up your saves before playing with it. The pause menu shows \`ObenseuerExtended v$label ($commit)\` when this build is installed."
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
        git log --no-merges -n 300 --format='- `%h` %s' "$since..$start"
        [ "$count" -le 300 ] || echo "- ... and $((count - 300)) older ones"
        echo
        echo "</details>"
        echo
        echo "---"
        echo
        echo "**Installing:** download \`ObenseuerExtended-$label-${commit}_pre-release.zip\`, unpack it, close the game and run \`install.bat\` (Windows) or \`install.sh\` (Linux)."
    } > "$notes"
}

if $numbered; then
    write_notes "<commit>"
    echo "Pre-release $label ($reason) from ${branch:-a detached head}, $count commits $since_text"
else
    write_notes "$(git describe --always --abbrev=7 --exclude='*')"
    echo "Pre-release $label of $next ($reason) from ${branch:-a detached head}, $count commits $since_text"
fi
echo "----- notes -----"
cat "$notes"
echo "-----------------"

if $dry_run; then
    if [ "$problems" -gt 0 ]; then
        echo "Dry run: nothing was changed. $problems problem(s) above would stop a real run."
    elif $numbered; then
        echo "Dry run: nothing was changed. A real run commits version $label, tags v$label and publishes it."
    else
        echo "Dry run: nothing was changed. A real run tags v$label+<commit> and publishes it."
    fi
    exit 0
fi

$local_only || gh auth status > /dev/null 2>&1 || die "gh isn't logged in (gh auth login)"

echo "Running the tests..."
dotnet test ObenseuerExtended.slnx -c Release -p:ObenDeploy=false -v quiet -nologo

# From here until the push everything is local and taken back if a step fails.
tag=
rollback() {
    echo "experimental: failed, taking back the release" >&2
    [ -z "$tag" ] || git tag -d "$tag" > /dev/null 2>&1 || true
    git reset -q --hard "$start"
}
trap 'rollback' ERR

if $numbered; then
    # The changelog hooks would write the file again after the commit; the release writes it itself.
    export OBEN_CHANGELOG_HOOKS=0
    sed -i "s|<Version>[^<]*</Version>|<Version>$label</Version>|" Directory.Build.props
    # The first numbered release is the version the file already has: then the tag goes on this commit.
    if ! git diff --quiet Directory.Build.props; then
        git add Directory.Build.props
        git commit -q -m "chore(release): $label" -m "Sets the version to $label."
    fi
    build/changelog.sh --release "$label" --quiet
else
    # The kit's build copies changelog.json into the package, so it should list everything up to this commit.
    build/changelog.sh --quiet
fi
# The same abbreviation build/Revision.targets stamps into the assemblies, without "-dirty".
commit=$(git describe --always --abbrev=7 --exclude='*')
write_notes "$commit"
name="ObenseuerExtended-$label-${commit}_pre-release"
build/package.sh "$label" "$name"
zip="dist/$name.zip"
[ -f "$zip" ] || false

if $local_only; then
    trap - ERR
    if $numbered; then
        git reset -q --hard "$start"
        echo "Packaged $zip. The version commit was taken back; nothing was tagged or pushed."
    else
        echo "Packaged $zip. Nothing was tagged or pushed."
    fi
    exit 0
fi

if $numbered; then
    tag="v$label"
else
    tag="v$label+$commit"
fi
# Verbatim, or git drops the notes' "## What's new" heading as a comment.
git tag -a --cleanup=verbatim "$tag" -m "ObenseuerExtended $label ($commit), pre-release" -m "$(cat "$notes")"
# All or nothing: a failed push leaves nothing half published, and the release is taken back.
push_notes=()
git rev-parse -q --verify "$notes_ref" > /dev/null && push_notes=("$notes_ref")
git push --atomic origin "$branch" "$tag" "${push_notes[@]}" || false
trap - ERR

title="ObenseuerExtended v$label ($commit)"
if ! gh release create "$tag" "$zip" --prerelease --title "$title" --notes-file "$notes"; then
    die "the tag is pushed but the release wasn't made; run: gh release create '$tag' '$zip' --prerelease --title '$title' --notes-file <notes>"
fi
echo "Published the pre-release $label ($tag)."
