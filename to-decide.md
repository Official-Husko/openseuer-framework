# To decide

Open questions that need the owner's call. Each item says what is undecided and what the options cost. Once decided, move it to TODO.md (or drop it) and take it out here.

## Free-text settings

Cheat Menu's Key and ObenCoop's lobby type are plain text fields. They could become a key picker and a choice list, but a typo in the file would then reset the whole config, so they were left alone.

- 2026-10-29: config files now read field by field. A value that doesn't fit (a misspelt key or choice) keeps only that field's default, with a warning in the mod's log, so the typo no longer resets the whole file. What is left to decide:
  - Cheat Menu's `Key` would become a `KeyCode`. The file keeps names like `"Insert"`, and the Settings page shows a key picker instead of a text field.
  - ObenCoop's `SteamLobbyType` would become an enum (`Friends`, `Public`, `Private`), shown as a choice. The file then says `"Friends"` instead of `"friends"`, and the `invite` spelling for private stops working. ObenCoop doesn't reference the UI kit, so a `[Setting(Choices = ...)]` string isn't an option there without adding that dependency.
