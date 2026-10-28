# Save format and the ObenSave mod

Obenseuer writes every save file as minified JSON, with no compression. A save folder with a handful of levels is several megabytes of text, most of it repeated field names. ObenSave stores the same text in a small container, compressed with Deflate, and reads both its own files and the game's.

This page is the format specification and the rules for how the mod and the `oben` tool treat save files. The layout of a save itself (folders, what is in each file, how the game reads them) is in [docs/game/save-system.md](../game/save-system.md). Everything about the game here comes from the decompiled code; the in-game behaviour listed under "Checking it in the game" has not been run yet.

Short version:

- Files keep their names (`Info.tnmt`, `Globals.tnmt`, `<Level>.tnmt`, `Globals.dialogue`). Only the bytes inside change.
- The text inside is exactly what the game wrote, so converting back gives the original file byte for byte.
- With the mod loaded, both kinds of file load, in any mix, in any folder.
- Without the mod the game cannot read a container. Switch the feature off first (that converts the saves back), or convert with `oben save convert`. The data is never lost, because conversion needs nothing from the game.

The code is in `mods/ObenSave/Format/` (the container) and `mods/ObenSave/Files/` (reading, writing and converting files). Neither uses Unity types. The `oben` tool and the unit tests (`tests/ObenSave.Tests`) compile the same source files, so all three agree on the format.

## Files and names

The game decides which files exist with `File.Exists` on hard-coded names: `Info.tnmt` marks a slot as used in the load menu, `Globals.tnmt` is required to load, `<Level>.tnmt` is looked up by scene name. Those checks do not go through any method a mod can replace cheaply, so a save file has to keep its name. That is the main reason the container lives inside the `.tnmt` files instead of next to them under a new extension.

| File | Content inside the container |
| --- | --- |
| `Info.tnmt`, `Globals.tnmt`, `<Level>.tnmt` | FullSerializer JSON of a `SaveDataHeader`, minified. |
| `Globals.dialogue` | The Dialogue System state, a Lua chunk. |

Other files in a save folder (the map `.png`) are not touched. Nothing else in `persistentDataPath` is touched either: the settings files (`Display.json`, ...) stay as they are.

## Container layout

All integers are little-endian.

| Offset | Size | Field | Meaning |
| ------ | ---- | ----- | ------- |
| 0 | 8 | magic | ASCII `OBENSAVE` |
| 8 | 1 | version | Format version. Currently `1`. |
| 9 | 1 | codec | `0` = stored (the text as it is), `1` = raw Deflate (RFC 1951) of the text. |
| 10 | 1 | flags | Bit 0: the original file started with a UTF-8 byte order mark. Other bits are `0`. |
| 11 | 1 | reserved | `0`. |
| 12 | 4 | length | Length of the text in bytes, uncompressed, not counting the byte order mark. |
| 16 | 4 | CRC-32 | IEEE 802.3 CRC-32 (the zip and PNG polynomial) of those text bytes. |
| 20 | n | payload | The text, or its Deflate stream. Runs to the end of the file. |

The header is 20 bytes. Codec `1` is a raw Deflate stream with no zlib or gzip wrapper, produced by .NET's `DeflateStream` at `CompressionLevel.Optimal`. The writer uses codec `0` when Deflate would not make the file smaller (tiny `Info.tnmt` files, for example), otherwise codec `1`.

### Text and the byte order mark

The game writes UTF-8 with a byte order mark (`EF BB BF`) and reads with `File.ReadAllText(path, UTF8)`, which drops it. The container stores the text without the mark and remembers in flag bit 0 whether the file had one. Restoring a file writes the mark back when the bit is set, which is what makes the round trip exact. A vanilla file without a mark is stored with the bit clear.

The writer refuses to wrap a file that starts with a UTF-16 mark (`FF FE`, `FE FF`) and leaves it alone. The game never writes those.

### Checks when reading

A file is a container when its first 8 bytes are the magic. The reader then:

1. Rejects a header shorter than 20 bytes.
2. Rejects a version above 1 (`Save format N is newer than this version understands (1).`) and an unknown codec.
3. Rejects a length above 1 GiB.
4. Decodes the payload. Deflate output must be exactly `length` bytes with nothing left over, and the CRC-32 must match. Any mismatch fails with `The save file is damaged (length or checksum mismatch).`

The length is checked while inflating, so a damaged length field cannot make a small file expand into gigabytes.

## How vanilla files are detected

Anything that does not start with `OBENSAVE` is a vanilla file. A real `.tnmt` starts with the byte order mark and `{`; the magic can never be confused with it. Vanilla files are read the way the game reads them: UTF-8 text, byte order mark dropped.

There is no per-folder or per-save setting. Every file is judged on its own, so a folder can hold both kinds: for example, a slot written by the game, followed by one level saved with the mod on. The game's folder handling keeps working with that, because it copies and deletes whole files:

- Saving into another slot copies the other levels' files from the folder that was loaded (`CopyDirectory`), as raw bytes, whatever their format.
- Deleting a slot deletes the folder.
- The autosave rotation reads the `Date` inside `Info.tnmt`, not file times.

## Reading in the game

Every save file the game reads goes through one public method, `SaveController.ReadSaveFile(string path)` (`SaveController.cs:701`): `GetSavedGameInfo` and `GetCharacters` for `Info.tnmt`, and `LoadGameWithMigration` for `Globals.tnmt`, the level file and `Globals.dialogue`. It is a prefix on that method:

1. Open the file and read its first 8 bytes. If they are not the magic, return and let the game's own code read the file. Vanilla files cost one 8-byte read and nothing else.
2. Otherwise read the whole file, decode it and return the text as the method's result, skipping the game's code.
3. If decoding throws (damaged file, newer version), log the error with the file name and let the game's code run. The game then fails on the file the way it fails on any corrupted save, which its menu already handles ("Corrupted").

The prefix is always applied while the mod is loaded, whether or not the writing feature is on. That is what keeps old and new files loadable after the feature is switched off.

## Writing in the game

The writing hook belongs to the feature `ObenSave/CompressedSaves`. It is a postfix on `SaveController.SaveGame`. The game writes the files as it always does, and when `SaveGame` returns, the postfix goes through the folder that was just saved (`SaveController.instance.GetCurrentSavePath()`) and converts every plain `*.tnmt` and `Globals.dialogue` in it:

1. Read the file, build the container.
2. Decode the container again and compare with the original bytes. Anything but an exact match skips the file.
3. Write the container to `<name>.tmp` in the same folder, then replace the original with it (`File.Replace`), and put the original modification time back.

If anything fails, the plain file stays where it is and the error is logged, so a save is never lost or half written: at every moment the file is either the game's complete file or the complete container. The postfix runs after `SavingDone`, so mods that read the files in a `Saved` handler still see plain files; they should use `SaveController.ReadSaveFile` anyway, which works for both.

Converting after the game wrote the file (instead of replacing the game's write) is deliberate. The game's writer is a private method with a private argument type and has a second code path, and a small public overload that the JIT can inline; the dialogue file goes through a different call again. Hooking `SaveGame` once covers all of them and also compresses the other levels' files that were copied from an older, plain slot.

The cost is that the game writes the plain text first and the mod reads it back, so a save does about twice the disk traffic and a moment of CPU for Deflate. Typical saves are small enough for that not to matter; if it turns out to be noticeable, the write can be moved in front of the game's own.

## Switching it off, and removing the mod

The decision, with the alternatives:

- **Keep a plain copy next to every container.** Vanilla could read the copy. But the files keep their size, which defeats the point, and two copies can disagree.
- **Put the container in a new file and leave a stub under the `.tnmt` name.** A stub is a file vanilla can read, so vanilla would load it as an empty or reset save and the next save would overwrite the real one. Failing loudly is better than that.
- **Write containers under the `.tnmt` names and make converting back easy.** This is what ObenSave does. Without the mod the game reads the container as text, the deserializer fails, and the load menu shows the slot as "Corrupted" (or a load fails with "Save data corrupted"). Nothing is changed on disk by that, and the game only overwrites a slot when the player saves into it. The data is intact and convertible.

To make switching off and uninstalling safe:

1. **Switching the feature off** (`features ObenSave/CompressedSaves off` in the console, the settings screen, or the bridge) converts every compressed file under `Saves/` back to the game's own format before the feature goes quiet. After that the saves on disk are what the game would have written. Reading still works for any container that turns up later, as long as the mod is loaded.
2. **The console command `obensave restore`** does the same conversion on demand, and `obensave compress` converts the other way. `obensave status` counts files of each kind.
3. **`oben save convert --all --to plain`** does it with the game closed, and works without the mod. It is the way back after the mod was removed first. `oben save export` writes a plain copy of one slot into any folder.
4. A hot reload or unloading the mod does **not** convert anything; the files then stay compressed until the mod is loaded again, so the order for uninstalling is: switch the feature off, wait for the message in the log, then remove the mod.

What cannot be handled automatically is deleting the mod folder while saves are compressed. That is why the file starts with the readable word `OBENSAVE`: someone looking at the file, or a bug report, can tell what it is. The fix is always `oben save convert --all --to plain` (or putting the mod back).

## The oben tool

The tool uses the data folder of the game it finds (the same one `oben status` prints as `Data folder`) and its `Saves` folder. A `<slot>` is a path to a save folder, or `Character/Slot` relative to `Saves` (for example `Esko_Virtanen/Slot3`, `Esko_Virtanen/Autosave`), or just `Slot3` when there is only one character.

```sh
oben save list                           # characters and slots, format and size of each
oben save info <slot>                    # every file: format, sizes, CRC-32, header fields, object counts
oben save export <slot> <dir> [--format plain|compressed] [--pretty]
oben save import <dir> <slot> [--format compressed|plain] [--force]
oben save convert <slot> --to plain|compressed
oben save convert --all --to plain|compressed
```

- `export` writes the slot's files into `<dir>`, in plain format by default (the files the game itself would have written, so they open in any text editor). `--pretty` indents the JSON for reading; the game reads it just as well, but it is no longer byte for byte the original. Other files in the folder (the map image) are copied.
- `import` is the reverse: it takes a folder of save files (either format) and writes them into a slot, compressed by default. It refuses to overwrite an existing slot without `--force`, and refuses while the game is running.
- `convert` changes files in place, with the same temp-file-and-replace and verification as the mod. It also refuses while the game is running, unless `--force`.

Put options after the arguments (`--pretty` and the other switches take the next word as their value). `--saves <folder>` names a Saves folder directly instead of looking for the game's, which is handy for trying the commands on a copy; it also skips the "game is running" check, since the files are then not the game's. `oben save help` prints the list above.

All of these work with or without the mod installed. The tool compiles the container source from `mods/ObenSave/Format` and `mods/ObenSave/Files` (see `tools/ObenTool/ObenTool.csproj`), and its own code is in `tools/ObenTool/Saves/` and `tools/ObenTool/Commands/SaveCommand.cs`.

## Checking it in the game

The mod is off by default until this has been played through:

1. Without the mod, start a new game, save to a slot and make an autosave (go through a door). Note the sizes with `oben save list`.
2. Switch the ObenSave mod on. Load that slot: it must load, and the log must not show errors.
3. With the feature on, save into another slot and go through a door. `oben save list` shows the new files as compressed and smaller; the old slot is still plain.
4. Load the compressed slot from the menu, then the plain one. Both must load. The load menu must show the date and level for both.
5. Switch the feature off. `oben save list` must show every file plain, the log says how many were converted, and the slots still load.
6. Switch it on again and save once more, then unload the mod in the mod manager and try to load: the compressed slot shows as corrupted. `oben save convert --all --to plain` brings it back.
