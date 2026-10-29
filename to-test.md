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
- [ ] **Time freeze:** `oben call time.freeze`, then `clock.get` twice 10 s apart gives the same time. `time.freeze on=false` lets the clock run again.
- [ ] **Time speed:** `oben call time.speed speed=10` gives about 25 in-game minutes per 10 real seconds. `time.speed clear=true` puts it back.
- [ ] **Freeze and sleep [save]:** freeze the clock, then sleep in a bed. Time passes while sleeping (`time.control` shows `suspended: true`) and is frozen again afterwards.
- [ ] **`clock.after`:** `oben call clock.after minutes=2` logs "clock.after: 2 in-game minutes passed" about 8 s later.
- [ ] **`clock.after` while sleeping [save]:** note `clock.get`, run `clock.after minutes=60`, then sleep. The log line comes about one in-game hour after the noted time, not two or three (fixed: it counted minute ticks, and a sleeping frame covers several minutes).
- [ ] **Load aborted:**
  - `oben eval 'SaveController.ChangeLevel("", "")'`. A few seconds later the log says "The level change the game announced didn't happen ... (ready: True)".
  - `oben eval "ObenFramework.Game.WorldState.IsReady"` is true.
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
- [x] **ThirdPerson:** passed 2026-10-29, running the F6 hotkey's action from a script:
  - F6 moves the camera 2.5 m behind the player, and `firstperson` is False.
  - F6 again, or switching the tweak off in third person, gives first person with both locks True.
  - A zoom blocker shows "The camera has to stay in first person here." once.
  - Scroll to zoom on lifts the locks; off puts them back.
- [ ] **ThirdPerson by hand:** press the real F6 key, and walk into a wall in third person.
- [ ] **ThirdPerson [save]:** save while in third person. The save holds `disableThirdPerson:true` and `firstperson:true`.
- [x] **CameraView (framework):** passed 2026-10-29 in the tenement through `player.camera`:
  - `mode=third distance=3` unlocks both locks and goes to third person (`firstPerson` False a frame later).
  - A zoom blocker (`DisableZoom`) and a `PlayerCameraTrigger.FirstPersonOnly(true)` each pull the camera in with `heldBack` True; when they let go, it goes back out to 3 m.
  - `mode=first` gives first person with both locks True again.
  - `body=true` keeps `playerRig` active in first person; `body=false` hides it.
  - `ModeChanged` fired False, then True.
  - While `OnSavingGame` serialises the camera, it reads first person, locked, saved distance 0; right after, it is back in third person. Still to do: a real save.
- [ ] **Developer menu:** switch on `CheatMenu/DevMenu`, press F8, and check each part:
  - **Quests:** `DevBridge.test` shows the rewind buttons; `Abandoned2` only offers state changes.
  - **Flags:** filter and switch flags.
  - **Watches:** add TimeOfDayAzure / `timeSpeedOverrideFactor`; `clock.set speed=2` shows 2 within a quarter of a second.
  - **Session and story:** the values read live.
  - **Player and world:** stats, entrypoints, item search.
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

1. [ ] Copy 1: `oben call console.exec command="coop lobby create Test"` prints "Hosting for up to 4 players." and "Lobby open: Test. Join code XXXX-XXXX-XXXX (192.168.x.x:27890)". `coop.status` shows lobby Hosting with that code.
2. [ ] Copy 2: `console.exec command="coop lobby list"`, wait 2 s, then `oben call coop.lobbies --instance 2`. results[0] is "Test", verdict Ok, 1 player.
3. [ ] Copy 2: `coop lobby join 1`. `coop.status` on both shows 2 players, lobby Hosting / Joined. `coop say hello` arrives on the other copy.
4. [ ] Copy 1: `coop invite` prints the code. Copy 2: `coop leave`, then `coop join <code>` joins again.
5. [ ] **Presence:** load a level in both copies (`new_scene Interior Player Tenement`) and wait 10 s.
   - `coop.presence --instance 2` shows remotes[0] in "Interior Player Tenement".
   - Teleport copy 1 by 2 m: copy 2's `latest` follows within about 1 s.
   - A teleport of more than 8 m sets the Teleported flag.
   - `coop presence off` on copy 1 empties copy 2's remotes at once; `on` brings it back.
6. [ ] **Hot reload while connected:** rebuild ObenCoop while connected.
   - Both copies keep 2 players and the lobby.
   - The log says "Carried the session over a reload" and "Carried the lobby over a reload".
   - `coop lobby list` from copy 2 still finds 2/4.
   - With presence on in both, rebuild ObenCoop on copy 1 only: copy 2's `coop.presence` keeps copy 1 in its remotes, with no 5 s gap (fixed: restarted sample numbers were dropped as late).
7. [ ] Copy 2: `coop leave`, then `coop join 127.0.0.1:27999`. A failure notice appears within 10 s, and `coop.status` lobby.error is set.
8. [ ] `coop status` says "Steam: ready, signed in as ..." with no errors. A Steam join between two copies doesn't count here, because they share the emulator's id; that needs two real Steam accounts.
9. [ ] Blob transfer: send a big payload (over 1 MB) from host to guest through a script. It arrives intact and flood control doesn't drop the connection.

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

- [ ] A first real save with every mod on: ModSaves files appear under `<save>/mods/`, and nothing errors while saving or loading.
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
- [ ] **Mod data [save]:** `ModSaves.Peek<JObject>("ObenFramework", "items")` reads the placeholder list of the loaded save; `TimeControl.DayLengthMinutes` is 96 in the tenement.
- [ ] **Conversations:** `Conversations.StartRaw(title, conversant, actor)` with two NPC transforms runs a conversation between them.
