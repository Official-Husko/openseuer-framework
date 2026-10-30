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
- [ ] **Missing items, quick check:** `oben eval "new ItemReference(1234567).ID"` gives `1234567`; it gave `0` before. `oben call registry.placeholders` lists it under `active`.
- [ ] **Missing items [save], full check:**
  1. `oben run scripts/examples/missing-item.cs`.
  2. `mods.reload id=ScriptRunner`.
  3. Save and load. The inventory shows 2× "Missing item (ScriptRunner: Demo Crate)", and `<save>/mods/ObenFramework.items.json` lists it.
  4. A shop refuses it. Dropping it and picking it up again keeps the id.
  5. Running the script again turns the stack back into Demo Crate in place.
  6. `registry.missing` lists both stacks with their place; `registry.removemissing mod=ScriptRunner` deletes them (without `mod` or `all=true` it refuses).

## 2. UI kit, HUD, main menu, UI state (M05, M07, M26, M29)

Older key-press checks still open from the first UI work:

- [ ] MODS on the main menu opens the mod menu with mouse, keyboard and controller.
- [ ] Esc over the pause menu's MODS page goes back one level, not out of the game.
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
- [ ] **HUD tab:**
  - "Prompt over Interact" shows "Demo prompt" with the E key box under the crosshair; E gives a toast and removes it, otherwise it goes after 10 s;
  - the crosshair and HUD toggles hide and restore those pieces.
- [ ] **Prompt:** `oben call hud.prompt text="Open the crate" action=Interact seconds=15` shows in the game's tutorial panel, and the listing says `inGameTutorialPanel: true`. `hud.prompt hide=true` removes it.
- [ ] **Guide:** `oben call hud.guide` runs its steps in order:
  - "Jump" (Space);
  - "Open your map" (M; it waits until the map is closed);
  - "Walk a few steps";
  - "That's the demo guide" for 3 s, then a toast.
  - `hud.guide stop=true` stops it.
- [ ] **Input capture:** `oben call input.capture action=Jump seconds=10` stops jumping for 10 s, and `ui.state` shows `jumpCaptured: true`.
- [ ] **Main menu buttons:**
  - `oben call menu.buttons add="Demo A"`, then `add="Demo B"`.
  - Both sit under ???/MODS and Credits in the menu font. Check `insideParent` and a screenshot of the sign: are they clipped?
  - Down from Credits moves to Demo B, and Up goes back.
  - `menu.buttons remove=true` removes them.
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

- [ ] **Co-op in the menus, pause menu buttons, mod version:**
  - The main menu has Co-op under the grid; the pause menu has Co-op above MODS. Both open the Co-op window (status "Not in a co-op game.", the console commands). Keyboard and pad reach both buttons.
  - The pause menu shows "ObenseuerExtended v0.1.0" just above "V. 0.5.13", same size and colour.
  - Hot reload of ObenCoop: its buttons go and come back; the kit's reload keeps MODS and the version line.

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
- [ ] **Watches over the game, F9:** press the real F9 key in a level; the box comes and goes. A mod's `DevMenu.AddWatch` shows in it too.
- [x] **Log part:** passed 2026-10-30. A Unity warning with `<b>bold</b>` in it shows the tags as text in amber, an error shows red with "+47 lines", Follow keeps the newest line in view, and picking a line shows all of it below.
- [ ] **Log part by hand:** change Show, From and the filter, and click a row with the mouse; "Copy it" puts the line on the clipboard.
- [x] **NPCs part:** passed 2026-10-30 in the tenement: Malone (37) and Seamus (125, hidden, greyed and listed after Malone). Go to it puts the player 1.5 m in front of Malone, facing him; Bring it here says Malone has no path finding. A spawned copy of character 84 ("Testy", CheatMenu/dev1) is listed in both lists; Bring it here, Follow me, Stop following and Look at me are done, Stop its activity says it isn't doing anything, and Despawn removes it.
- [x] **Player and world, quests and console additions:** passed 2026-10-30 through the menu's buttons: Lockpicking set to 7.5; coordinates 97.5, -89.9, 23,5 (comma) take the player there and "abc" gets a sentence; Where I am refills X; searching 14 with How many 3 gives 3 sausages; Made by DevBridge lists only `DevBridge.test`, its line says "Now on: Open crates (0/5)", typing 3 in the counter field gives 3/5 (the game's "Objective Updated" shows); `devmenu` opens and closes the menu and `devmenu watches` hides the watches. In the Cheats window, 13 and 47 on the Hour and Minute sliders give 13:47.
- [ ] **Items of removed mods [save]:** with a save holding a mod's items and that mod removed, the Player and world part lists the stacks, and removing them (asks first) clears them from the inventory and the open containers.
- [ ] **NPCs part [save]:** the relationships (pick one, set the disposition, the target follows), and Kill it (asks first; the NPC stays dead in the save).
- [ ] **Developer menu [save]:** any change made from the menu marks the save in `mods/CheatMenu.dev.json`.
- [ ] **Reopened task:** finish `DevBridge.test`, then set it Active from the dev menu. `oben eval "TaskController.instance.TaskFinished(\"DevBridge.test\") + \" \" + TaskController.instance.TaskActive(\"DevBridge.test\")"` gives `False True` (fixed: both were true).
- [ ] **Third person in a tight spot:** in third person, walk into a real first-person camera trigger. The camera goes in, a "has to stay in first person" toast shows once, and the camera goes back out after the trigger.
- [ ] **Tracer:**
  - `trace.start filter=Quest,GlobalFlags`: `patched` about 58, `refused` 0.
  - Use the dev menu's quest and flag parts; `trace.list` shows records with `from: "CheatMenu"`.
  - `trace.stop`, then `trace.start` with no filter: `patched` about 1618. Note `patchMs`.
  - `trace.stop` says `active: false`, and the earlier records are still readable.
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

- [ ] `oben call story.queue` in a loaded save shows `session` SaveLoad, `ready` true and nothing pending. Going through a door doesn't change that.
- [ ] **Mod quest save [save]:**
  - Create the test quest (see `docs/modding/framework/quest-authoring.md`), then save and reload.
  - `quests.list task=DevBridge.test` has unchanged texts and states, and `<save>/mods/ObenFramework.quests.json` exists.
  - `modquest list` in F1 lists it.
  - Then disable DevBridge and load again: the quest stays frozen in the log and the save isn't broken.
- [ ] **Quest notification:** a New Task notification appears on create, and the task shows in the task panel (screenshot).
- [ ] **Added response:** `dialogue.respond conversation="Bazaar/Trader/Jens Brom" anchorId=1 playerText="Seen any ducks?" npcReply="No."`, and the same for anchorIds 4, 5 and 6.
  - Talk to Jens in the Bazaar: the choice shows, and picking it logs "Dialogue response picked".
  - Hot reload DevBridge: the choice is gone.
- [ ] **Branch:** `dialogue.branch conversation="Bazaar/Trader/Jens Brom" playerLine="Any work?" 'npcLines=["Maybe.","Later."]'` shows after greeting 1. Then `dialogue.narrate text="Test"`.
- [ ] **Interception:** `oben run scripts/examples/story-intercept.cs`, then commit a crime. It goes on record at half severity, and conversation starts are logged.
- [ ] **Difficulty:** `difficulty.set factor=NeedsSpeed value=0.5` makes needs drain at half speed (`player.stats` over a minute).
- [ ] **New Game screen:**
  - At the main menu, run `newgame.testoptions` and `difficulty.presets add=harsh name=Harsh needs=1.5`.
  - In New Game, "Harsh" is in the difficulty selector, and a "Mod options" button sits above Back. Screenshot where it lands, and check pad navigation.
- [ ] **New game start:** start that game. `newgame.last` shows the options, `start.choices` lists the backstory picks, and `tutorial.state` steps through the opening tutorial with events for each prompt.

## 6. Economy and gambling (M23, M27)

Already checked in Open Sewer Bazaar:
- **Stalburg Bank:** withdraw, refused deposit, transfer, quote, the ledger, invoices paid from the account, freeze and unfreeze.
- **Traders:** the list, Brom's hours, a ×2 price factor and back.
- **Also:** junk mail senders, the casino machine and table lists, `gambling.payout clear`.

- [ ] `bank.ledger` keeps its entries after a framework hot reload.
- [ ] While an account is frozen, `legal.list` shows UnfreezeAccount_Stalburg Bank with `canTrigger` true.
- [ ] `traders.factor "shop=Brom's" buy=2` also doubles the prices in the trade window itself (screenshot), and `buy=1` resets them.
- [ ] **Interior Tenement Kolhola**, "Money exchange": `bank.exchange "bank=Money exchange" amount=100 to=rm` takes 100 OC and gives 6 RM. `amount=1` gives TooSmall.
- [ ] **Open Sewer Mines:** `fuel.stations` lists the station; `fuel.price station=<id> price=10` sets it; filling up and paying records FuelBought.
- [ ] **Interior Bazaar Casino:**
  - With `gambling.payout game=slotmachine multiplier=2`, a slot win shows doubled in `gambling.events` and in the wallet.
  - The pajatso tray text shows the scaled win.
  - With `game=chinahand multiplier=2`, first place pays 4× the stake.
- [ ] **Interior Bazaar Ratfighting Arena:** RatBet, ContestStarted, ContestFinished (scaled winOc), then RatWinnings.
- [ ] **Interior Bazaar Bar:** the drinking contest raises ContestStarted and ContestFinished, and `contests.list` shows the opponent and tolerance mid-contest.
- [ ] **Interior Tenement O Market:** `markets.list`, then buy something; CheckoutPaid fires.
- [ ] **Player shop [save], in Interior Player Tenement:**
  1. `furniture.zones placed=true`.
  2. `playershops.create id=test`, then `playershops.set id=test area=<zone> open=true`.
  3. Put an item in a container there.
  4. `playershops.stock`, then `playershops.sell` fills the till, and `playershops.collect` moves it to the wallet.
  5. Save and reload: the shop and the ledger are still there.

## 7. Environment and housing (M25, M25b)

Already checked:
- **Environment:** flags force and release; the FOV override (options file untouched); sky override, read-back and clear; the vignette; power device and grid on/off; heavy rain; radiation and water readings; the hazard list.
- **Housing:** apartments; tenants; `tenants.check` AlreadyRenting; renovations list; the post box.

- [ ] **Flags [save]:** a forced flag never lands in the save. Force `obenext_test`, save, then check `Globals.tnmt`. It's also gone after a framework hot reload.
- [ ] Sky and vignette overrides are reverted by a framework hot reload.
- [ ] **Screen effects:** each one reverts with `effect=clear`.
  - `screenfx effect=blur amount=0.6`: open and close the inventory and the pause menu, and the game's own blur comes back after clear.
  - `effect=black`.
  - `effect=shake seconds=1 amount=0.3`.
- [ ] **Settings:** `settings.override brightness=` and `sensitivity=` behave like the FOV override.
- [ ] **Hazards:** `hazards.set id='Open Sewer Bazaar|TriggerHurt||Stuff/_Slum/Slumshack4/Green Mushrooms (Low Potency)/Mushroom Hurt' enabled=false`. Walking into those mushrooms no longer hurts.
- [ ] **Radiation and thunder:** `radiation.info dose=50`; `weather.conditions thunder=true`.
- [ ] **Shower:** `fixtures.set kind=shower id=565cfbbf-0c3f-40f2-8263-07135236d0a1 on=true` (Bazaar).
- [ ] **Tank:** `fixtures.set kind=tank id=3b40ecfe-505e-492e-95db-6e47a8e3e51b item=10840 amount=20`, an empty 200 L barrel in the Bazaar; 10840 is Clean Water.
- [ ] **Other scenes:** list first, then act on an id from the list.

  | Feature | Command | Likely scenes |
  | --- | --- | --- |
  | Saunas | `fixtures.list kind=sauna` | Interior Bazaar Sauna, Interior Tenement A |
  | Hotels [save] | `hotels.list`, `hotels.rent` | Interior Tenement Kolhola, Interior Mines Barrack Hotel |
  | Flood gates | `fixtures.list kind=floodgate` | Interior Tenement B, Open Sewer Mines, Open Sewer Tenement |
  | Parcel lockers | `lockers.list` (send a parcel first: `mail.send kind=Parcel`) | Interior Tenement O Market, Interior Bazaar J Kiosk, Interior Bazaar Mallment |
  | Lanterns | `fixtures.list kind=lantern` | Interior Player Tenement |
  | Blast sites | `blasts.list`, `blasts.detonate` | Open Sewer Mines |
  | Blackouts | — | Interior Bazaar Casino, Interior Bazaar Mentti B |

- [ ] **Eviction:** on a tenant the game allows evicting (not Malone in 7, he has `disableEvictButton`): `housing.evict apartment=<n>`, then `cancel=true`.
- [ ] **Renting [save]:** `tenants.rent` puts a tenant in a free apartment, and they move in after the hours given. The deposit isn't recorded when renting quietly.
- [ ] **Renovation [save]:** starting one while talking to a contractor works.

## 8. Transport, furniture, assets, computers (M24, M20, M21, M29b)

Already checked:
- **Bazaar:** level exits (140), entrypoints, ladders, a tarp door opened, the radio set to station 2 with its event, locks.
- **Custom furniture:** "Test Chair" defined from the Red Metal Chair and placed.

- [ ] **Custom furniture [save]:**
  - `furniture.custom key=test template="Red Metal Chair" title="Test Chair" category="Mod Furniture"`, then `furniture.give id=oben:DevBridge/test`.
  - It shows under a "Mod Furniture" build group. Place it from the build menu, save and load: it's still "Test Chair".
  - Disable DevBridge and load: it's a stand-in that looks like the chair.
  - With ObenFramework off: a plain Red Metal Chair.
- [ ] **Known gap:** custom pieces don't show in `furniture.kinds`. Decide whether they should, and fix it.
- [ ] **Known limitation:** placed furniture gets new ids after a framework hot reload in a session without a save. Check whether ids hold over a hot reload in a real save, where the ledger exists. If not, carry the id map over the reload with `ReloadState`.
- [ ] Furniture shops sell the custom piece; flatpack loot spawns it; blueprints can be given and mailed.
- [ ] **Locks:** `locks.set` on one of `locks.list radius=10 lockedOnly=true` locks and unlocks it, and `locks.givekey` lets the player open it.
- [ ] **Keypads:** `keypads.list` / `keypads.set` where there's a keypad.
- [ ] **Door hours:** `doors.schedule` changes a door's hours, and they come back on unload.
- [ ] **Travel:** `levels.use` / `travel.go` changes level through the game's own path; `travel.events` shows Leaving and Arrived. In a real save this saves first, as doors do.
- [ ] **Open Sewer Mines:** `trains.list segments=true`, `trains.events`, `trains.drive`, `trains.switch`.
- [ ] **Open Sewer Metro:** `tickets.list price=5` changes the ticket price at the machine.
- [ ] **Interior Bazaar Mine:** `elevators.list`, `elevators.call`.
- [ ] **Interior Bazaar Bakery:** `drones.list`, `drones.set`.
- [ ] **Wait stations:** `waits.list`, `waits.use`.
- [ ] **Computer apps, Interior Bazaar Internet Cafe:**
  - Register a test app from a script with `ComputerApps.Register`, then sit at a computer.
  - The tile shows on the desktop, and clicking it opens the app window.
  - Esc closes the window and the player stays seated.
  - `ComputerApps.Open(id)` opens it standalone.
- [ ] **Computers:** e-mails, recycle bin files, desktop picture swap and back on unload.
- [ ] **Guitar:** hold one, then `guitar.play song=[0,0.5,0.5,0.25]`. Strums sound, and nearby NPCs comment.
- [ ] **TVs:** `tvs.list` / `tvs.set` where there's a TV.
- [ ] **Item model:** `items.model` shows a model on an item lying in the world and in the hands.
- [ ] **Item icon:** `ItemIcons` replaces an item's icon in the inventory (screenshot).
- [ ] **Text:** `text.get` reads the game's localization tables. A mod text in another language shows after switching the game's language.
- [ ] **Assets left from the first round:**
  - an OGG and an MP3 through `ModAssets.SoundAsync`;
  - `ModSounds.Loop(..., SoundGroup.Music)` follows the Music slider and stops on Dispose and on mod unload;
  - a 9-slice sprite on a UI Image;
  - an OBJ with 500k+ vertices loads without a one-frame hitch.

## 9. NPCs and animals (M15)

Already checked:
- **Mod NPC copies:** of an AI body (144) they walk, wander and stop; a copy of Crazy Neighbor (107) stays; a static body (59) refuses orders with a clear reason.
- **Also:** say, kill and the died event; a destroyed mod NPC is noticed; crime state, scene phones, breeding pens.

- [ ] **Saved mod NPCs [save]:**
  - `npcs.spawn name=s template=144 saved=true`, then save and load: the NPC comes back, and `<save>/mods/ObenFramework.npcs.json` exists.
  - Reload DevBridge: the NPC disappears and returns.
  - `npcs.despawn name=s` takes it out of the save.
- [ ] **Look:** `npcs.look name=<n> meshes=true`, then `npcs.look name=<n> mesh=male_head shape=_02UpperLidRaise weight=80` changes the face (screenshot).
- [ ] **Look on a slider body:** a mod NPC on 146, the only AI body with sliders, wearing a LookData.
- [ ] **Sit and sleep orders:** `npcs.order order=sit` near a chair; `order=sleep` near a bed.
- [ ] **Routine:** `NpcRoutines.Set` from a script. The NPC spawns at the step's point, changes step on the minute, and walks out through a door.
- [ ] **Claims:**
  - `Hold(Freeze|Talk)` stops the NPC moving and stops you talking to it.
  - `PlayAnimation` blends a clip in and out.
  - `Dispose` restores everything.
- [ ] **Actions:** `npcs.act` follow, unfollow and hit on a game NPC.
- [ ] **Animals:** in a scene with animals, `animals.list`, then `animals.act id=.. action=tame`. Also `sleepHold`, then `wake`, then `hunt`.
- [ ] **Taming [save]:** a tamed animal is still tame after save and load (the game itself never re-applies the flag).
- [ ] **Relationships:** `npcs.relationship` in a save that has relationships.
- [ ] **Phone:** `phone.scene ring=<address> number=112`.
- [ ] **Scene look on a mod NPC:** `scenelook.apply` with `ModNpc.Root` as target.

## 10. Player (M16)

Already checked: speed and jump multipliers (also over a reload), `clear=true`, a 30-minute "Reading" wait, the Cooking skill banner, seats sit and leave, the wasteland held and stopped, teleport facing and pitch.

- [x] **Survival (M11), 2026-10-30, in a second copy on a test character:** a hunger threshold at 50 fired once going up (45 to 55, and again 49.99 to 50.1 after an apple took it down). A thirst drain multiplier of 0 held thirst at 29 for an in-game hour while hunger rose by its rate of 5. A damage hook halved a hunger hit (10 became 5, `Damaged` said 5) and cancelled a fall (no health lost, no `Damaged`). Consume hooks cancelled a sausage from the world and let an apple through (hunger 55 to 45, `Consumed`, `Source` World). With depression at 100 and a dirty player the game rolled a breakdown: drinking, then self-harm, both cancelled, no bleeding. Tiredness 100 raised `PassedOut`, a 300 hit raised `Died` (Hunger) once. Setting a stat from a `Loading` handler was refused.
  - To repeat it: the loaded slot had `PlayerStats.Active` off (saved during the start menu), so set it on first or no need changes. A breakdown is only rolled while depression is still moving toward its target, and a clean player's hygiene keeps effective depression under 100.
- [ ] **Sleeping:** `player.sleep action=sleep` when tired; WokeUp fires.
- [ ] **Clock label:** the wait clock shows the mod's label, e.g. "Reading" (screenshot during a wait).
- [ ] **Custom skill [save]:** register one from a script.
  - Its level-up banner shows; mod and game banners take turns.
  - It has a row on the character page (C).
  - The value survives save and load.
- [ ] **Wardrobe:** `player.wardrobe`: equip a hat from the bag.
- [ ] **Notes:** `player.notes give=<Assets/... path>`.
- [ ] **Weapons:** `player.weapons target=Mining multiplier=3` with a pickaxe on rock gives triple damage.
- [ ] **Custom interaction:** `CustomInteractions.Add` from a script. The title and verb show on the crosshair, and a refusal reason works.
- [ ] **Look across a door [save]:** `PlayerLook.Keep` keeps the look over a door or level change.
- [ ] **God mode [save]:** `HoldGodmode`, save, remove the mod, load. The save is not stuck in god mode.
- [ ] **Medical and conditions:** a treatment at the doctor raises Treated; conditions list and change.

## 11. Items and crafting (M17, M19)

Already checked: inventory events from Mod (also right after a framework reload), stacks, stations list, crafting speed factor, recipe search, loot pools, resource nodes, spawners, breakables.

- [ ] **Inventory events:**
  - Dragging a stack inside the inventory raises nothing.
  - Moving a stack to a chest raises ItemRemoved from LocalPlayer.
  - Eating raises ItemRemoved and ItemUsed.
  - `inventory.equip id=<weapon>` raises HeldChanged.
- [ ] **Crafting speed:** `crafting.speed key=* speed=4`, then open a stove: the shown time is a quarter. Craft once; `crafting.events` shows CraftStarted and Crafted.
- [ ] **Crafting sources:**
  - Run as a script: `CraftingSources.Register(s, st => Containers.All().Where(c => Vector3.Distance(c.transform.position, st.Position) < 4f))`.
  - At a stove next to a chest with ingredients, the recipes become craftable.
  - Crafting takes from the chest, and `crafting.events` shows SourceConsumed.
- [ ] **Recipe filter:** `RecipeFilter.Add(scope, c => c.Name.Contains("Soup"))` lists only soups, and controller selection still picks the right recipe.
- [ ] **Quality:** `crafting.quality` changes the output quality of a craft.
- [ ] **Unloaded storage [save], in another scene:**
  - `storage.unloaded scene="Interior Player Tenement"` lists container GUIDs.
  - `storage.queue ... item=12 amount=2`, save, then walk into the tenement: `worlditems.events` shows UnloadedApplied.
  - Save and reload: the change doesn't apply twice.
- [ ] **Inventory sources [save]:** an extra container counts for blueprints and renovations; the inventory is used first.
- [ ] **Machines:** recycling, dishwasher, growing station and autofarm events in a save that has them.
- [ ] **Performance:** with the inventory window open, no recount work happens unless items change (profiler or frame time).

## 12. World props, scene logic, landmarks, signs (M22)

Already checked: SceneLook list and reskin on Helena (Jens Brom's look), props cube and furniture chair, relays and states, landmarks list, objects near, visited scenes.

- [ ] **Props [save]:**
  - `props.spawn id=test kind=cube scale=0.5` and `props.spawn id=chair kind=furniture source="Red Metal Chair"`.
  - Change level and come back: both are there.
  - Save and load: `<save>/mods/ObenFramework.props.json` exists and both props come back.
  - Rebuild DevBridge: they vanish and return.
  - `props.remove all=true`.
- [ ] **Use prompt:**
  - `oben eval 'ObenFramework.World.Props.WorldProps.OnUse(ObenLoader.Mods.ModRegistry.Find("DevBridge").Instance, "poke", u => UnityEngine.Debug.Log("poked " + u.PropId)); return "ok";'`
  - then `props.spawn id=btn kind=cube interact=poke title="Poke me"`.
  - Looking at it shows the prompt, and the interact key logs "poked btn". Also try in third person (Game Tweaks).
- [ ] **Landmarks [save]:** `landmarks.mark id=test title="Test"` puts a red dot on the map. It comes back after save and reload, and `Globals.tnmt` never contains `DevBridge:test`.
- [ ] **Signs:** `signs.list` and `signs.set` where there's an editable sign; TextChanged fires.
- [ ] **Scene logic:** `scene.set` on something harmless (a counter or state), and `scene.fire` on a relay.
- [ ] **SceneLook retarget:** `scenelook.apply ... mode=apply` on a different rig, e.g. the player's model. Also: `scenelooks status` and `scenelooks here` in F1.

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
- [ ] **BankExchangeCoins:** still to write, see TODO.md. Changing RM into coins at a low-coin bank pays 0 until it's fixed.

## 15. Parity additions (2026-10-29)

Small APIs added after the parity audit. Most are one eval each; run them in a level (`new_scene` is enough unless marked).

- [ ] **Game keys:** `oben eval 'return ObenFramework.Ui.GameKeys.Key("Interact") + " " + ObenFramework.Ui.GameKeys.Label("Interact");'` gives `E E`. With `input.capture action=Jump seconds=10`, `GameKeys.Held("Jump")` stays false while Space is held.
- [ ] **Console:** `oben eval 'return ObenFramework.Console.GameConsole.Open();'` opens the F1 console (screenshot); `Close()` shuts it a frame later and the player can move again.
- [ ] **Tooltip:** `oben eval 'return ObenUIKit.Hud.ShowTooltip("Test", "Some <b>details</b>");'` shows the game's tooltip box (screenshot); `HideTooltip()` removes it.
- [ ] **Notes [save]:** with a note held, `PlayerNotes.Open(<id from player.notes>)` opens the menu's notes page at it; for a book, at the page it was left on.
- [ ] **Keypad:** `Keypads.Refuse` on a keypad from `keypads.list` plays its wrong-code reaction and `WrongCode` fires.
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

