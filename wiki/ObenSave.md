Obenseuer writes its saves as plain text, several megabytes each, most of it the same field names over and over. ObenSave compresses them.

It's **off until you switch it on** ("Compressed save files" under ObenSave on the MODS **Mods** page).

## How it works

- Your save files keep their names; only what's inside them changes.
- With the mod on, the game reads both kinds of file, compressed and plain, in any mix.
- Switching it off converts every save back to the game's own format, exactly as the game wrote it.

## Before you remove it

The game can't read a compressed save on its own. **Switch ObenSave off before you delete it or uninstall**, so your saves are converted back first.

If you already removed it, the tool in the zip converts them back with the game closed:

```sh
./tool/linux-x64/oben save convert --all --to plain
tool\win-x64\oben.exe save convert --all --to plain
```

Nothing is ever lost: the conversion needs nothing from the game, and a compressed file starts with the readable word `OBENSAVE`, so it's easy to tell what it is.

The format is documented in [save-format.md](https://github.com/Official-Husko/openseuer-framework/blob/main/docs/modding/save-format.md).
