#!/bin/sh
# Takes ObenseuerExtended out of Obenseuer again: the loader and the changes it made to Unity's startup files.
# The Mods folder (and your mod settings) stays unless you pass --remove-mods.
set -e
here=$(cd "$(dirname "$0")" && pwd)
oben="$here/tool/linux-x64/oben"
chmod +x "$oben" 2>/dev/null || true
"$oben" uninstall "$@"
