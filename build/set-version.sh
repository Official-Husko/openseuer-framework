#!/usr/bin/env bash
# Sets the version of the pack, <Version> in Directory.Build.props (every assembly and the tool). The release
# scripts call this; never set it by hand. Each mod's own version in its mod.json is separate
# (build/mod-versions.sh); --manifest writes one into the given mod.json files.
#
#   build/set-version.sh <version>
#   build/set-version.sh --manifest <version> <mod.json>...
set -euo pipefail

repo=$(cd "$(dirname "$0")/.." && pwd)

stamp() {
    local version=$1
    shift
    for manifest in "$@"; do
        [ -f "$manifest" ] || continue
        # The first "version" key only: "gameVersion" and the dependency entries are left alone.
        sed -i -E "0,/\"version\"[[:space:]]*:/ s|(\"version\"[[:space:]]*:[[:space:]]*\")[^\"]*\"|\1$version\"|" "$manifest"
    done
}

if [ "${1:-}" = "--manifest" ]; then
    version=${2:?usage: build/set-version.sh --manifest <version> <mod.json>...}
    shift 2
    stamp "$version" "$@"
    exit 0
fi

version=${1:?usage: build/set-version.sh <version>}
[[ $version =~ ^[0-9]+\.[0-9]+\.[0-9]+([-+][0-9A-Za-z.-]+)?$ ]] || { echo "set-version: '$version' isn't a version" >&2; exit 1; }
sed -i "s|<Version>[^<]*</Version>|<Version>$version</Version>|" "$repo/Directory.Build.props"
