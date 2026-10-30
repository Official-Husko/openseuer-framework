#!/usr/bin/env bash
# Works out the version of the next release from the commits since the last one (docs/modding/releasing.md):
#
#   Before 1.0.0 the major version stays 0. A release with a new feature (a feat commit) or a breaking change
#   (type!: or a "BREAKING CHANGE" line) bumps the minor version, 0.2.0 -> 0.3.0; one with only fixes and other
#   work bumps the patch version, 0.2.0 -> 0.2.1. 1.0.0 is the first stable release, made with build/release.sh
#   when everything in PROGRESS.md is at 100% and the owner says so.
#   From 1.0.0 on: a breaking change bumps the major version, a feature the minor version, the rest the patch.
#
# The first numbered release is <Version> in Directory.Build.props (0.1.0: the pre-releases before it were tagged
# v0.1.0-pre.1 to pre.5).
#
#   build/next-version.sh [--minor | --patch]
#
# Prints the version and why, for example "0.2.0 new features". --minor or --patch overrides what the commits say.
set -euo pipefail

repo=$(cd "$(dirname "$0")/.." && pwd)
cd "$repo"

force=
for arg in "$@"; do
    case $arg in
        --minor) force=minor ;;
        --patch) force=patch ;;
        *) echo "next-version: unknown option $arg (build/next-version.sh [--minor | --patch])" >&2; exit 1 ;;
    esac
done

last=$(git tag --list 'v*' --sort=-v:refname | grep -E '^v[0-9]+\.[0-9]+\.[0-9]+$' | head -n 1 || true)
if [ -z "$last" ]; then
    first=$(sed -n 's:.*<Version>\(.*\)</Version>.*:\1:p' Directory.Build.props | head -n 1)
    [[ $first =~ ^[0-9]+\.[0-9]+\.[0-9]+$ ]] || { echo "next-version: no <Version> in Directory.Build.props" >&2; exit 1; }
    echo "$first first numbered release"
    exit 0
fi

IFS=. read -r major minor patch <<< "${last#v}"
subjects=$(git log --no-merges --format=%s "$last..HEAD")
bodies=$(git log --no-merges --format=%b "$last..HEAD")

if [ -n "$force" ]; then
    bump=$force
    reason="asked for a $force version"
elif grep -qE '^[a-z]+(\([^)]*\))?!:' <<< "$subjects" || grep -q '^BREAKING CHANGE' <<< "$bodies"; then
    bump=major
    reason="a breaking change"
elif grep -qE '^feat(\([^)]*\))?:' <<< "$subjects"; then
    bump=minor
    reason="new features"
else
    bump=patch
    reason="fixes and other work only"
fi
# Before 1.0.0 a breaking change only bumps the minor version.
[ "$bump" = major ] && [ "$major" -eq 0 ] && bump=minor

case $bump in
    major) echo "$((major + 1)).0.0 $reason" ;;
    minor) echo "$major.$((minor + 1)).0 $reason" ;;
    patch) echo "$major.$minor.$((patch + 1)) $reason" ;;
esac
