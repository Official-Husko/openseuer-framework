#!/usr/bin/env bash
# Publishes wiki/ to the GitHub wiki (the repository's .wiki.git). The pages are written here, next to the code they
# describe, and this replaces the wiki's pages with them: a page removed from wiki/ is removed from the wiki too.
#
#   build/wiki.sh [--dry-run]
#
# --dry-run shows what would change and pushes nothing. GitHub only creates a wiki's repository once its first page
# was saved on the website; after that this script does everything.
set -euo pipefail

repo=$(cd "$(dirname "$0")/.." && pwd)
dry_run=false
for arg in "$@"; do
    case $arg in
        --dry-run) dry_run=true ;;
        *) echo "wiki: unknown option $arg (build/wiki.sh [--dry-run])" >&2; exit 1 ;;
    esac
done

origin=$(git -C "$repo" remote get-url origin)
wiki="${origin%.git}.wiki.git"
work=$(mktemp -d)
trap 'rm -rf "$work"' EXIT

git clone -q "$wiki" "$work" || { echo "wiki: can't clone $wiki; save a first page on GitHub to create it" >&2; exit 1; }
find "$work" -maxdepth 1 -type f -name '*.md' -delete
cp "$repo"/wiki/*.md "$work"/

cd "$work"
git add -A
if git diff --cached --quiet; then
    echo "The wiki is up to date."
    exit 0
fi
git diff --cached --stat
if $dry_run; then
    echo "Dry run: nothing was pushed."
    exit 0
fi
commit=$(git -C "$repo" describe --always --dirty --abbrev=7 --exclude='*')
git commit -q -m "Update from $commit"
git push -q
echo "Published the wiki from $commit."
