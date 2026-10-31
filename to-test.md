# To test

Everything below is built, reviewed and covered by unit tests, but has not been run in the game yet (or only partly). Work through it the same way as the earlier in-game passes: drive the game over the bridge, look at screenshots for anything visual, and check the game's state after every step instead of trusting a command's return value.

Tick items off here as they pass. When something fails, fix it in its own commit (with the in-game result in the body) and note the fix next to the item. Once a whole area passes, update its row in `PROGRESS.md` (the "Missing" column lists the same open checks).

## How to run a pass

```sh
OBEN="dotnet tools/ObenTool/bin/Release/net10.0/oben.dll"
dotnet build ObenseuerExtended.slnx -c Release      # deploys everything
$OBEN launch --wait 150                              # fresh start, see below
$OBEN call console.exec 'command=new_scene Interior Player Tenement'   # a level without a character
$OBEN call screenshot scale=0.5 format=jpg           # base64 jpg, decode and look at it
```

- **Start fresh.** The loader changed in the last round (mod id and game version checks, log-once keys). Don't hot reload the new framework onto an old loader; launch the game anew.
- **Two kinds of world.** `new_scene <name>` loads a level without a character or save; many checks work there. Anything about saving needs a **real save**: start a new game (or load one), save from the pause menu or by sleeping, and load it again. Those items are marked **[save]**.
- **Two copies** for co-op: `oben install --multi-instance` once, then `oben launch --wait 150` and `oben launch --instance 2 --wait 150`; add `--instance 2` to bridge calls for the second copy. Marked **[2 copies]**.
- **After every area:**
  - `oben call patches.check` must show `broken: []`;
  - `grep -c NullReferenceException` on Player.log must not have grown;
  - `oben logs | grep -E "\[Error\]|\[Warning\] \[(ObenFramework|DevBridge)"` should show nothing new that isn't expected.
- **Events:** most `*.events` bridge commands start recording on their first call; call them once before the action, then again after it. For events without a recorder, subscribe from a script with `oben run`, as in `scripts/examples/`.
- **Clean up** test objects (props, furniture, mod NPCs, quests, flags, multipliers) when an area is done.
- **Watch for the facing convention:** `player.teleport ... yaw=` turns the view to that world yaw, and positive `pitch` looks down. Both were fixed this round; a wrong direction in a screenshot is worth a second look before blaming the feature.

## 0. Start-up and stability

- [x] `oben logs` shows `[ObenFramework] Ready: 34 parts.` with nothing listed as not working. (2026-10-29: 34 parts, game check passed: 728 hooks of 9 mods.)
- [x] `oben call framework.modules failed=true` gives `notWorking: 0`. (2026-10-29.)
- [x] `oben call patches.areas mod=ObenFramework` lists every area as available, including `Shops` (8), `Clock` (1) and `ItemPlaceholders` (2). (2026-10-29: 25 areas, all on.)
- [x] `oben call mods.list` shows no `warning` on any mod. (2026-10-29.)
- [x] `oben call patches.check`: 0 broken. (2026-10-29: 473 patched methods with 708 handlers at the main menu; the 730 written here before was a guess.)
- [x] **Hot reload stress.** In a level, run 12 cascade reloads, each followed by a forced full GC: (2026-10-29: 24 cascades in Interior Player Tenement, `indirect: 0` and `broken: []` in every round the bridge answered and at the end, no NullReferenceException in Player.log. A cascade of all 9 mods took 5.2 s at first and 15.2 s by the 24th. Again after the unpatching fix: 24 rounds clean, 2.5 s at first and 5.0 s by the 24th, see TODO.md.)
  - repeat: `touch Mods/ObenFramework/ObenFramework.dll`, wait until `latest.log` has the next "Reloaded ObenFramework, ..." line (a fixed 7 s wait is too short now that every mod reloads with the framework), `oben eval 'System.GC.Collect(); System.GC.WaitForPendingFinalizers(); return 1;'`, then `patches.check`;
  - every round must show `indirect: 0` and `broken: 0`;
  - the game must still respond at the end.
  - This is the crash fixed by the loader's InlineJumps; re-run it whenever Harmony or the loader changes.
- [ ] Play normally for 15 minutes in a real save (walk, open doors, use storage, eat, sleep, change level twice) with every mod on. Player.log must have no new exceptions from our code.

## 1. Loader and core (M01, M02, M03, M10, M18)

- [x] **Patch areas:** (2026-10-29: all as expected; the error was in `ErrorReport`, which the Errors page lists, and the reload cleared the area.)
  - `oben run scripts/examples/patch-areas.cs` returns `first patch True, area on True; missing method patch False, area on False`.
  - `oben call console.exec command=help` has no "Patched through a patch area." line.
  - `oben call patches.areas mod=ScriptRunner` shows `demo` off, and the error is on the MODS Errors page.
  - Clean up with `oben call mods.reload id=ScriptRunner`.
- [ ] **Game version check:** give a test mod `"gameVersion": "0.4.x"`. It loads with a warning in the log, on the Errors page and in `mods.list`. With `"strictGameVersion": true` it doesn't load.
- [ ] **Bad mod id:** a test mod with id `con` or `my mod`. The loader refuses it with a message saying what to change.
- [x] **Steam build:** here (sideloaded) `oben call game.info` has `gameBuildId: null` and the first line of `latest.log` names only the version. On a Steam install it shows the build, and a test mod with `"gameBuilds": ["1"]` loads with a "Made for Steam build 1" warning. (2026-10-29: this copy has a depot download's app manifest in its folder, so it reports build 26179069; the loader now finds kept manifests too.)
- [x] **RefuseOtherGameVersions:** with `"RefuseOtherGameVersions": true` in `ObenLoader/settings.json`, the test mod with `"gameVersion": "0.4.x"` (no `strictGameVersion`) doesn't load, and the log line names the setting. Take it out again afterwards. (2026-10-29: StrictTest with 0.4.x refused with the setting named.)
- [x] **mod.json checks:** a test mod needing `"ObenFramework": "latest"` is refused with a message about the version; one with `"version": "first"` loads with a warning. (2026-10-29: refused with the version message; the refusal is logged twice, see TODO review follow-ups.)
- [x] **Mod declared in code:** a folder with only a DLL whose `ObenMod` class has `[ObenModInfo("CodeTest", "Code Test", "1.0.0")]` and `[ObenModDependency("ObenFramework")]` shows in `mods.list` as Code Test 1.0.0 and loads after the framework. (2026-10-29: CodeTest with AnyGameVersion loaded after the framework, even with RefuseOtherGameVersions on.)
- [x] **Loose DLL:** copy a test mod's DLL straight into `Mods/` (no folder). It loads, `mods.list` shows `loose: true`, the log warns to give it a folder, and its config lands in `Mods/<id>/`. A copy of `0Harmony.dll` loose in `Mods/` is skipped with a log line, and a loose copy of a mod that has its folder is refused as a duplicate. (2026-10-29: LooseTest.dll loaded with the warning and its folder at Mods/LooseTest; a loose 0Harmony.dll was skipped. The duplicate case wasn't run.)
- [x] **anyGameVersion:** a test mod with `"anyGameVersion": true` (or `"gameVersion": "*"`) loads without an Errors page entry, even with `RefuseOtherGameVersions` on, and the log has its "isn't checked" line. (2026-10-29: through the attribute; loaded under RefuseOtherGameVersions with the log note.)
- [ ] **Handle shortcuts:** from a script, `this.SaveData<...>()`, `this.Postfix(...)` and `this.Hotkey(...)` behave like the static calls (the hotkey shows on the Hotkeys page).
- [x] **Log rate limit:** a script logging 1000 lines in one frame. About 100 lines appear, then one "dropped" note. Errors still count in `errors.log`. (2026-10-29: 80 lines after 20 errors, 920 dropped with the note.)
- [x] **Repeated errors and mod log files:** a script logging the same error 50 times and 30 different ones in one frame writes the first once and 19 others (20 in 10 s), with "60 errors were held back" after 10 s; `errors.log` counts all 50 of the first. `ObenLoader/logs/mods/<logger>.log` has the lines with frame numbers, `log.stats` the counters, and the developer menu's session page a Logs line. With `LogHub.MinFileLevel` set to Debug (as `VerboseLog` does) all 80 errors are written. Quitting right after a flood still writes both notes. While a trace runs, a logged warning shows as a `log Warning [...]` mark. (2026-10-29.)
- [x] **Time freeze:** (2026-10-30: the clock stayed at 09:59 for over 40 s, and through a level change.) `oben call time.freeze`, then `clock.get` twice 10 s apart gives the same time. `time.freeze on=false` lets the clock run again.
- [x] **Time speed:** (2026-10-30: 28.7 in-game minutes in 10 s at speed 10, 2.9 at normal speed; both include the bridge round trips.) `oben call time.speed speed=10` gives about 25 in-game minutes per 10 real seconds. `time.speed clear=true` puts it back.
- [ ] **Freeze and sleep [save]:** freeze the clock, then sleep in a bed. Time passes while sleeping (`time.control` shows `suspended: true`) and is frozen again afterwards.
- [x] **`clock.after`:** (2026-10-30: "2 in-game minutes passed since 10:36, now 10:38".) `oben call clock.after minutes=2` logs "clock.after: 2 in-game minutes passed" about 8 s later.
- [ ] **`clock.after` while sleeping [save]:** note `clock.get`, run `clock.after minutes=60`, then sleep. The log line comes about one in-game hour after the noted time, not two or three (fixed: it counted minute ticks, and a sleeping frame covers several minutes).
- [x] **Load aborted:** (2026-10-30, in a second copy on a test character: logged "didn't happen; still in Open Sewer Tenement (ready: True)", IsReady true.)
  - `oben eval 'SaveController.ChangeLevel("", "")'`. A few seconds later the log says "The level change the game announced didn't happen ... (ready: True)".
  - `oben eval "ObenFramework.Game.WorldState.IsReady"` is true.
- [x] **Overrides and countdowns across loads:** with `time.freeze` and `clock.after minutes=600` held, a level change keeps both; loading a save clears both with "A new session (SaveLoad): clock overrides cleared". (2026-10-30.)
- [x] **No hour change from a load:** loading a save whose time is in another hour raises no `GameClock.HourChanged`; the first minute after it only records the hour. (2026-10-30: clock set to 13:30, then a save at 09:53 loaded: no `HourChanged`, and the clock ran on to 09:59. Before the fix it raised `HourChanged 9`.)
- [x] **WorldReady after a save load:** load a save and watch `GameEvents.Loading`, `Loaded` and `WorldReady` from a script. `WorldReady` comes once, after `Loaded`, with the save's time on the clock, and `Sessions.Started` says `SaveLoad`. (2026-10-30: before the fix it also fired about 0.1 s before `Loading`, with the level's default time, and the real one then reported `Direct`.)
- [x] **Missing items, quick check:** `oben eval "new ItemReference(1234567).ID"` gives `1234567`; it gave `0` before. `oben call registry.placeholders` lists it under `active`. (2026-10-30.)
- [x] **Missing items [save], full check:** (2026-10-30, on a copy of a real save: both stacks showed as the missing item and the save's file listed it; a shop that takes everything refused it and still took a coin; dropped, it lay on the floor with its id and picked up as the same stand-in; the script turned the stacks back into Demo Crate on the spot; `registry.missing` listed 4 stacks in the inventory, `removemissing` without a mod refused and with `mod=ScriptRunner` removed 4.)
  1. `oben run scripts/examples/missing-item.cs`.
  2. `mods.reload id=ScriptRunner`.
  3. Save and load. The inventory shows 2× "Missing item (ScriptRunner: Demo Crate)", and `<save>/mods/ObenFramework.items.json` lists it.
  4. A shop refuses it. Dropping it and picking it up again keeps the id.
  5. Running the script again turns the stack back into Demo Crate in place.
  6. `registry.missing` lists both stacks with their place; `registry.removemissing mod=ScriptRunner` deletes them (without `mod` or `all=true` it refuses).
- [x] **Missing recipes [save]:** (2026-10-30, on a copy of a real save: all as below; the save's file listed the recipe next to the item.)
  1. `oben run scripts/examples/missing-recipe.cs`, then `mods.reload id=ScriptRunner`.
  2. `oben eval "RecipeDatabase.instance.FetchRecipesById(<id>).Name"` gives "Missing recipe (ScriptRunner: Demo Sausage)".
  3. Copying a blueprint's data keeps the id: `Item.CloneMeta` of an `ItemTaskItemData` with a `RecipeReference` to it returns the same id (the game made it 0).
  4. `RecipeController.instance.AddRecipe(new RecipeReference(null) { ID = 1234567 })` returns true instead of throwing.
  5. Save and load: the recipe list shows the stand-in (`registry.placeholders` lists it under `recipes`). Running the script again puts the real recipe in the list on the spot.

## 2. UI kit, HUD, main menu, UI state (M05, M07, M26, M29)

Older key-press checks still open from the first UI work:

- [x] MODS on the main menu opens the mod menu with keyboard and controller. (2026-10-31: with the menu's selection on MODS, a faked Enter and a faked pad A each opened it through the game's own menu handler. A click by hand is still to try, but it is the button's own onClick.)
- [x] Esc over the pause menu's MODS page goes back one level, not out of the game. (2026-10-31, faked Esc: the MODS page closed, the pause menu stayed open and paused.)
- [ ] Keyboard navigation through every MODS page: Mods, Settings, Hotkeys, What's New, Errors.
- [ ] The Cheat Menu opens on Insert.

New:

- [x] **Settings page and config files (M04):** passed 2026-10-29.
  - With `"StackSize": 500.0` and `"Spoilage": "NaN"` in Game Tweaks' config.json, the start logged two warnings, set 20 and 0.5, and rewrote the file.
  - The log line `config.json: StackSize 500.0 -> 20.0, Spoilage "NaN" -> 0.5` appeared, and every later save logged its changes.
  - The Game tweaks page shows Needs, Items, Crafting, Renovations, Time and Camera cards, with "Reset to defaults" at the bottom.
  - Reset asks first; No changes nothing, Yes puts StackSize 20 back to 2 in the file.
- [x] **Confirm dialogs have their No button again:** before, building the footer threw "Value does not fall within the expected range" and only Yes showed. Passed 2026-10-29.
- [x] **Show mod hints / Show mod key prompts (UI kit settings):** passed 2026-10-29 from scripts.
  - With hints off, `Hud.Hint` returns `IsActive` False, while a forced hint is still active; on again, hints show.
  - With prompts off, `Hud.ShownPrompt` is null; on again, the prompt comes back.
- [x] **HUD (M26), checked 2026-10-31 on a copy of Slot1 in Interior Player Tenement:**
  - **Hints:** five hints from one owner and one from another: the fourth one waiting from the first owner was dropped (three each at most), the other owner's stayed. Changing the shown hint's `Text` redrew it in the game's hint panel. `HideHints(owner)` took that owner's two away and left the other's. Switching "Show mod hints" off through the settings page's own save dropped a waiting hint and the other mod's, kept a forced one on screen, and refused new ones until it was on again.
  - **Subtitles:** a blank `Subtitle` refused; `GameSubtitle` typed out in the game's own subtitle line above the kit's strip.
  - **Bars:** `Color = null` put the theme's fill back; a bar with an empty label drew no label line; one whose value threw on the first read was logged and came back inactive.
  - **Prompts:** asking twice gave the same prompt; a misspelt action ("Interactt") logged its warning; 20 more from one owner kept the newest 16; a prompt tied to an object was removed as soon as the object was destroyed; prompts hid while `Time.timeScale` was 0.
  - **Hiding the HUD:** the crosshair and the whole HUD (kit pieces with it) hid and came back when the script's scope ended. The game's no-UI screenshot taken while the HUD was hidden used to leave its own entry in the hide list (the HUD stayed hidden afterwards); with the fix the HUD came back.
  - **Notes:** `PlayerNotes.Open` answered false over the open inventory (the page stayed there) and true with no menu up, on the notes page.
  - `patches.check`: 510 patched methods, none broken.
- [ ] **Setting switches by hand:** flip both switches on MODS, Settings, UI kit and watch a mod hint and a prompt.
- [x] `oben call ui.state` in a level with nothing open: `inWorld: true`, `busy: false`, `windows: []`. Passed 2026-10-29, with `captureHooks`, `rawKeyHooks` and `pauseHooks` true.
- [x] **Input and UI state (M05):** passed 2026-10-29 in the tenement.
  - **Raw keys:** every `Input.GetKey*(KeyCode)` call in `GameController.Update`, `DeveloperConsole.ShouldToggleDevConsole` and the three `InteractableList` methods goes through `RawKeys` (6, 6, 2, 2, 2; none left).
  - **What each capture holds:** `Key(F12)` holds only F12; `Action("Screenshot")` holds F11; `All` holds 1, 4 and X, but not F1, F11, F12 or pad buttons.
  - **F12 tapped through Unity:** with nothing held, `TakeScreenshot.ScreenshotNoUI` ran; with F12 held it didn't; with `All` it still ran.
  - **Real presses by the owner:** F1 held kept the console shut and F1 free opened it; F12 held did nothing.
  - **Hotkeys pressed by the owner:** test hotkeys K and Ctrl+K. K fired only K; Ctrl+K fired only Ctrl+K. `Down`, `Held`, `Up` were logged in order, and letting go of Ctrl before K ended Ctrl+K without a stray K "up" (fixed during the test). The K clash with the game's Hints key was logged at registration.
  - **Window over the inventory:** opening `ui.demo` over the inventory closed the inventory; closing the window left nothing locked. `CursorVisible` and `InventoryDisabled` follow the game.
  - **Pad buttons:** `JoystickButton3` reads "Pad Y" and clashes with the game's controller Y.
- [ ] **By hand with a pad:** bind a mod hotkey to a pad button on the Hotkeys page and press it. Hold a capture of `Key(Alpha1)` with a tool equipped: 1 doesn't put it away.
- [ ] **`oben call ui.demo`, Choices tab:**
  - a segmented Slow/Normal/Fast/Silly row with Normal lit;
  - seven swatches with a frame on red;
  - steppers showing 3 and 1.00x, whose `<` greys out at 0.25x;
  - a text field;
  - a card with a toggle plus Toast and Confirm buttons;
  - a tooltip after resting the mouse 0.5 s on a control;
  - W/S move between controls; A/D on the tab row switch tabs.
- [ ] **Lists tab:**
  - a list of 1240 items with a thin red scrollbar, and a second list of 100,000 rows;
  - Top, Middle and End jump the long list (row 50000 and 99999 are highlighted);
  - up and down move inside a list and leave it at its ends;
  - scrolling doesn't stutter;
  - scroll the long list down, then make the window rebuild (switch tab and back): it stays where it was (fixed: it jumped to the top).
- [ ] **Model tab:**
  - a lit, spinning coin on dark grey; drag turns it, the wheel zooms, Enter resets it;
  - the "Item id" stepper swaps the model;
  - after closing, no coin is left anywhere and the scene's lighting is unchanged.
- [ ] **Windows tab:**
  - Left, Right and Floating windows open, and the floating one drags by its title;
  - the display-only panel lets you walk, Esc opens the pause menu, and "Close the panel" closes it;
  - Fragile → Throw closes the window with a "Fragile window closed: Error" toast, and the error is logged;
  - with "Capture Jump" on, Space doesn't jump while the panel is open and does again after closing.
- [ ] **HUD tab by hand:** the demo window's buttons and toggles ("Prompt over Interact", hide the crosshair, hide the HUD). The calls behind them passed from scripts (below); the clicks are left.
- [x] **Prompt:** `hud.prompt text="Open the crate" action=Interact seconds=15` showed over E in the game's tutorial panel (`inGameTutorialPanel: true`); E (faked through `Input.GetKeyDown`) gave the toast; `hud.prompt hide=true` removed it. (2026-10-31)
- [x] **Guide:** `hud.guide` ran "Jump" (Space), "Open your map" (M opened the map; the next step waited until it closed), "Walk a few steps" (done past 3 m) and "That's the demo guide", then ended; `hud.guide stop=true` stopped a fresh one. (2026-10-31)
- [x] **Input capture:** `input.capture action=Jump seconds=10` made `ui.state` show `jumpCaptured: true`, false again after 10 s. (2026-10-31)
- [ ] **Input capture by hand:** hold Space during `input.capture action=Jump seconds=10`: no jump. (A faked key doesn't reach the game's jump: it reads `Input.GetKey`, which the JIT inlines, so a script patch on it never runs.)
- [x] **Main menu buttons (M29), 2026-10-31 at 2560x1440, screenshots:**
  - Co-op sat under MODS and `menu.buttons add="Demo A"` under Credits, in the menu font, on screen.
  - With seven more, two rows fitted (six buttons); the third ran below the screen. Fixed: the rows are measured through the menu camera and the last place becomes "More…". With Co-op and Demo A to G the menu showed Co-op, A, B / C, D, More…; More opened a small window with E, F, G; "Demo F" there showed its toast; renaming G and greying E out showed in the open window; removing two closed it and all six sat on the menu.
  - Navigation through the game's own calls: Credits ↓ Demo A ↓ Demo D → More ↑ Demo B ↑ Quit ← Credits; MODS ↓ Co-op (← stays) ↓ Demo C → Demo D → More (→ stays).
  - Enter (faked) on the More window's Back closed it five times out of five without the menu clicking More again, and More kept the menu's highlight.
  - `Interactable = false` didn't grey the button (only ignored clicks); fixed, it now greys like the pause menu's own.
  - Seven buttons added while the camera was over Load Game came out the same once back on the sign.
  - `menu.buttons remove=true` removed the demo buttons and left Co-op.
- [ ] **What's New:** MODS → Settings → "UI kit" → "Read What's New" opens the page. "Show What's New at the next start" clears `LastSeenVersion`, and the popup shows on the next launch.
- [ ] **Camera after a kit window:** sit on a chair (or lie in bed), open and close a kit window. The camera stays locked the way the game had it.
- [x] **Mod menu (M06).** Passed 2026-10-29 after a fresh start: the script-driven parts by script (notes per item), the key presses, hover and wheel by hand.
  - [x] **Configs found by themselves:** MODS → Settings → the Mod cycle lists Cheat Menu, DevBridge, DevBridge (trace), Game tweaks, ObenCoop, Performance Fixes and UI kit, each once. hotkeys.json isn't there. Cheat Menu shows only Key; Spots, Watches and MoveSpeed stay hidden.
  - [x] **Descriptions and lists:** the ObenCoop fields have their descriptions. "Ignored mods" reads "DevBridge, ScriptRunner"; changing it writes a JSON list to the file. (Typing "DevBridge, ScriptRunner , CheatMenu," wrote the three names; the field then kept the raw text, fixed: it shows what was saved.)
  - **Reset per setting:**
    - [x] Change Game tweaks' stack size; "Reset" appears at the right end of its title bar.
    - [x] Hovering it shows "Default: 2", and clicking it puts the value back and hides it.
    - [x] Backspace on the highlighted row does the same. Backspace on an unchanged row does nothing. (The pad's X wasn't tried.)
  - [x] **Search:** (passed by script: "speed" gave one Game tweaks card with its three speed settings, an edit there was written, "game tweaks" listed all 17, "port" found Dev Bridge and ObenCoop.)
    - Type "speed" in the search box and press Enter: a card per mod with a matching setting.
    - Change a value in the results: the file is written.
    - "Clear the search" goes back to one mod at a time, and closing and reopening the menu empties the search.
    - "game tweaks" finds every Game tweaks setting.
  - **Page keys:**
    - [x] PgUp/PgDn and Q turn pages, wrapping at both ends. Q in the search box types a q. (The pad's bumpers weren't tried.)
    - [x] The footer hint fits next to Back (three short lines).
  - [x] **A broken page:** a script adds a page whose build throws. The page shows "This page couldn't be shown", the error is in the kit's log, and the page row still turns.
  - [x] **Quit flush:** drag a slider and press Alt+F4 within a quarter second. The value is in the file. (By script: a slider set and `Application.Quit()` in the same frame; `CraftingSpeed 2.0 -> 3.0` was logged right before "Game closing" and is in the file.)
  - [x] **New field kinds:** from a script, `ModSettings.Draw` a test config with a `[Flags]` enum, a `Vector3` and a `[Setting(Choices = ...)]` string into a window. They show as switches, a text field and a cycle; text that isn't three numbers puts the old vector back. (Passed after a fix: a config holding a Vector3 or Color couldn't be saved at all, Newtonsoft stopped on `Vector3.normalized` as a loop. They save as `{"x", "y", "z"}` now.)
- [x] **Scrolling in kit windows:** (2026-10-29, by hand.) the mouse wheel scrolls MODS anywhere over the page (over text and gaps, not only over controls); a thin bar on the right shows the position and can be dragged, and it is gone on a page that fits. On What's New, Down and Up scroll the notes a step at a time. (By script: the wheel's raycast reaches the body from text and gaps, and Down/Up stepped What's New by 340 px.)
- [x] **What's New look:** release title, date line, colored counts, a card per mod, lines tagged feat/change/fix/remove in color with wrapped lines lined up. "MODS > Cheats" shows a real ">". (2026-10-29, screenshot.)
- [x] **Errors page colors:** a caught problem (amber) with an inner exception and an uncaught one through Debug.LogException (red): "Caused by" on its own line, Unity's and Mono's frames colored, `S.Loader.Items (lambda)` and `(async or iterator)` names, System and Unity frames dimmed, "Show all 15 lines" and "Show less". (2026-10-29, screenshots.)
- [x] **MODS button color:** MODS on the main menu has the same color as the other buttons (it kept the "???" placeholder's grey). (2026-10-29.)

- [ ] **UI kit parity (M07).** Needs the kit deployed; `oben call ui.demo` opens the demo window.
  - **Slider and cycle fixes:** a demo slider moved from code (`SliderHandle.Set`) shows the new number; clicking the middle of a cycle row steps it.
  - **Full window:** Docked and floating, then Full. The window covers the screen less a margin, the scene is dimmed, the side panel lists 25 items with its own scrollbar, the body 30 lines with its own. Picking an item rebuilds the body for it (through `Defer`). Up and Down go through the side's items first, and the side scrolls to keep the highlight in view.
  - **Close reasons:** load a save from the pause menu with a kit window open: it closes (`SaveLoad`). A script that destroys a window's frame object closes it with `Destroyed`, and the game's keys come back.
  - **Pause Menu key:** with the game's Pause Menu action bound to another key, that key closes the top kit window too; the pad's Start does.
  - [x] **Top inset:** a Right window with `Top = 110` starts 110 units below the top. (2026-10-29: measured 110 exactly, outside in third person; 110 still covers the RM counter, so the docs say 150.)
  - **More tab of the demo:** the title, the green smaller label, Plain/Quiet/Box buttons (the boxes light up red), the Volume slider's Reset (shown while it isn't 0.50, on hover "Default: 0.50", Backspace on the row) and Max on its title bar side by side, the Ctrl+F7 key binding (hold Ctrl, Shift or Alt and press a key; a modifier alone doesn't finish it), twelve swatches in two rows without a header, the scroll area with its own bar (the wheel scrolls it, the keyboard highlight scrolls it), and the blue box with rounded corners (`UiSprites.RoundedRect`) drawn in a raw `Area`, with a dark-to-grey gradient strip under it.
  - **Slider once a frame:** dragging a slider whose callback logs prints at most one line a frame, the last value.
  - [x] **Live player preview:** (2026-10-29: first person with Body and Face framing, closing put both rigs and all eleven body renderers back off; third person in the tenement and outside, where the level switches the meshes' holder off, after a fix; dragging turns the player like a copy after a fix: right moves the face right, up moves it up. The idle animation by eye is still open.) In the tenement, demo, Model tab, "Show the player, live". The player shows from the front, breathing (idle animation), in their clothes, and their body is gone from the scene behind; Body and Face switch the framing; dragging circles them. In first person too (the body is switched on for the preview). Closing the window brings the body back as it was (gone again in first person, shown in third person), with no lamp light left off.
  - **Framing a copy:** an item or mod model with Face framing shows its top part.

- [x] **Co-op in the menus, pause menu buttons, mod version** (2026-10-31, screenshots):
  - The main menu has Co-op under the grid; the pause menu has Co-op above Mods. Both opened the Co-op window ("Not in a co-op game."). The pause menu's own keyboard moves went Difficulty → Co-op → Mods → Quit to Menu; the main menu's are above.
  - The pause menu shows "ObenseuerExtended" and "v0.19.0 (241390d-dirty)" just above "V. 0.5.13", same size and colour.
  - After several kit and framework hot reloads Co-op came back on both menus, with MODS and the version line.

## 3. Game Tweaks, developer menu, tracer, ObenPatches

Switch each tweak on with `oben call features.set mod=GameTweaks id=<id> enabled=true` in `new_scene Interior Player Tenement`. The guide `docs/modding/game-tweaks.md` has the exact evals.

| Tweak | How to check | Off | On |
| --- | --- | --- | --- |
| `Needs` | `player.drain` | no GameTweaks entries | multiplier 0.5 per need |
| `StackSize` | `items.find query=plank` → `stackable` | 8 | 16 |
| `Spoilage` | eval in the guide (`RemoveMinutesFromIntegrity(60)`) | 0.9 | 0.95 |
| `CraftingSpeed` | `crafting.speed` | — | key `*` at 2 |
| `DishwasherSpeed` | `crafting.speed` | — | key `Dishwasher` at 2 |
| `NoMinimumSkill` | recipe 10016 eval in the guide | `False 21600` | `True 7200` |
| `RenovationCost` | `renovations.list state=Available`, Makeshift stairwell | 3500 / 175 | 1750 / 88 |
| `RenovationTime` | same, hours | 24 | 12 |
| `RenovationMoneyOnly` | `oben eval "TenementController.instance.CheckForItem(35010, 9999f)"` | False | True |
| `DayLength` | `m_dayLength` via Traverse (guide) | 96 | 192 |
| `DayLength` | `clock.get` twice, 30 real seconds apart | ~7.5 game min | ~3.75 game min |

- [x] Every row of the table, then switch each tweak off again: the value returns. Passed 2026-10-29 in the tenement (`player.skills skill=Mashing value=1` first for `NoMinimumSkill`): every off and on value as in the table, 7.46 game minutes in 30 s off and 3.72 on, and all back to the off values after switching off. `GameTweaks.TweakState` read 1/false for everything off and the tweak values on, and `OnChanged` fired on each switch and config save.
- [x] **ThirdPerson:** passed 2026-10-29, running the F6 hotkey's action from a script. (Correction, same day: the 2.5 m was right but the camera sat above the head, along the game's leftover dolly direction, and outside the body didn't show. Fixed: behind the view and a little above, the body shown everywhere; checked outside with a screenshot.)
  - F6 moves the camera 2.5 m behind the player, and `firstperson` is False.
  - F6 again, or switching the tweak off in third person, gives first person with both locks True.
  - A zoom blocker shows "The camera has to stay in first person here." once.
  - Scroll to zoom on lifts the locks; off puts them back.
- [ ] **ThirdPerson by hand:** press the real F6 key, and walk into a wall in third person. Walk around: the camera stays behind, and the body turns with the mouse like the game's own (over-the-shoulder style).
- [ ] **ThirdPerson [save]:** save while in third person. The save holds `disableThirdPerson:true` and `firstperson:true`.
- [x] **CameraView (framework):** passed 2026-10-29 in the tenement through `player.camera`:
  - `mode=third distance=3` unlocks both locks and goes to third person (`firstPerson` False a frame later).
  - A zoom blocker (`DisableZoom`) and a `PlayerCameraTrigger.FirstPersonOnly(true)` each pull the camera in with `heldBack` True; when they let go, it goes back out to 3 m.
  - `mode=first` gives first person with both locks True again.
  - `body=true` keeps `playerRig` active in first person; `body=false` hides it.
  - `ModeChanged` fired False, then True.
  - While `OnSavingGame` serialises the camera, it reads first person, locked, saved distance 0; right after, it is back in third person. Still to do: a real save.
- [x] **Developer menu:** passed 2026-10-30 in `new_scene Interior Player Tenement`, driving the menu's own buttons from a script:
  - **Quests:** `DevBridge.test` (made with `quests.create`) shows the rewind buttons.
  - **Flags:** "Set a flag by name" sets `DevMenuTestFlag`, and filtering on `devmenu` lists it as On.
  - **Watches:** TimeOfDayAzure / `timeSpeedOverrideFactor` reads 2.0 after the speed went to 2.
  - **Session and story:** the values read live.
  - **Player and world:** setting health from the stat slider.
- [x] **Watches over the game:** passed 2026-10-30. "Show them over the game" puts the watches in a box at the top left (an unknown type reads "can't be read"), it follows a change within a quarter of a second, steps aside while a window is open, and `devmenu watches` in the F1 console hides it (`WatchesOnScreen` false in config.json).
- [x] **Watches over the game, F9:** press the real F9 key in a level; the box comes and goes. A mod's `DevMenu.AddWatch` shows in it too. Passed 2026-10-30 by the owner (with a Script Runner watch counting frames).
- [x] **Log part:** passed 2026-10-30. A Unity warning with `<b>bold</b>` in it shows the tags as text in amber, an error shows red with "+47 lines", Follow keeps the newest line in view, and picking a line shows all of it below.
- [x] **Log part by hand:** change Show, From and the filter, and click a row with the mouse; "Copy it" puts the line on the clipboard. Passed 2026-10-30 by the owner.
- [x] **NPCs part:** passed 2026-10-30 in the tenement: Malone (37) and Seamus (125, hidden, greyed and listed after Malone). Go to it puts the player 1.5 m in front of Malone, facing him; Bring it here says Malone has no path finding. A spawned copy of character 84 ("Testy", CheatMenu/dev1) is listed in both lists; Bring it here, Follow me, Stop following and Look at me are done, Stop its activity says it isn't doing anything, and Despawn removes it.
- [x] **Player and world, quests and console additions:** passed 2026-10-30 through the menu's buttons: Lockpicking set to 7.5; coordinates 97.5, -89.9, 23,5 (comma) take the player there and "abc" gets a sentence; Where I am refills X; searching 14 with How many 3 gives 3 sausages; Made by DevBridge lists only `DevBridge.test`, its line says "Now on: Open crates (0/5)", typing 3 in the counter field gives 3/5 (the game's "Objective Updated" shows); `devmenu` opens and closes the menu and `devmenu watches` hides the watches. In the Cheats window, 13 and 47 on the Hour and Minute sliders give 13:47.
- [x] **Save round trip of the developer menu:** passed 2026-10-30 on a copy of Slot2 loaded as `Dev_Test` (player renamed, the owner's folder untouched):
  - **Relationships:** "VeryGoodFriend" added through `RelationshipController.AddRelationship`, set to 42 from the menu (target 42 too), saved and loaded: still 42 to 42.
  - **Kill it:** on Meri Haka (99), a plain scene NPC without a controller (it used to be refused; kill, hit, talk and look now work on every NPC), with the owner clicking Yes in the dialog: Dead. After save and load she is alive again: the game doesn't save `NPCInfo.state` (`[fsIgnore]`), so the docs and the dialog say so now.
  - **Items of removed mods:** `scripts/examples/missing-item.cs`, `mods.reload id=ScriptRunner`, save and load: the menu lists 2 Demo Crate stacks of ScriptRunner; "Remove the stacks of ScriptRunner" with Yes clears them (0 left, `Scan` empty).
  - **Dev mark:** `mods/CheatMenu.dev.json` in the saved slot has `Modified: true`.
  - **Tracer marks:** "game saved (day 0 19:43, player at (26.01, -100, -0.5), saving no, loading no)" and "save loaded (...)", plus the framework's warning about the Demo Crate mirrored in between.
- [x] **Reopened task:** finish `DevBridge.test`, then set it Active from the dev menu. `oben eval "TaskController.instance.TaskFinished(\"DevBridge.test\") + \" \" + TaskController.instance.TaskActive(\"DevBridge.test\")"` gives `False True` (fixed: both were true). Passed 2026-10-30: `True False` after Finished, `False True` after Active.
- [ ] **Third person in a tight spot:** in third person, walk into a real first-person camera trigger. The camera goes in, a "has to stay in first person" toast shows once, and the camera goes back out after the trigger.
- [x] **Tracer:** passed 2026-10-30.
  - `trace on Quest,GlobalFlags` in the F1 console: 58 methods, 0 refused, 121 ms.
  - The dev menu's flag and quest buttons show up in `trace.list` with `from: "CheatMenu"` (`QuestLog.Available` takes 15 to 17 ms a call).
  - The trace opens with marks for the mods loaded, what was patched before it (485 methods, the tracer's own left out) and the state (`day 0 18:13, player at (97.5, -89.9, 23.5), saving no, loading no`).
  - `LogHub.Verbose` is true while it runs and false after; `trace off` writes `ObenLoader/logs/trace.log` with nested calls indented.
  - `trace.start filter=ObenFramework.Clock internals=true`: 79 methods instead of 41, `TimeControlPatches.Prefix` x1636 collapsed into "more" records.
  - With `MirrorLinesPerSecond` at 20, 50 log lines give 20 marks and "30 log lines left out of the trace (more than 20 a second)".
  - No filter: 1853 methods in 4886 ms; `trace.stop` says `active: false` and the earlier records are still readable.
- [x] **Tracer marks on a real save:** see the save round trip above (2026-10-30).
- [x] **Tracer, patches:** (2026-10-29: all as expected.)
  - `trace.start filter=ObenFramework.Clock handlers=true` picks one more method than `handlers=false` (42 and 41), and `trace.list` shows `TimeControlPatches.Prefix` called every frame.
  - With a trace running, `features.set mod=PerformanceFixes id=TimerSnapshot enabled=false`, then `enabled=true`: `trace.list` has the mark `patch on: PerformanceFixes TimeOfDayAzure.UpdateTimers (prefix)`.
  - `trace.stop`, then `patches.list` has no `DevBridge.trace` owner.
- [x] **patches.list:** lists the owners (ObenFramework 442, ObenPatches 15, ...); `mod=PerformanceFixes` gives its 8 methods with their handlers. (2026-10-29.)
- [x] **Failed feature and framework part leave no patches:** `oben run scripts/examples/broken-feature.cs` returns `enabled False, patched False` and `help` has no extra line. A framework part started from a script with a throwing setup in its own `[PatchArea("DemoPart")]` leaves `DemoPart` switched off with "the 'demo part' part failed to start". Reload the framework afterwards. (2026-10-29.)
- [x] **Unloading restores the game's methods:** `mods.unload id=PerformanceFixes` drops its 8 methods from `patches.list` and the game keeps running on the original code (`Inventory.AllSlots` answers); `mods.load` puts them back with `broken: []`. (2026-10-29.)
- [ ] **RenovationMaterialsOnce [save]:** build two tenement resource storages with 10 planks each, then order a renovation needing 10 planks. One storage keeps its 10, and the inventory isn't touched.
- [ ] **Cheat Menu:** all its earlier checks (god mode, needs, heal, noclip, move speed through the framework, teleport spots, money, items, time, weather, crime).
  - Move speed survives a door and is back to normal after unloading the Cheat Menu.

## 4. Co-op [2 copies]

Passed on 2026-10-30 with two copies on this machine (protocol 2), direct play:

1. [x] Copy 1: `coop lobby create Test` prints "Hosting for up to 4 players." and "Lobby open: Test. Join code ****-****-**** (***.***.***.***:27890)" (codes are stars with `HideJoinCodes` on, the default). `coop.status` shows lobby Hosting with the code.
2. [x] Copy 2: `coop lobby list`, then `coop.lobbies --instance 2`: results[0] "Test", verdict Ok, 1 player, 29 ms.
3. [x] Joining: from the co-op window's list (below) and with `coop join`; both copies show 2 players, Hosting / Joined. `coop say hello from one` arrives on copy 2.
4. [x] `coop invite` copies the real code (read back from the clipboard: KDE's and the other copy's) while the text shows stars. `coop leave`, then `coop join <code>` joins again.
5. [x] **Presence** in Interior Player Tenement: copy 2 lists copy 1 in the same area; a 2.5 m teleport is followed within a second; a 12 m one shows `Teleported` for about half a second and snaps (no slide); `coop presence off` empties copy 2's remotes at once, `on` brings it back.
6. [x] **Hot reload while connected:** both copies keep 2 players and the lobby; the log says "Carried the session over a reload" and "Carried the lobby over a reload"; `coop lobby list` from copy 2 still finds Test 2/4. (Fixed: ObenCoop failed to load on the reload, "Failed to resolve assembly ObenCoop.Net-r2", a loader bug with enum default values from a reloaded DLL; and after a reload the host refused joiners with "The host doesn't have: ObenCoop", its mod set was made before it counted as loaded.) The presence part (copy 1 reloaded alone, no gap) wasn't repeated.
7. [x] `coop join 127.0.0.1:27999`: "Nobody answered at that address." after 8.6 s, in `coop.status` lobby.error, shown once (fixed: it was shown twice).
8. [x] `coop status` says "Steam: ready, signed in as Account Name (*****)" (the emulator's account; the id is stars in streamer mode).
9. [x] **Blob transfer and named channels through `Coop`** (scripts/coop-*.cs in the session's scratch, the same calls another mod makes): `Coop.Listen` and `ListenBlobs` in copy 2, `Coop.Broadcast` and `SendBlob` from copy 1. The message arrives with its sender, a 3 MB blob arrives intact (same checksum, 1.58 MB on the wire), the sender sees Done.
10. [x] **Co-op window** (screenshots): not in a game it shows Host a game (name, Direct or Steam, the port hint), Join a game (masked field) and Find games (Search, the list with name, players and ping); in a game the join code line, Copy join code, Show, the players with host, you and ping, Leave. Show reveals the code and address and turns into Hide.
11. [x] **Spacewar on the emulator:** `SteamApp: spacewar` in config.json and a restart: the log says "Steam will start as app 480", `GameSteam.AppId` is 480, the game doesn't relaunch, `coop status` says "ready as Spacewar (480)". Back to `obenseuer` and a restart: 951240. The emulator can't create Steam lobbies on either.
12. [x] **Real Steam, one account** (Valve's `steam_api64.dll.original` swapped in for the test and the emulator put back; this copy isn't on the Steam account; Proton started directly with `STEAM_COMPAT_CLIENT_INSTALL_PATH`, since umu leaves it empty and Steam then can't start on any app): as `spacewar` Steam is ready on 480 with the real account and `coop lobby create steam` opens a lobby (Hosting, a Steam code); as `toxikk` the same on 324810; as `obenseuer` the game closes itself a second after the main menu (its relaunch check). The overlay isn't available that way.
13. [ ] **Real Steam, two accounts:** host a Steam lobby (Spacewar on both), find it as a friend, join by the friend list's "Join game", by an invite and by the code, P2P traffic, the host leaving. Then the same started from Steam as a non-Steam game (overlay, Invite friends).
14. [ ] With a pad and the keyboard only: the co-op window's fields, choices, list and buttons can all be reached.
15. [x] Mod events through `Coop.OnStarted/OnPlayerJoined/OnPlayerLeft/OnEnded` from a script: hosting, a join, a leave and stopping each raise theirs once.

## 5. Story, quests, dialogue, law, new game (M14, M28, M29c)

Already checked without a save: quest create, edit, counter, reveal, hand-in, checkpoint, finish, restore and jump; the duplicate-title refusal; Lua `Oben_QuestState`; dialogue lines and adding a response; law commit, fine and clear; `difficulty.get`; `tutorial.state`; `session.info`.

- [x] `oben call story.queue` in a loaded save shows `session` SaveLoad, `ready` true and nothing pending. Going through a door doesn't change that. (2026-10-30, on a copy of a real save, also after a level change to the Bazaar.)
- [x] **Mod quest save [save]:** passed 2026-10-30.
  - The test quest at counter 2 of 3, saved and loaded: texts, states and counter unchanged, `ObenFramework.quests.json` written, `modquest list` lists it, and a jump and reset after the load work from the saved definition.
  - The removed-mod case, with a quest owned by Cheat Menu (DevBridge can't be switched off without losing the bridge): with Cheat Menu unloaded, the quest stays in the log as it was and `modquest reset` refuses it. Saving and loading without the mod works and keeps its lines in `ObenFramework.quests.json`. Once the mod is loaded again, the quest moves again.
- [x] **Quest notification:** "New Task" and "New Objective Step one 0/3" show on create, and both mod tasks are listed on the game's Tasks page (screenshots, 2026-10-30).
- [x] **Added response:** passed 2026-10-30 in the Bazaar (Jens is hidden more than 20 m away, so teleport next to him first).
  - Added on anchors 1, 4, 5 and 6, the choice shows under the game's own two choices for greetings 1 and 4. Picking it logs "Dialogue response picked", Jens answers "No." and the talk ends.
  - After a DevBridge hot reload, nothing is registered and greeting 5 has only the game's choices.
- [x] **Branch:** "Any work?" shows after greeting 1 and plays "Maybe." and "Later.", logging "Dialogue topic picked". `dialogue.narrate text="Test"` shows on the black screen (2026-10-30).
- [x] **Interception:** with `story-intercept.cs`, a severity-40 crime goes on the record as 20 and the conversation start is logged; after `--reset` a crime counts in full (2026-10-30).
- [x] **Clearing a mod's dialogue:** `dialogue.clear` removes nothing while a conversation is open (one warning in the log), and after it closes removes a branch, a response and two conversations (4) at once; Jens shows only his own choices again (2026-10-30).
- [x] **Game variables and early removal:** `lua.set name=Day` and `name=DREAM_1` are refused, and a mod variable saves and loads. From a script, a mod's Lua function is removed by its own mod but not by another; `StopIntercepting` takes out a scope's conversation and crime interceptors at once (2026-10-30).
- [x] **Difficulty factor** (2026-10-31, in a new game's Interior Start with the stats switched on, since they are off there until the opening is done): hunger +1.063 and thirst +2.5 in 40.6 s without a factor; at `difficulty.set factor=NeedsSpeed value=0.5`, +0.532 and +1.25 in the same time.
- [x] **New Game screen** (2026-10-31, at the main menu with `newgame.testoptions`, a 0 to 1000 option and `difficulty.presets add=harsh name=Harsh needs=1.5`; screenshots):
  - "Harsh" is in the difficulty selector after Custom, with needs at 1.5×.
  - On Custom, the two general options sit at the top of the game's list and the two custom-rule options at its end. On Casual and Default the list shows the level's description with the general options under it.
  - Clicking an arrow changed the choice; the page's own navigation (down, right) reached both rows and changed them, and down from the last row went to Next.
  - Hovering a row showed its title and "From Dev Bridge." in the description box.
  - The 0 to 1000 option, registered with the page open, appeared at once and moved 50 a press, stopping at 1000 before wrapping.
  - "Show mod difficulty presets" off took Harsh out of the selector, on put it back.
- [x] **New game start** (2026-10-31, as Dev Test with the intro skipped):
  - `newgame.last` had the name, Harsh (preset `DevBridge.harsh`) and the five option values picked on the screen. In Interior Start the session was `NewGame` and every option read its stored value.
  - Registering an option during the game logged the late warning; DevBridge reloading its stored options logged none.
  - Five backstory picks through the game's cards each raised `Chosen` once, and `start.choices` listed them.
  - The game's first autosave held `ObenFramework.newgame.json` with the options and the preset. Loaded from the main menu (where the screen was back to its defaults), the session was `SaveLoad`, the options read their stored values and `activePreset` was the preset.
  - A pick left on the screen was dropped at the next load.
- [x] **Opening tutorial (M29c):** passed 2026-10-31 in a new game as Dev Test (intro skipped), with the input faked from a script (`InputManager.GetKeyDown`/`GetKey`, the camera's `finalInputX`) and a recorder on `TutorialGuide.StepChanged`:
  - Interior Start started the sequence about 3 s after the fade: LookAround, then (each after the faked input, with the game's 2 s pause as `None`) Move, Interact (looking at the TV), Menu; the level's own `TutorialInventory` gave Inventory, `TutorialMap` gave Map, which went straight on to Tasks; Tasks dropped the package. `tutorial.state` matched at every point.
  - Head lamp shown and hidden, Building, then Tenement over it (it ranks first, as on screen); finishing building hid the tenement prompt too (Step Tenement, Active false: the game's own doing).
  - A level change to Open Sewer Tenement (the opening room can't be saved) brought the tenement prompt back on screen, reported as None → Tenement; its key finished it. A reload prompt without batteries came and went after 2.5 s.
- [ ] **Opening tutorial by hand:** real mouse look, WASD, E, Tab and the rest in a new game; the same changes as above (the script faked the inputs, not the devices).

## 6. Economy and gambling (M23, M27)

Already checked in Open Sewer Bazaar:
- **Stalburg Bank:** withdraw, refused deposit, transfer, quote, the ledger, invoices paid from the account, freeze and unfreeze.
- **Traders:** the list, Brom's hours, a ×2 price factor and back.
- **Also:** junk mail senders, the casino machine and table lists, `gambling.payout clear`.

- [x] `bank.ledger` keeps its entries after a framework hot reload. (2026-10-31: all 3 entries after a reload of every mod.)
- [x] While an account is frozen, `legal.list` shows UnfreezeAccount_Stalburg Bank with `canTrigger` true. (2026-10-31; the unfreeze then booked an `Unfrozen` entry.)
- [x] `traders.factor "shop=Brom's" buy=2` also doubles the prices in the trade window itself (screenshot), and `buy=1` resets them. (2026-10-31: Kalja 4 for 104 instead of 52, feathers 14 for 56; back after `buy=1`.)
- [x] **Interior Tenement Kolhola**, "Money exchange": `bank.exchange "bank=Money exchange" amount=100 to=rm` takes 100 OC. `amount=1` gives TooSmall. (2026-10-31: it pays 5 RM, not the 6 expected here, and the quote says 5 too: the game cuts `100 * 0.06f` = 5.99999 to 5. Logged as ExchangeCreditTruncation in TODO.md.)
- [x] **Open Sewer Mines:** `fuel.stations` lists the station; `fuel.price station=<id> price=10` sets it; filling up and paying records FuelBought. (2026-10-31: price 10 raised `FuelPrice`; 3 litres metered on the pump and paid through the game's `GasStation.PayFuelOc` took 30 OC and raised `FuelBought` with 3 l for 30. Filling a real minitrain wasn't tried.)
- [x] **Interior Bazaar Casino** (2026-10-31, on a copy of Slot1; reels and coin tosses decided by a script hook, the rest through the game's own methods):
  - **Slot machine, ×2:** 20 coins through the coin slot (wallet 300 → 280, one `CoinsInserted` each), stake raised to 2, a win the game worked out as 6 paid 12 (`Won` 12, statistics +12).
  - **Doubling:** a won toss 12 → 24; a coin on its edge `Doubled` 24 → 24 with the coins kept in the machine (42); a lost toss 24 → 0.
  - **Cash-out:** `CashedOut` 40 described the machine before its reset (stake 2, 40 in), wallet +40. `casino.machines` showed `lastWin` 24.
  - **Pajatso, ×3:** five full-force launches through a 1-coin gate paid 3 each, and the tray read "Collect 15 OC". Collecting paid 15 (`CashedOut`). At ×0 a gate win left the tray at 0, dark, with the button on "Empty".
  - **China Hand at the casino table, ×2:** sat down, picked "Shittypants" in the dialogue and 25 OC on the slider. `CardGameStarted` came at the deal with stake 25 and `playing` true. The game's own `FinishGame` (first place) paid 100 (wallet 304 → 404, statistics +100), and the framework's net count was +75.
  - **Giving up:** a Ristiseiska game for 10 left mid-game raised `CardGameGaveUp`, and the net count went back to 0.
- [x] **Scratch cards, ×2** (2026-10-31): eight OC Pots scratched from the inventory. The draw saw a top prize of 100 000, and the card had 50 000 again afterwards. One won 444: statistics +444, `Scratched` 444, and its receipt carries 444 to cash in. The seven losers left receipts worth 0.
- [x] **Interior Bazaar Ratfighting Arena, ×2** (2026-10-31):
  - Through Rosario Rootto's dialogue, the rat selector and the slider: 30 OC on Green (`RatBet`).
  - A second "(Place Bets)" was offered, but the round started first, so the game refused it.
  - Green won through `EndRound`: 120 scaled to 240 (`ContestFinished` 240, statistics +240), and collecting paid 240 (`RatWinnings`).
  - Two bets in one round (30, then 20, through `PlaceOCBet`) cost 50 and left a stake of 20: logged as RatBetOverwrite in TODO.md. A lost round reported Red as the winner.
- [x] **Interior Bazaar Bar:** Jooseppi's `StartContest` raised `ContestStarted`, and `contests.list` showed him with tolerance 1.0, then 0.958 after two drinks. `Contests.GiveUp()` ended it as a loss. (2026-10-31; his own dialogue only sells beer.)
- [x] **Interior Bazaar Sauna:** a contest with Timo Löyly started (opponent "Timo") and stopped as a win, `ContestFinished` with `won` true. (2026-10-31)
- [x] **Free and item tables** (2026-10-31):
  - The bookshop table plays for nothing (bets 0–0). Its game started with `CardGameStarted` stake 0 (it never fired at free tables before), and getting up raised `CardGameGaveUp`. The table sits in a story memory, so the game was chosen from code.
  - The Test Map Card Games table, set to play for apples: a stake of 2 paid 8 apples for first place at ×2 (statistics +24, three per apple).
- [x] `Casino`, `CardGames` and `Contests.IsAvailable` are true; `Payouts.Clear` removed the bridge's 6 multipliers in one call. patches.check: 515 patched, 0 broken; oben check: 800 hooks, 0 broken. (2026-10-31)
- [x] **Interior Tenement O Market:** `markets.list`, then buy something; CheckoutPaid fires. (2026-10-31: two cups carrying the market's ownership, run through the conveyor, scan and `Pay` from code; the game charged its 1 OC minimum and `CheckoutPaid` fired. The game's own pay loop saw Paper Trash on the conveyor, so the scan totalled 0; a cup picked from the shelf by hand would tell whether that is the made-up cup.)
- [x] **Player shop [save], in Interior Player Tenement:** (2026-10-31: 5 apples in the apartment's locker, 3 sold for 12 OC into the till and collected; 1 more left 4 OC in the till over a save and load, with the shop open on its area and the ledger's 4 entries.)
  1. `furniture.zones placed=true`.
  2. `playershops.create id=test`, then `playershops.set id=test area=<zone> open=true`.
  3. Put an item in a container there.
  4. `playershops.stock`, then `playershops.sell` fills the till, and `playershops.collect` moves it to the wallet.
  5. Save and reload: the shop and the ledger are still there.
- [x] **Trading from code, Open Sewer Bazaar and Interior Bazaar Ankea:** (2026-10-31, on a copy of Slot1)
  - `traders.buy "shop=Brom's" item=19010 count=9` bought 7 Kalja at 11 and 2 at 13 (their quality), 103 OC; Brom's till +103, two `Bought` events from `Mod`; the quality came along.
  - `traders.sell` of 5 paid 4 OC each (Brom's buys at 0.598), one `Sold`; the Kalja went back on his shelf.
  - Refusals: `CannotAfford`, `OutOfStock`, `BadInput` (money), `UnknownShop`, `NotEnoughItems`, `NotAccepted` (Kalja to SellOil, bricks to Brom's, who doesn't deal in Hardware). With the inventory full, feathers came back `NoRoom` and everything stayed as it was; Kalja still fit onto the player's Kalja stacks.
  - With the trade window open a buy updated both sides at once.
  - `furnitureshops.buy shop=Ankea "furniture=Test Chair" count=2` paid 162 OC (81 each at 117%), `sell` paid 70 (100%), the till and stock followed; `ShopCannotAfford` with the till at 10, `CannotAfford` with 19 OC.
- [x] **One-off trader stock:** `shops.addstock "shop=Brom's" item=14 amount=2 once=true` put 2 sausages in; bought out, a restock (`Storage.SpawnItems`) left them out while normal apple stock came back to 3, and they stayed out after a framework reload and after a save and load. (2026-10-31)
- [x] **Vending titles:** a vending entry's own title reaches `StockLine.Title`; the Bazaar's machines have none, so they show the item's title. (2026-10-31)

## 7. Environment and housing (M25, M25b)

Already checked:
- **Environment:** flags force and release; the FOV override (options file untouched); sky override, read-back and clear; the vignette; power device and grid on/off; heavy rain; radiation and water readings; the hazard list.
- **Housing:** apartments; tenants; `tenants.check` AlreadyRenting; renovations list.

- [x] **Flags [save]:** a forced flag never lands in the save. The first try found it in `Globals.tnmt`: the game only collects `GlobalState` in its save step and writes the file at the end of `SaveGame`, after our hook had forced it again. Fixed (the values underneath stay in until `SaveGame` returns); the next save held `obenext_perm` (set for good) and not the forced `obenext_test`, which was still forced afterwards. A framework hot reload removed the force (the flag didn't exist before, so it was gone). (2026-10-31)
- [x] **Overrides and hot reload:** sky fog density 0.05 and a vignette and blur held; after a framework reload the density was the game's 0.0005 again, no vignette overlay was left and the blur was off. (2026-10-31)
- [x] **Screen effects:** a held blur (Beautify blur on, intensity 2.4) stayed through the inventory and the pause menu; after `effect=clear` it was off (1) and the game's own blur came on and off with both again. `effect=black` took the fade canvas to 1 and clear back to 0. A 1 s shake of 0.3 moved the camera up to 0.92 m and it settled at 0. (2026-10-31)
- [x] **Settings:** `brightness=150` set Beautify brightness 1.5 and `sensitivity=2500` the camera's 25 (from 1 and 9); clear put both back; the four options files kept their hashes. (2026-10-31)
- [x] **Save refusal:** during a save in Interior Player Tenement, `GlobalFlags.Set`, `Tanks.Fill`, `Tanks.Empty`, `PowerDevices.SetOn`, `PowerGrids.SetEnabled`, `Lights.SetOn`, `Switches.Set`, `Radiation.SetDose`, `Surfaces.SetCovering`, `ScreenFx.Shake` and `ScreenFx.FadeToBlack` all returned false and nothing changed. (2026-10-31)
- [x] **Hazards:** the Bazaar's fire barrel (`...Bazaar_Corner_Alley/Group#20/barrel_001_open_fire`) switched off did no damage with the player standing in it; switched on it took 16 health and raised one `Entered`. (The fire barrel instead of the mushrooms: the same `TriggerHurt`.) (2026-10-31)
- [x] **Radiation zone:** standing in Open Sewer Mines' dumping tunnel zone (1453 mSv/h), the dose rose 24 in 4 s; switched off it rose 0 (the game's own switch would have kept dosing), switched on 24.6 again. `radiation.info dose=50` and back to 0 worked. (2026-10-31)
- [x] **Thunder:** `weather.conditions thunder=true` and `false` in the Bazaar. (2026-10-31)
- [x] **Shower and tank events:** the Bazaar shower draws from its own 15 L barrel `82815dd1-...`; on for 5 s it drained it in two `TankChanged` batches about two seconds apart (Game) and one after it went off, with on and off `ShowerChanged` (Mod). The empty 200 L barrel `3b40ecfe-...` filled with 20 L of Clean Water. In the apartment a fill, a swap to Diesel and an empty each reported at once with the right before and after; a pour in 139 small steps came as one change, as `LocalPlayer` while the tank's window was filling. Emptying a hose fed from elsewhere is refused. (2026-10-31)
- [x] **Power devices and grids:** the Bazaar camp fire `3979b050-...` on and off with `SwitchedOn`/`SwitchedOff`; a grid in the apartment off and on with two `GridChanged`; a light and a switch through `Lights.SetOn` and `Switches.Set` and back, each true. (2026-10-31)
- [x] **Saunas:** Kolhola's sauna `c1b9b6a2-...` on (event with 16 degrees) and off (24.7); a second throw within 1.5 s refused; at 80 degrees a throw gave 0.4 steam. (2026-10-31)
- [x] **Flood gates:** Interior Tenement B's eight gates start unpowered. Gate 04 refused to open without power; with power it opened (`GateChanged` opening); a power cut on the way stopped it and `moving` went false; power back turned it round and closed it (`GateChanged` closing). (2026-10-31)
- [x] **Lanterns:** the jack-o'-turnip in Interior Mines Potato Mine (`f55a8be1-...`, candle time set to 120): blown out with `Lanterns.Extinguish` and relit, it was still lit 10 s past the old candle's end. Blown out the game's way with 6 s left and relit, it went out 6 s later (the game bug, TODO.md LanternStaleTimer). Lit and Extinguished events with their sources. (2026-10-31)
- [x] **Blast sites:** a linked pair in Open Sewer Mines (`22644e38-...`, `28e02a56-...`): `Detonate` refused with no dynamite and with dynamite in one only; with both it blew both, one `Detonated` each. (2026-10-31)
- [x] **Blackouts:** the casino's "Breakout" trigger: `Started` raised, `IsRunning` true and then false, the screen back to clear. (2026-10-31)
- [x] **Surfaces:** White Wallpaper (19470) on an apartment wall and scraped off, one `SurfaceChanged` each; scraping a bare wall answers true without an event. (2026-10-31)
- [x] **IsAvailable:** all 18 environment, light, switch and grid classes true; `patches.check` found 495 patched methods, none broken. (2026-10-31)
- [ ] Lights with several states: no level has one in 0.5.13; check one on furniture that has them when one turns up.

All of M25b below was checked on 2026-10-31 on copies of a real save (Slot1), going Player Tenement, Kolhola, Mines Barrack Hotel, Kolhola, O Market, Open Sewer Tenement.

- [x] **Renovation [save]:** with Samuel Jonasson's screen open (`TenementContractor.ShowMenu`), apartment 4's Small-Walls-1 was refused without its materials (30 planks, 30 nails, 15 scrap metal, 4 duct tape, 2 tarps); with them it started, took 800 OC and the materials, `Started` (Mod). `PayFinal` refused while it ran; with the timer shortened the game raised `WorkDone` (Game); paid, it took 75 RM, `Finished` and `RenovationFinished` (Mod). Saved and loaded: installed (`currentWall` Makeshift). A press on the game's upgrade screen (`LocalPlayer`) was not tried.
- [x] **Rent screen:** `tenants.screen` refused while the contractor's menu was open and without the Tenement Blueprint task item; with it the game's Tenement page opened for Pate Rantanen (26); a second call was refused while it showed. Its rent button for apartment 4 rented with `Rented` (`LocalPlayer`) and paid the 1507 OC 30 RM deposit to the player.
- [x] **Eviction:** refused while Pate was moving in; `MovedIn` (Game) after the move; notice and cancel from a mod (`EvictionStarted`, `EvictionCancelled`, Mod; repeats refused); the screen's evict button `EvictionStarted` (`LocalPlayer`); `MovedOut` (Game) at the end took the deposit back.
- [x] **Renting [save]:** a quiet `tenants.rent` of Pate into apartment 4 raised `Rented` (Mod) already showing no deposit (before the fix it showed one until the call returned), no money moved; saved and loaded, he lived there with deposit 0.
- [x] **Tenant events:** with the Crazy Neighbor (107, the only tenant with events) moved in, `tenants.event kind=SpawnTrash` started one with source Mod; the others were on the game's cooldowns.
- [x] **Cleaning:** an unknown apartment answers `NoSuchApartment`; apartment 2 started (`Running`, Mod), a second start refused, finished (`Done`, Mod), no start after. Apartment 3's 1-day job ended at the second midnight with `Done` (Game): the game ends cleaning jobs a day late (TODO.md, CleaningDayLate).
- [x] **Hotels [save]:** Kolhola's pods (41 OC a night): the desk's own choice path (`Hotel.RentRoom("2_1")`) rented Pod 8 with `RoomChanged` (`LocalPlayer`); a second rent in that hotel refused and the free pods showed `bookable` false. The stay didn't run out at noon: the hotel's `lastCheck` is the clock's own object after its first check (TODO.md, HotelDayOnce); given a separate yesterday it ran out at once (`RoomChanged` false, Game). Pod 9 for 2 nights from a mod charged the 58 OC `Price` said (`RoomChanged`, Mod) and was still the player's with 2 nights after a level change and back. A bed in the Mines Barrack Hotel rented too (one room per hotel); its rooms and the hotel share Kolhola's ids.
- [x] **Mailboxes:** the apartment's box: two letters taken quietly (no toast; one taken normally showed one), each with `Used` (Mod); the box used as the player reported `LocalPlayer`. Open Sewer Tenement's post box counted 1 with a letter and a mailable package in the inventory (2 before the fix), `Post` sent the letter and left the package, `Used` (Mod).
- [x] **Parcel lockers:** O Market's 38-compartment locker: two parcels claimed into compartments 16 and 24 (`Claimed` 2, Mod), a second claim 0; one taken quietly (no toast), the other normally (one toast), an empty compartment refused, `Taken` with the parcel's title (Mod). The locker used as the player claimed and took with `LocalPlayer`.
- [x] **Save refusal:** during a save `Cleaning.Start`, `Mailboxes.Post`, `Tenants.Rent` and `CheckRent` (Busy), `OpenRentScreen`, `TriggerEvent`, `StartEviction` and `Renovations.Start` all refused and nothing changed.
- [x] **IsAvailable:** `Hotels`, `Renovations`, `Mailboxes`, `ParcelLockers` and `Tenement.HooksAvailable` true; `patches.check` found 509 patched methods, none broken. One NullReferenceException in the game's `InventoryNavigationHandler.HandleTenementMovementInput` when the test script first opened the contractor's Tenement page; it didn't repeat.

## 8. Transport, furniture, assets, computers (M24, M20, M21, M29b)

Already checked:
- **Bazaar:** level exits (140), entrypoints, ladders, a tarp door opened, the radio set to station 2 with its event, locks.
- **Custom furniture:** "Test Chair" defined from the Red Metal Chair and placed.

- [x] **Custom furniture [save]:** (2026-10-30, on a copy of a real save.)
  - `furniture.custom key=test template="Red Metal Chair" title="Test Chair" category="Mod Furniture"`, then `furniture.give id=oben:DevBridge/test`.
  - It shows under a "Mod Furniture" build group. Place it from the build menu, save and load: it's still "Test Chair". (Placed through the build bar, kept its title and id over save and load. The first try found the game's fast save reader collapsing custom pieces to their template, which turned the pieces into plain Red Metal Chairs; fixed.)
  - Disable DevBridge and load: it's a stand-in that looks like the chair. (After a DevBridge reload: a stand-in with the saved title, description and price 123; defined again, it was the real piece after the next load.)
  - With ObenFramework off: a plain Red Metal Chair. (Seen before the reader fix: with nothing linking it by id, the game loads the saved piece as its template.)
- [x] **Known gap:** custom pieces don't show in `furniture.kinds`. Decide whether they should, and fix it. (They do now, marked `custom`, without using the console first; `known=` checks the build menu's list.)
- [x] **Known limitation:** placed furniture gets new ids after a framework hot reload in a session without a save. Check whether ids hold over a hot reload in a real save, where the ledger exists. If not, carry the id map over the reload with `ReloadState`. (2026-10-30: carried over; all 21 ids held, also a chair placed after the load. The check found a second case: a level change cleared the ids before its own save, so pieces placed or moved since the last load got new ids. Fixed; a new and a moved chair kept theirs over two level changes.)
- [x] Furniture shops sell the custom piece; flatpack loot spawns it; blueprints can be given and mailed. (2026-10-30: Ankea's furniture shop offered a piece once on load, 3 at 77 OC, and bought it back; bought out, it didn't come back on the next load. Flatpacks with the piece came from TrashBin rolls, a trash bin, a ground spawner and package contents, which stayed empty without the new package hook. A blueprint was given, another mailed and delivered; one lying in Tenement B stayed in the world when its mod was gone and crafted the stand-in.)
All of M24 below was checked on 2026-10-31 on a copy of a real save (Slot1), going Player Tenement, Kolhola, Open Sewer Tenement, Metro, Mines, Mines Management, Bazaar Bank.

- [x] **Locks:** `locks.set` on one of `locks.list radius=10 lockedOnly=true` locks and unlocks it, and `locks.givekey` lets the player open it. (Kolhola's Changing Room door unlocked, made unpickable at 0.5, locked again with Mod events. Kolhola's level door locked, given Pia's keys with `Locks.SetKey`: `Levels.Use` refused without the key; with it the key unlocked the door after its sound, and the next use started the trip.)
- [x] **Lockpicking:** with the game's own lockpicking running on one door, `Locks.StartLockpick` on another returned false and the game's kept running (before the fix it stopped the player's attempt and the framework stayed busy). After it stopped, a mod's start worked, a second start was refused, and stopping it called back false and freed the framework.
- [x] **Keypads:** `keypads.list` / `keypads.set` where there's a keypad. (Kolhola: four rotary locks with codes like 25,3,1,13 and one keypad. A rotary code 17,3,22,5 was taken, 33 refused; the keypad took 1234 and refused 13. Opening raised Solved, `refuse=true` WrongCode, and locking it through its interact path Locked with source LocalPlayer.)
- [x] **Door hours:** `doors.schedule` changes a door's hours, and they come back on unload. (The game's only schedule, the Bazaar bank's front door, open Monday to Saturday 06-23: at 18:33 hours of 06-17 locked it within seconds; `reset` and a DevBridge reload each brought the level's table back and unlocked it.)
- [x] **Travel:** `levels.use` / `travel.go` changes level through the game's own path; `travel.events` shows Leaving and Arrived. In a real save this saves first, as doors do. (Five trips, each saving into Dev_Test's autosaves; Leaving and Arrived with source Mod. `levels.use` on the mines' Management door reported the exit's id and source Mod; before the fix a door opened by a mod reported Game.)
- [x] **Entrypoints:** `travel.entrypoint name=Dev.Spot` at the player, `travel.teleport` there from elsewhere, then `remove=true`: removed, a second removal false, the teleport refused and the name gone from the list.
- [x] **Open Sewer Mines:** `trains.list segments=true`, `trains.events`, `trains.drive`, `trains.switch`. (A minitrain started through its cab, departed, stopped on the brake; at 5 m/s it coupled to three fuel wagons, and a crash into a buffer at 4.6 m/s gave one Crashed event. Before the fix no crash was ever reported: the game's bounce back moves the train again before its crash step returns. A free switch swung to branch 1 a moment after the call; one with a wagon on its segment refused and `force` threw it at once.)
- [x] **Open Sewer Metro:** `tickets.list price=5` changes the ticket price at the machine. (Both machines 25 to 5, a ×2 factor on one gave 10, reset gave 25. A ticket bought at 5 took 100 to 95 OC, with TicketBought and TicketTaken.)
- [x] **Elevator** (Kolhola's instead of the Bazaar Mine's): `elevators.call` to floor 3 gave Called (Mod), the car moved 0 to 3 and Arrived came about 14 s later; a call to the floor it stands at gives Arrived again (the game's floor-reached step, doors open).
- [x] **Drones** (Open Sewer Tenement; the Bazaar Bakery has none): `drones.list nodes=true` gives each node's range and links; `drones.set target=<node> start=true` switched the drone on and each of the five nodes it passed was reported with its id, in route order.
- [x] **Wait stations:** `waits.list`, `waits.use`. (Interior Mines Management: 13:29 to 18:30 and a Finished event with 18,033 s; a second use during the wait was refused.)
- [x] **Saves:** during a save started from a script, a tarp, lock, keypad, elevator, door and teleport call each returned false and changed nothing.
- [ ] **Train on train:** drive a minitrain fast (over its couplers' speed) into a standing wagon: one Crashed event naming the wagon, not two.
- [x] **Computer apps (M29b)**, 2026-10-31 at the only usable computer in 0.5.13, the Bazaar Bank's laptop (Interior Bazaar Bank, a scan of all 124 levels found no other), screenshots:
  - The old tiles were sized in pixels on a screen measured in metres (84 by 72 m on a 0.39 by 0.20 desktop), so nothing showed. Fixed: icons are copies of the screen's Recycle Bin icon in the desktop's icon grid; two apps showed top left in the game's style, before anyone sat down.
  - A double click (two clicks) opened an app; one click didn't.
  - The kit-window app opened over the screen; a faked Esc closed it and the player stayed seated.
  - The on-screen app opened in a copy of the game's program window with its title, a taskbar entry and the screen's font: header, text, a live game time that ticked, a field, two buttons in a row, a long list clipped at the window's edge (951 units of list in an 803 view; scrolled to the end it showed line 12).
  - With the field focused, a faked E didn't make the player get up; with nothing focused it did, and the window stayed on the screen for the next sitting.
  - The taskbar entry minimised and restored the window; its close button closed it (one `Closed`, the taskbar entry gone); the title changed live; Redraw kept the typed text.
  - Ending the script that registered them took both icons and the open window away. Reloading the level with a window open raised `Closed` once, and the new laptop had the icons.
  - `computer.apps demo=true` added Demo Stocks and Demo Notes; `open=ObenUIKit.DemoNotes` opened it on the laptop in use.
- [ ] **Computer apps by hand:** with a pad, the right stick and A reach the on-screen app's buttons, and B doesn't make the player get up while a kit window is open over the computer (needs a pad; the hold is in `ComputerGates`). Real typing in the Demo Notes field.
- [x] **Computers:** (2026-10-31, the bank's laptop) `computers.list` gave its 4 e-mails with senders and 2 recycle bin files ("Upper Vault Safe" and its code); `SetWallpaper` showed a 16:9 picture under the icons and put the tunnel picture back when its owner ended (a 1 by 64 picture drew as a thin strip: the game keeps a picture's aspect); `Computers.Used` fired once for sitting down and once for getting up.
- [x] **Guitar:** hold one, then `guitar.play song=[0,0.5,0.5,0.25]`. Strums sound, and nearby NPCs comment. (2026-10-30: a strum and the song gave 5 strum events. NPCs only comment during a long note held over 10 s, never on strums; a note held with `guitar.play hold=1` drew a comment from a mod NPC that could see the player once the hold stopped skipping the guitar's update. The long notes loop; the player's own key release ends a mod's note.)
- [x] **TVs:** `tvs.list` / `tvs.set` where there's a TV. (2026-10-30, Interior Player Tenement: the old TV was unpowered and `tvs.set on=true` refused; powered from a script it switched on (static on screen) and off (dark), each with a `Television` event.)
- [x] **Item model:** `items.model` shows a model on an item lying in the world and in the hands. (2026-10-30: a sausage on the floor showed a two-cube OBJ at its size while an apple kept its look; the guitar in hand showed it in first person and still strummed; `clear` gave both their own look back.)
- [x] **Item icon:** `ItemIcons` replaces an item's icon in the inventory (screenshot). (2026-10-30: the sausage showed a mod model's drawn icon and the apple a drawing of its own model.)
- [x] **Icons drawn from models:** (2026-10-30: at the main menu and in a level; about 15 to 25 ms for 256 pixels; fog, ambient light and the game's lights the same before and after.)
  - `items.drawicon item=14 file=<png>` draws the sausage's pickup model with a clear background, close to the game's own icon.
  - `ItemIcons.SetFromModel` with an OBJ model, a registered item without a sprite (drawn from its prefab) and custom furniture with its own model and no icon all get a drawn icon.
  - Reloading the owning mod puts the item's own icon back and removes the rest.
- [x] **Text:** `text.get` reads the game's localization tables. A mod text in another language shows after switching the game's language. (2026-10-30: "Resume Game" read "Jatka peliä" and a mod text "Hei modilta" in Finnish; switching back raised `OnLanguageChanged` with "English (en)".)
- [x] **Assets left from the first round:** (2026-10-30)
  - an OGG and an MP3 through `ModAssets.SoundAsync`; (3.00 s and 3.13 s clips, about 10 ms each)
  - `ModSounds.Loop(..., SoundGroup.Music)` follows the Music slider and stops on Dispose and on mod unload; (a tone on each mixer group measured from the game's output: the Music group follows only Music, every other group Sound; the loop went on Dispose and on a DevBridge reload)
  - a 9-slice sprite on a UI Image; (border 12 kept sharp, sliced next to plain in a screenshot)
  - an OBJ with 500k+ vertices loads without a one-frame hitch. (518,400 vertices in 2.86 s, UInt32 indices; worst frame 27 ms during the load against 35 ms before it)
- [x] **Volume channels:** `sounds.volume channel=Music share=0.3`. (2026-10-30: Music at -10.46 dB; the game rereading its volume file and its options menu setting itself up kept it lowered while the menu read 0 dB; a slider move to -6 saved -6 and played at -16.46; the volume file was byte for byte the same afterwards; a Sound lowering moved OtherSounds too and ended on a DevBridge reload.)
- [x] **See-through MTL:** an OBJ whose MTL has `d 0.35` draws in Fade mode beside an opaque material (screenshot, 2026-10-30).
- [x] **Generated clips:** NaN, infinite and out-of-range samples came back as 0, 0 and clipped, the caller's array untouched, and a clip over `MaxClipSamples` was refused (2026-10-30).

## 9. NPCs and animals (M15)

Already checked:
- **Mod NPC copies:** of an AI body (144) they walk, wander and stop; a copy of Crazy Neighbor (107) stays; a static body (59) refuses orders with a clear reason.
- **Also:** say, kill and the died event; a destroyed mod NPC is noticed; crime state, scene phones, breeding pens.

- [x] **Saved mod NPCs [save]:** (2026-10-30, on a copy of a real save: Saved Sam came back after save and load at his saved place, the file listed him; a DevBridge reload took him away and brought him back; `npcs.despawn` took him out of the save.)
  - `npcs.spawn name=s template=144 saved=true`, then save and load: the NPC comes back, and `<save>/mods/ObenFramework.npcs.json` exists.
  - Reload DevBridge: the NPC disappears and returns.
  - `npcs.despawn name=s` takes it out of the save.
- [x] **Saved NPCs of other scenes:** `npcs.saved` from another level lists a saved NPC with its scene and `here: false`. (2026-10-30: the Bazaar Keeper listed from the player's apartment.)
- [x] **Look:** `npcs.look name=<n> meshes=true`, then a blend shape changes the face (screenshot). (2026-10-30: `_12OpenMouthSmil` at 100 opened the mouth to a grin.)
- [x] **Look on a slider body:** a mod NPC on 146, the only AI body with sliders, wearing a LookData. (2026-10-30: wore the player's look, beanie, glasses and clothes.)
- [x] **Sit and sleep orders:** `npcs.order order=sit` near a chair; `order=sleep` near a bed. (2026-10-30: they stood at the chair and on the bed until the orders got poses; now one sits on the chair and the other lies along the mattress. Despawning the sleeper frees the bed at once, with no error.)
- [x] **Routine:** `NpcRoutines.Set` from a script. The NPC spawns at the step's point, changes step on the minute, and walks out through a door. (2026-10-30: stand, wander, then walked 3 m to the door and was removed when the step moved to the Bazaar.)
- [x] **Claims:** (2026-10-30, on Marko Mainisto in the Bazaar and a mod NPC.)
  - `Hold(Freeze|Talk)` stops the NPC moving and stops you talking to it. (He stayed put on a patrol; a talk gave CannotTalk, and Started once lifted.)
  - `PlayAnimation` blends a clip in and out. (Only after the fix that fades the Animator's own output: before, nothing showed.)
  - `Dispose` restores everything. (Claim gone, activity on again.)
- [x] **Node groups:** `npcs.nodes` lists the Bazaar's 4 patrol routes; `npcs.claim do=patrol` walks one (about 7 m in 5 s), `stopnodes` stops him where he is; `Stand` at a group's standing spot walks there and stands. (2026-10-30.)
- [x] **Mod animation without a claim:** a humanoid clip on the unclaimed Bazaar police lowers the hips, fades in and out and leaves no trace. (2026-10-30.)
- [x] **Extras:** `npcs.extras` with templates 59, 70, 35, 107, 126 and 144: only `Appearance` or `CharacterAppearance` left on them, no errors in the log, all visible with their looks, and a humanoid clip plays on one. (2026-10-30.)
- [x] **Actions:** `npcs.act` follow, unfollow and hit on a game NPC. (2026-10-30: Marko followed, stopped, took the hit.)
- [x] **Animals:** `animals.list`, then `animals.act id=.. action=tame`, `sleepHold`, `wake`, `hunt`. (2026-10-30: on a cat, chicken and rat spawned as items; the rat only hunts when hungry, as in the game.)
- [x] **Taming [save]:** a tamed animal is still tame after save and load. (2026-10-30: a rat of Deekula C, which the game saves; the other rat stayed wild.)
- [x] **Relationships:** `npcs.relationship` in a save that has relationships. (2026-10-30: none of the saves here has one, so VeryGoodFriend was added the way a start choice does; disposition, target and pause came back after save and load.)
- [x] **Phone:** `phone.scene ring=<address> number=112` rings with 112 as the caller; `phone.list check=` says 112 is the game's, 12ab is no number and 5550199 is free. (2026-10-30.)
- [x] **Scene actors:** `npcs.actors nocharacter=true` lists an NPC object without a character with id -1 and a handle that finds it; `InScene` leaves it out. (2026-10-30: Hank's character taken off for the check and put back.)
- [x] **Scene look on a mod NPC:** `scenelook.apply` with `ModNpc.Root` as target. (2026-10-31, fixed and passes: Topias's look on a 144 copy stands whole in his six shown meshes; Jens Brom's Blender-rig look is retargeted onto a 144 copy, stands on the floor and walks with it. The first try on 2026-10-30 showed a giant floating head: the kit's Z-up clothes read in the body's Y-up space.)

## 10. Player (M16)

Already checked: speed and jump multipliers (also over a reload), `clear=true`, a 30-minute "Reading" wait, the Cooking skill banner, seats sit and leave, the wasteland held and stopped, teleport facing and pitch.

- [x] **Survival (M11), 2026-10-30, in a second copy on a test character:** a hunger threshold at 50 fired once going up (45 to 55, and again 49.99 to 50.1 after an apple took it down). A thirst drain multiplier of 0 held thirst at 29 for an in-game hour while hunger rose by its rate of 5. A damage hook halved a hunger hit (10 became 5, `Damaged` said 5) and cancelled a fall (no health lost, no `Damaged`). Consume hooks cancelled a sausage from the world and let an apple through (hunger 55 to 45, `Consumed`, `Source` World). With depression at 100 and a dirty player the game rolled a breakdown: drinking, then self-harm, both cancelled, no bleeding. Tiredness 100 raised `PassedOut`, a 300 hit raised `Died` (Hunger) once. Setting a stat from a `Loading` handler was refused.
  - To repeat it: the loaded slot had `PlayerStats.Active` off (saved during the start menu), so set it on first or no need changes. A breakdown is only rolled while depression is still moving toward its target, and a clean player's hygiene keeps effective depression under 100.
- [x] **Sleeping:** with tiredness at 85, `player.sleep action=sleep seconds=7200` slept to the alarm in about 3 s; `SleepStarted` and `WokeUp` fired, tiredness 68 after (2026-10-30).
- [x] **Clock label:** a 6-hour `PlayerSleep.Wait` with `Label = "Tinkering"` shows "Tinkering: 03:18" under the wait clock (screenshot, 2026-10-30).
- [x] **Custom skill [save]:** passed 2026-10-30 on a copy of a real save.
  - Registered from a script: "New skill Tinkering learned!" and "Tinkering increased to level 2!" banners. Two mod banners queued at once showed 9 s apart.
  - It has a row on the Character page (level 2, 2400 XP; this early save has no game skills yet).
  - 2.4 survived a level change and a load from disk, and `<save>/mods/ObenFramework.customskills.json` holds it.
- [x] **Wardrobe:** `player.wardrobe equip=17730` (Hard Hat with Headlamp from the bag) filled Head, `unequip=Head` emptied it again (2026-10-30). It showed the game's head lamp bug (HeadLampPrompt in TODO.md).
- [x] **Notes:** `player.notes give=Assets/Content/Notes/Bazaar/Bazaar_Restaurant_Note_001.asset` gave "Scatola Di Maccheroni - Help Wanted!", listed and readable on the Notes page (screenshot, 2026-10-30).
- [x] **Weapons:** with a pickaxe on a rock in Open Sewer Mines (a hit of 50, swung from a script by setting the pickaxe's `hitEnabled`), Mining x3 gave 150 (2026-10-30). The rounding choice is checked in its own commit.
- [x] **Custom interaction:** on a cube from a script, "Test box (Not yet)" in red while its reason held, then "Test box (Poke)" (screenshots, 2026-10-30).
- [x] **Look across a door [save]:** `PlayerLook.Keep` with another hat and height 0.1 kept them over a level change from the mines to Kolhola and over a load from disk (2026-10-30).
- [x] **God mode [save]:** held by Game Tweaks, the save wrote `"Godmode":false`; with Game Tweaks unloaded the save loaded without god mode (2026-10-30).
- [x] **Camera lock, slider preview, wearable lists (2026-10-30):** `player.lookat` turned the view onto a point in first person and snapped in third person, gave way to a talk and came back after it, and the view stayed on release. `player.look preview=` showed slider changes on the body at once; `options=hat` listed the 9 hats.
- [x] **Medical and conditions:** passed 2026-10-30 as far as the game allows.
  - The doctor is in Interior Tenement Deekula A (a `Doctor` at about 62.65, -96.74, 20.53; Kolhola's "A3 Doctor" is only a room). With the menu opened by the game's `Doctor.ShowMenu()`, `Medical.Treat(Antidepressants)` bought it for 30 RM through the panel ("Item added!") and `Treated` fired with the kind and price.
  - Conditions: 0.5.13 ships no condition assets at all (none even after `Resources.LoadAll`), so `player.conditions` lists nothing and refuses a made-up name, as it should.

## 11. Items and crafting (M17, M19)

Already checked: inventory events from Mod (also right after a framework reload), stacks, stations list, crafting speed factor, recipe search, loot pools, resource nodes, spawners, breakables. Everything in this area passes now.

- [x] **Inventory events:** passed 2026-10-30 on a copy of a real save, with the storage window open.
  - A drag inside the inventory (`ItemData.OnBeginDrag`, `Slot.OnDrop`, `OnEndDrag` from a script, like the C key) raised nothing.
  - Quick-moving a sausage into a chest raised ItemRemoved (LocalPlayer, 1, total 2); dragging it back raised ItemAdded (LocalPlayer).
  - Eating one from the inventory like the Use button (`item.InvokeAction(itemData, FirstAction)`) raised ItemUsed and ItemRemoved (Game); hunger fell by the game's 20.
  - `inventory.equip id=10710` (pickaxe) raised HeldChanged into the primary hand, and `unequip=primary` back to -1.
- [x] **Crafting at a stove with extra sources:** passed 2026-10-30 on a copy of a real save (Slot1), at the player's Table Stove with grilled turnip (one turnip). The stove was switched off and its grid had no capacity in that save, so `Toggle(true)` and `power.grid capacity=3000` first.
  - With turnips only in the chest and the cupboard next to it, `HasIngredients` was false; with `crafting.sources name=a container=<chest> priority=0` and `name=b container=<cupboard> priority=5` it was true, and the stove's window listed grilled turnip as craftable with nothing in the stove.
  - `crafting.control action=start` took the turnip from the cupboard (priority 5): CraftStarted, then SourceConsumed from DevBridge for the cupboard. After `crafting.sources name=a priority=10` (changed in place) the next craft took it from the chest.
  - Slot filters in place: both sources limited to paper (`item=10060`) made `HasIngredients` false, the cupboard limited to turnips (`item=12`) made it true again, `item=-1` lifted them.
  - With the window open, the stove's own `Craft` (its button) took the turnip from the chest. With both containers emptied behind the open window, `Craft` started nothing and the window refreshed: grilled turnip grey, boiled turnip gone.
- [x] **Quality:** passed 2026-10-30. With the game's random craft quality held at 0.6 by a script (`PlayerStats.SkillQuality`), a grilled turnip came out at 0.600, and at 0.900 with `crafting.quality key=* factor=1.5`. Two earlier crafts at Cooking 1 burnt to ash, the recipe's own fail output.
- [x] **Crafting speed:** passed 2026-10-30. The same craft took 8.09 s from CraftStarted to Crafted, and 2.08 s with `crafting.speed key=* speed=4`.
- [x] **Recipe filter:** passed 2026-10-30 in the stove's window: `recipes.filter text=Boiled` left only boiled turnip of the two, `SetSelectedRecipe("0")` (how the pad selects) picked boiled turnip, not grilled turnip (index 0 of the stove's own list), and lifting the filter brought both back.
- [x] **Station control:** passed 2026-10-30 at a fermenting bucket in the Potato Mine with Speedy Sugar Mash (Fast Kilju): start went to Processing, pause held the timer (4146 s left over 4 s), resume ran it on, stop ruined the batch (Crafted, failed, Ruined Mash).
- [x] **Upgrades:** passed 2026-10-30: Spices placed next to the Table Stove was listed as connected to it, the stove's equipment quality went from 0.5 to 0.8, and `CraftingUpgrades.SetOn` off and on took it to 0.5 and back. The upgrade pieces are Distillation Kit, Sample Analyzer, Spices and Sterilizer.
- [x] **Machines:** passed 2026-10-30.
  - Plants: a mushroom plant aged to 100% and harvested raised PlantHarvested (3 bags of green mushrooms). A tomato planted with `PlantActions.Plant` in a free can, grown, and one ripe tomato picked with `GrowItem.GetItem` (as the player does) raised PlantPicked. `GrowingStations.SetPaused` and `SetRotting` reached the planted plant.
  - Dishwasher (placed with `furniture.place kind=Dishwasher`): it washes the owner off stolen items with washing powder and power. A pint owned by character 37 started with `crafting.control action=start` (at speed 50), came out with no owner, and raised CraftStarted, Crafted ("Pint") and DishwasherFinished.
  - Recycling (O Market; Tenement B's two machines are disabled test props): 3 glass and 2 wine bottles put in through the panel raised Recycled (3 for 3, 2 for 4) and the panel showed 7. `Recycling.SetOwed` to 12 and Get coins paid 12 OC and left 0.
  - Autofarm (Potato Mine, the farmer's 28-spot field): two plants harvested, then at 07:00 on a replant day Replanted came with 2 planted, not initial. `SetGrowPercent` took 20-60 and refused 70-20. The game's replant days are a day late (DateWeekdayShift in TODO.md).
- [x] **Unloaded storage [save]:** passed 2026-10-30. From Kolhola, `storage.unloaded scene="Interior Player Tenement"` listed the tenement's container GUIDs. Two turnips queued into an empty chest and one wine bottle out of a beer crate stayed queued over a save and a load from disk; walking into the tenement raised UnloadedApplied for both and the chest held 2 turnips, the crate 1 bottle instead of 2. After a save and another load nothing applied twice.
- [x] **Inventory sources [save]:** passed 2026-10-30 with a chest registered from a script.
  - Renovation (the 5,215 OC one: 10 cables and more): with 4 cables in the inventory and 10 in the chest, `TenementController.CheckForItem` said no without the source and yes with it (14 counted). `TakeRequiredItemsAndMoney` took the inventory's 4 first and 6 from the chest, plus the rest and the money.
  - Blueprint (Wooden Table, given with `furniture.blueprints give=`, at a Work table placed with `furniture.place`): with 1 plank in the inventory and 6 in the chest, `PlayerHasResources` was false without the source and true with it. The workshop window showed "3/4" planks after the craft, counting the chest. Crafting took the inventory's plank first and 3 from the chest, the rest from the inventory (the glue lost one use), and the bench started.
  - The table needs Carpentry 2. The first craft tripped the game's auto-eat bug (AutoConsumeLastItem in TODO.md); keep hunger under 50 when repeating it.
- [x] **Performance:** with the inventory and a chest open, a script counting `InventoryWatch.Check` saw 0 recounts in about 940 frames, then exactly 1 after giving two apples (2026-10-30).
- [x] **Consumable values [save]:** passed 2026-10-30 on a copy of a real save: a per-save sausage value fed 45 instead of 20, survived a level change under a session value, left with its mod and came back with it, was gone at the main menu and back after loading the save.

## 12. World props, scene logic, landmarks, signs (M22)

Already checked: SceneLook list and reskin on Helena (Jens Brom's look), props cube and furniture chair, relays and states, landmarks list, objects near, visited scenes.

- [x] **Props [save]:** (2026-10-31, on a copy of a real save)
  - `props.spawn id=test kind=cube scale=0.5` and `props.spawn id=chair kind=furniture source="Red Metal Chair"`.
  - Change level and come back: both are there. (None in Tenement B, all four back in the player's tenement.)
  - Save and load: `<save>/mods/ObenFramework.props.json` exists and both props come back. (All four live after the load.)
  - Rebuild DevBridge: they vanish and return. (A framework rebuild reloaded DevBridge; all four came back.)
  - `props.remove all=true`. (Removed 4.)
- [x] **Use prompt:** (2026-10-31: "Poke me (Use)" on the cube and the interact path ran the handler with "btn"; a light prop got its prompt once props without a collider got a box on the walk-through usable layer, as the game's physics queries skip triggers; in third person both showed in the game's interaction list and the selected one ran.)
  - `oben eval 'ObenFramework.World.Props.WorldProps.OnUse(ObenLoader.Mods.ModRegistry.Find("DevBridge").Instance, "poke", u => UnityEngine.Debug.Log("poked " + u.PropId)); return "ok";'`
  - then `props.spawn id=btn kind=cube interact=poke title="Poke me"`.
  - Looking at it shows the prompt, and the interact key logs "poked btn". Also try in third person (Game Tweaks).
- [x] **Landmarks [save]:** `landmarks.mark id=test title="Test"` puts a red dot on the map. It comes back after save and reload, and `Globals.tnmt` never contains `DevBridge:test`. (2026-10-31, screenshots; the map needs the Tenement Map task item, and a mark on the player's own spot hides under the game's player pin.)
- [x] **Signs:** `signs.list` and `signs.set` where there's an editable sign; TextChanged fires. (2026-10-31: no level has one; a Small Label from the build menu's Signs group is one. Set to "Mod was here", TextChanged with ByPlayer false.)
- [x] **Scene logic:** `scene.set` on something harmless (a counter or state), and `scene.fire` on a relay. (2026-10-31: a relay fired with its Fired event, the tenement's branch set and back, a relay's outputs off and on.)
- [x] **SceneLook retarget:** `scenelook.apply ... mode=apply` on a different rig, e.g. the player's model. Also: `scenelooks status` and `scenelooks here` in F1. (2026-10-31: Topias's look on the player's mesh model, posed with the body; the scripts object above it isn't the one to target. `scenelooks status`, `here` and the new `checks` work, and so does "Run scene looks checks" on the MODS menu's Settings page.)

## 13. Saves, ObenSave and the first real save

- [x] A first real save with every mod on: ModSaves files appear under `<save>/mods/`, and nothing errors while saving or loading. (2026-10-29: 17 files in Slot1, Slot2, Autosave and Autosave2, no errors from our code over the saves and door changes.)
- [ ] **Mod data round trip [save]:** the nine steps under "How to test it in the game" in `docs/modding/framework/saves.md` (save, door, main menu, load, cross-slot save, delete, hot reload, corrupt file). Also: a data class with a list field without an initializer has an empty list after a load, and setting `Value = null` throws. (`Peek` with the fill was checked on Slot2 on 2026-10-30: members missing from the bank ledger file came back empty, the saved values intact.)
- [ ] **Save state:** `save.capture` and SaveState apply on a real object; `save.slots` lists characters and slots; "save now" works.
- [ ] **ObenSave (off by default):**
  - Switch it on, save, then load: the compressed container is written and read back.
  - `oben save` inspect, export, import and convert on that save.
  - Switch it off again: the game's own format loads.
- [ ] **Bank interest [save]:** over a week, the BankInterest fix and the bank ledger agree (interest shows as its own ledger entry).

## 14. Performance Fixes and Unofficial ObenPatches

- [ ] Each Performance Fixes feature on and off, with frame time and allocations compared (see `docs/modding/performance-fixes.md`).
- [ ] **ReflectionProbeRefresh** (off by default): check it in a level before turning it on.
- [ ] **NpcSightChance** (off by default): an NPC's sight check before and after.
- [ ] **StrayLightControllers:** load Open Sewer Tenement: Player.log has no `LightController.Awake` NullReferenceException (before the fix: one per load). The fix itself passed with a stray controller added in Interior Tenement Kolhola (2026-10-30).
- [x] **ItemGiverRefillTime:** leave Interior Tenement Kolhola: Player.log has no "InteractableItemGiver is null!" (before the fix: one per cup dispenser). (2026-10-30: walking in, one clock subscription per dispenser instead of two; leaving at 00:29 and four more level loads, no error.)
- [x] **ScreenshotFolder:** a faked F11 and F12 a few seconds apart save two pictures in `Screenshots/` next to the exe, the F12 one without the HUD. (2026-10-31; with the fix off: "Failed to store screen shot".)
- [ ] **BankExchangeCoins:** still to write, see TODO.md. Changing RM into coins at a low-coin bank pays 0 until it's fixed.

## 15. Parity additions (2026-10-29)

Small APIs added after the parity audit. Most are one eval each; run them in a level (`new_scene` is enough unless marked).

- [ ] **Game keys:** `oben eval 'return ObenFramework.Ui.GameKeys.Key("Interact") + " " + ObenFramework.Ui.GameKeys.Label("Interact");'` gives `E E`. With `input.capture action=Jump seconds=10`, `GameKeys.Held("Jump")` stays false while Space is held.
- [x] **Console (M29):** passed 2026-10-31 by script. `IsAvailable` true; `Open()` opened the F1 console at the main menu (screenshot) and in a level, where the game's key lock had one holder while open and none after `Close()`. `help` listed every mod command with its mod; `Execute("features")` returned what it printed. Names with a space, a quote or nothing were refused. Two `Close()` calls in one frame both answered true (the game closes a frame later); fixed, the second answers false.
- [x] **Tooltip:** `Hud.ShowTooltip("Test tooltip", "Some <b>details</b> here")` showed the game's tooltip box at the crosshair; `HideTooltip()` removed it. (2026-10-31)
- [ ] **Notes [save]:** with a note held, `PlayerNotes.Open(<id from player.notes>)` opens the menu's notes page at it; for a book, at the page it was left on.
- [x] **Keypad:** `Keypads.Refuse` on a keypad from `keypads.list` plays its wrong-code reaction and `WrongCode` fires. (2026-10-31, Kolhola: `keypads.set refuse=true`.)
- [ ] **Power devices:** `PowerDevices.Describe(<a stove>).FuelItemIds()` lists wood and other fuels; `BurnTimeFactor` is 1 for most.
- [ ] **Sounds:** `GameSounds.PlayPickup("Bottle")`, `PlayLibrary("harvest")` and `PlayLibraryAt("scarySounds", <position>)` are heard; `SoundscapeIds()` lists the level's soundscapes and `SetSoundscape(id)` / `ResetSoundscape()` switch the ambience.
- [ ] **Economy:** `Wallet.RmToOc(1)` gives 10; `Prices.OfSale(<trader>, <item>, out _)` is lower than `Prices.Of` for the same item (base sell price 0.9 against 1.2).
- [ ] **Mail sent [save]:** send a quest delivery or pay an invoice by mail; `PlayerMail.Sent` fires with the receiver and item.
- [ ] **Police [save]:** with something on the record, `Law.BribePrice` is a price; `Law.TryBribe()` either clears the record and takes the coins, or fails once and `CanBribe` turns false. `Law.Confiscate(<a container>)` moves the inventory into it and destroys stolen items.
- [ ] **Contests:** during a rat fight, `Contests.Participants(Contests.Active()[0])` lists the rats; after a contest ends, `Contests.LastResult` is set.
- [ ] **NPCs:** `Npcs.Meshes(<id>)` and, on a slider body (146), `Npcs.LookOf(<id>)` read without a claim; `NpcActions.StopActivity(<id>)` ends what an NPC is doing; `Characters.All()` shows `LastName` and `Relationship`; `Janitors.Objects()` lists the janitor objects of the scene.
- [ ] **Missing items [save]:** see area 1 (`registry.missing`, `registry.removemissing` needs `mod` or `all=true`).
- [ ] **Recipes:** a recipe from `RecipeBuilder.From(Recipes.ById(10016), id, "Test")` with one input and output shows at the template's station with the template's icon.
- [ ] **Containers:** in the player's tenement, `Containers.IsPlayerOwned(<a placed chest>)` is true; in a rented apartment `IsTenantOwned` is true. `Containers.TakeFromSlot` takes from the right slot.
- [ ] **Looks:** `PlayerLook.ApplyToPlayer(PlayerLook.Random())` gives a new random look (screenshot); `Random(male: false)` gives a female body.
- [ ] **Day length:** `TimeControl.DayLengthMinutes` is 96 in the tenement. (The `ModSaves.Peek` half of this item passed on Slot2 on 2026-10-30, see area 13.)
- [ ] **Conversations:** `Conversations.StartRaw(title, conversant, actor)` with two NPC transforms runs a conversation between them.

## 16. Bug reports (2026-10-30)

Checked on Linux through Proton already: the Report page (screenshots), `report` and `report all` in the F1 console (34 ms on the main thread, the zip on a worker), `report folder` (Wine's file browser opens on ObenLoader/reports), `oben report --all --json`, the last crash and the save in both, no user name, Steam id or address left in any text file, system.json with Wine 11.0 and the Linux kernel. Still open:

- [ ] **Windows:** a report on real Windows says Windows with no Wine line, `report folder` opens Explorer, and the user name in `C:\Users\<name>` is hidden everywhere.
- [ ] **Report on GitHub:** once the templates are on GitHub, the button opens the bug form with the ObenseuerExtended version, game version, system and the ticked PC parts filled in.
- [ ] **BepInEx:** with BepInEx 5 and a plugin or two in a test copy of the game, the summary lists them with GUID, version and hash, and the form's BepInEx field is filled.
- [ ] **A real crash:** after the game crashes, the next report has its Crash_ folder (crash.dmp and Player.log) and the "Last crash" line.
- [ ] **Too big:** with a big save and long logs, a zip over 25 MB says so on the page and in the console.

