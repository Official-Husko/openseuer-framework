#!/usr/bin/env bash
# Builds the player package: dist/ObenseuerExtended-<version>.zip with the mods, the oben tool for Linux
# and Windows (self-contained, no .NET install needed) and the install scripts from build/package.
#
#   build/package.sh [version] [name]   the version defaults to <Version> in Directory.Build.props, the name
#                                       (folder and zip) to ObenseuerExtended-<version>
#
# Mods whose mod.json says "devOnly": true (the dev bridge, the script runner) stay out: they open a
# local port or run code sent to the game, and are only for people making mods. Never deploys into the game.
set -euo pipefail

repo=$(cd "$(dirname "$0")/.." && pwd)
version=${1:-$(sed -n 's:.*<Version>\(.*\)</Version>.*:\1:p' "$repo/Directory.Build.props" | head -n 1)}
[[ $version =~ ^[0-9]+\.[0-9]+\.[0-9]+([-+][0-9A-Za-z.-]+)?$ ]] || { echo "package: '$version' isn't a version" >&2; exit 1; }
name=${2:-ObenseuerExtended-$version}
[[ $name =~ ^[0-9A-Za-z._-]+$ ]] || { echo "package: '$name' isn't a plain file name" >&2; exit 1; }
out="$repo/dist/$name"

# Every folder with a mod.json that isn't marked devOnly, and the project to build for it.
projects=()
ids=()
for dir in "$repo"/src/*/ "$repo"/mods/*/; do
    dir=${dir%/}
    [ -f "$dir/mod.json" ] || continue
    if grep -Eq '"devOnly"[[:space:]]*:[[:space:]]*true' "$dir/mod.json"; then
        echo "  skipping $(basename "$dir") (devOnly)"
        continue
    fi
    projects+=("$dir/$(basename "$dir").csproj")
    ids+=("$(sed -n 's:.*"id"[[:space:]]*\:[[:space:]]*"\([^"]*\)".*:\1:p' "$dir/mod.json" | head -n 1)")
done
[ "${#projects[@]}" -gt 0 ] || { echo "package: no mods found" >&2; exit 1; }

rm -rf "$out" "$repo/dist/$name.zip"
mkdir -p "$out/Mods" "$out/tool"

echo "Building $version..."
for project in "${projects[@]}"; do
    dotnet build "$project" -c Release -p:ObenDeploy=false -p:Version="$version" -v quiet -nologo
done

for i in "${!projects[@]}"; do
    id=${ids[$i]}
    mkdir -p "$out/Mods/$id"
    (cd "$(dirname "${projects[$i]}")/bin/Release" && find . -type f ! -name '*.deps.json' -exec cp --parents {} "$out/Mods/$id/" \;)
    echo "  $id"
done

for rid in linux-x64 win-x64; do
    echo "Publishing oben for $rid..."
    dotnet publish "$repo/tools/ObenTool" -c Release -r "$rid" --self-contained true \
        -p:PublishSingleFile=true -p:EnableCompressionInSingleFile=true -p:ObenDeploy=false \
        -p:Version="$version" -o "$out/tool/$rid" -v quiet -nologo
    rm -f "$out/tool/$rid/"*.pdb
done

# oben install copies these next to itself into the game; without them it can't install anything.
for rid in linux-x64 win-x64; do
    for file in ObenLoader.dll 0Harmony.dll Mono.Cecil.dll; do
        [ -f "$out/tool/$rid/payload/$file" ] || { echo "package: tool/$rid/payload/$file is missing" >&2; exit 1; }
    done
done

cp "$repo/build/package/"{install,uninstall}.{sh,bat} "$repo/build/package/README.txt" "$repo/LICENSE" "$out/"
mkdir -p "$out/game-files/hashes"
cp "$repo/tools/game-files/"*.{sh,bat,ps1} "$out/game-files/"
cp "$repo/tools/game-files/hashes/"*.json "$out/game-files/hashes/"
for optional in THIRD-PARTY-NOTICES.md changelog.json; do
    [ ! -f "$repo/$optional" ] || cp "$repo/$optional" "$out/"
done
# Windows wants CRLF in batch files and Notepad in text files, whatever the checkout used.
sed -i 's/\r$//; s/$/\r/' "$out/"*.bat "$out/game-files/"*.bat "$out/README.txt"
chmod +x "$out/"*.sh "$out/game-files/"*.sh "$out/tool/linux-x64/oben"

(cd "$repo/dist" && zip -qr "$name.zip" "$name")
echo "Done: dist/$name.zip ($(du -h "$repo/dist/$name.zip" | cut -f1))"
