#!/bin/sh
# Installs ObenseuerExtended into Obenseuer.
# Run it from the unpacked folder. Extra arguments go to the installer, e.g. --game "/path/to/Obenseuer".
set -e
here=$(cd "$(dirname "$0")" && pwd)
oben="$here/tool/linux-x64/oben"
chmod +x "$oben" 2>/dev/null || true

echo "ObenseuerExtended installer"
echo
echo "This adds the mod loader and the mods to Obenseuer. Close the game first."
echo "It looks for the game in Heroic, in your Steam libraries and in OBENSEUER_DIR."
echo "Installing again updates the mods and keeps your settings."
echo

"$oben" install --mods "$here/Mods" "$@"

echo
echo "Done. Start Obenseuer as usual; the mods show up in the MODS menu."
echo "To take everything out again, run uninstall.sh."
