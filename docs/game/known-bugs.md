# Known game bugs

Bugs in the game itself, found while reading the code or playing. Each fixed one is a feature in Unofficial ObenPatches (`mods/ObenPatches`), switchable with `features ObenPatches/<id> on|off` in the F1 console or `features.set` over the bridge. Line numbers are for game 0.5.13 in `decompiled/Assembly-CSharp/`.

## Fixed

### MissingSavesFolder: an error on the main menu before the first save

`SaveController.GetCharacters()` (`SaveController.cs:1236`) calls `Directory.GetDirectories(persistentDataPath + "/Saves/")` without checking that the folder exists. On a fresh install it doesn't until the first save, so every visit to the main menu logs a `DirectoryNotFoundException` from `LoadMenu.GenerateCharacterList`. The menu catches it and carries on, so it's only noise in Player.log, but it hides real errors.

Fix: a prefix creates the empty `Saves` folder first, which the first save would do anyway. Tested on 0.5.13: with the fix off, `SaveController.GetCharacters()` throws "Could not find a part of the path ...\Saves"; with it on it returns an empty list.

### RobbersSeePolice: NPCs check their own head when looking for other NPCs

`NPC_FOV.CheckIfNPCsAreDetected` (`NPC/NPC_FOV.cs:108`) loops over every NPC but reads `npcInfo.GetComponent<NPC_FOV>()`, the NPC's own info, instead of the loop item's. Every iteration tests the same head position (its own), so the result is all NPCs in the level or none. The only user is `Act_Robber`, which subscribes to `NPCsDetected` to react when police see it (`NPC/Act_Robber.cs:173`).

Fix: a prefix with the same logic using each NPC's own head, skipping the NPC itself. Tested that the patch applies and comes off with the switch; the robber behaviour itself still needs a robbery in progress to watch.

### ReflectionProbeRefresh: a tag typo stops reflections from refreshing (off by default)

`RefreshCubemapInTrigger` (`RefreshCubemapInTrigger.cs:21` and `:29`) compares the collider's tag with `"Player)"`, with a stray parenthesis, so `update` never becomes true and the reflection probe never re-renders while the player is inside its trigger.

Fixed literally, the probe would render every frame while the player stands there, which is expensive. The fix renders it when the player walks in and then four times a second. It's off by default until the component has been seen in a level and the result looked at.

### EmailMonthNames: August, September and October are shifted on emails

`TimeAndDay.GetMonthNameShort` (`TimeAndDay.cs:175`, the switch at `:186-188`) maps month 8 to "Sep", 9 to "Oct" and 10 to "Sep". The only users are the mail program's list and reader (`ComputerEmails.cs:77`, `ComputerEmail.cs:19`), which print `{month} {day}` for each email, so mail from those three months carries the wrong month name. Checked in the running game: `1=Jan ... 7=Jul 8=Sep 9=Oct 10=Sep 11=Nov 12=Dec`.

Fix: a postfix returns "Aug", "Sep" and "Oct" for months 8, 9 and 10. Check: with the fix on, `new TimeAndDay { month = 9 }.GetMonthNameShort()` is "Sep" (off: "Oct"), or look at an email dated in one of those months on the computer.

### RandomPickSkipsLast: random picks never choose the last entry, and a patrol can hang

Five methods pick a random index with `Random.Range(0, n - 1)`. The int overload already leaves out its upper bound, so the last entry can't be picked:

- `Patrol.OnUpdate` (`BehaviorDesigner.../AstarPathfindingProject/Patrol.cs:60`) and `PatrolWithAnimations.OnUpdate` (`:78`): with random patrol on, the next waypoint is drawn in a `for (num = waypointIndex; num == waypointIndex; num = Random.Range(0, Count - 1))` loop. The last waypoint is never visited, and with two waypoints an NPC at the first one rolls 0 forever, an infinite loop that freezes the game. The game's other Patrol task (`BehaviorDesigner.Runtime.Tasks.Movement/Patrol.cs:64`) uses plain `Count`, which shows the intent.
- `FindWithTag.OnUpdate` (`BehaviorDesigner.../UnityGameObject/FindWithTag.cs:24`): the "random" option never returns the last object found.
- `Storage.RemoveRandom` (`Storage.cs:1063` and `:1067`): the "remove random items" effect never touches the last slot.
- `Fishspawner.DeltaSeconds` (`Fishspawner.cs:70`): the last fish prefab never spawns.

Fix: a transpiler on those five methods turns the `ldc.i4.1; sub` before `Random.Range(int, int)` into `- 0`. It logs and leaves a method alone if the pattern isn't there. Checked against the IL in the running game: one match in each of the first four, two in `RemoveRandom`, and `Fishspawner.DeltaSeconds` has three `Range` calls of which exactly one is the `- 1` one. Not seen in play: whether an NPC in a shipped level has a two-waypoint random patrol. Check: the patch applies without a warning in the log; to see the hang, put an NPC with a two-waypoint random A* patrol on its first waypoint with the fix off.

### AmbientSoundVariety: random ambient sounds ignore their pitch range and last clip

`SoundscapeArea.PlayRandomSoundsCoroutine` (`SoundscapeArea.cs:218`) sets `float pitchMax = sound.randomPitchMin;` (it should be `randomPitchMax`), so `Random.Range(pitchMin, pitchMax)` always returns the low end and a sound set up with a pitch range never varies. The clip choice at `:226` and `:231` is `Random.Range(0, audioClips.Length - 1)`, so the last clip never plays.

Fix: a transpiler on the coroutine's `MoveNext` reads the pitch from `randomPitchMax` and drops the `- 1`. Checked against the IL in the running game (both patterns present). Whether any shipped sound has a different max pitch, so whether anyone hears the change, is unknown: the sound data is serialized in scenes. Check: stand in an area with random ambient sounds and watch the clips and pitch; with a debug build of the coroutine you'd see `audioSource.pitch` vary.

### DialogueLuaFunctions: the pocket bank conversation never knows you have an account

`Bank.RegisterDialogue` (`Bank.cs:637`) registers the Lua function `CheckIfPlayerHasAccount` with `typeof(Bank).GetMethod("CheckIfPlayerHasAccount")`, but the method is `PlayerHasAnAccount`, so the lookup returns null. `LuaMethodFunction.InvokeMethod` returns nil for a null method, so the function silently answers nil. The Penni-Pena conversation sets `Variable["PocketBankHasAccount"] = CheckIfPlayerHasAccount();` in its first entry (found in the dialogue database in the running game), so it treats every player as having no account. `RelationshipController.Register` (`RelationshipController.cs:243`) has the same slip: it looks `RelationshipIdActive` up on `PlayerSubscriptionsController` instead of itself. No shipped dialogue calls that one, so it only matters to other callers.

Fix: postfixes on both register methods register the functions again against the right methods, only on the call that did the registering. Check: talk to Penni-Pena (Bazaar, Mentti) after opening an account and see that it no longer offers to open one; or, with the console, `Lua.RunRaw("return CheckIfPlayerHasAccount()")` once the bank has registered.

### DialogueRealMoney: dialogue money functions drop the decimals of RM

`Money.AddMoneyDialogue` and `RemoveMoneyDialogue` (`Money.cs:416`, `:468`) cast the double amount to int before calling `AddMoney`/`RemoveMoney`, but the notification prints the real amount with two decimals. The dialogue event `RemoveMoneyRM(float amount)` (`DialogueEventSystem.cs:33`) takes a float on purpose. So 2.5 RM would announce "2.50 RM" and move 2. RM is stored in tenths (`rMoneyAmount`), so decimals are real.

None of the shipped dialogue entries I searched (the whole dialogue database in the running game) uses a fractional amount, so this fixes nothing visible in the base game today; it matters for the float event and for mods that call the functions. Fix: a prefix handles RM amounts with decimals itself (same notification text, then the real amount); whole amounts and OC take the game's own path. Check: with the fix on, call `Money.instance.RemoveMoneyDialogue(1.5, true)` and see the balance drop by 1.5 RM.

### NpcSightChance: NPC sight ignores its detection chance (off by default)

`NPC_FOV.CheckIfTargetIsDetected` (`NPC/NPC_FOV.cs:169`) rolls `(float)Random.Range(0, 1) < Mathf.Lerp(detectionPropabilityFocus, detectionPropabilityEdge, t)`. The int overload's upper bound is left out, so the roll is always 0 and any target inside the field of view with a clear line is detected every time, however low the chance is. The one NPC checked in the running game has focus 1 and edge 0.15, a field of view of 90 degrees and a 0.5 s check cooldown, so the intent is clearly a falling chance towards the edge.

Fix: a transpiler replaces the roll with `Random.value`. Off by default because it makes NPCs notice you less near the edge of their view, a balance change the game never had. Check: with it on, stand near the edge of a guard's view cone and see that you're not spotted every half second.

### BankInterest: bank interest is never paid or charged

`Money.CurrentTime` (`Money.cs:632-653`) raises each account's savings by `Mathf.Pow(1 + Interest, num / 7)` and its debt by `Mathf.Pow(1 + DebtInterest, num / 7)`, where `num` is the number of days since the last check. The check runs every day, so `num` is 1, and `num / 7` is an integer division: the exponent is 0 and neither the 1.5% savings interest nor the 3% debt interest ever applies (unless seven days pass between two checks). The `/ 7` shows the intent: interest per week.

Fix: a prefix applies the same formulas once per whole week since interest was last applied, and keeps that day in the save (`mods/ObenPatches.bank-interest.json` next to it). It then moves the game's last-check day to today, so the game's own (always zero) step skips itself and everything after it, like loan payments, runs as before. Counting starts the day the fix first runs, so nothing is charged for time that already passed. On by default: savings grow and debts grow, as the banks advertise. Tested on 0.5.13 by driving `CurrentTime` day by day with a test account: with the fix, day 7 brings 1000 OC to 1014 and a 1000 OC debt to 1029 (the game truncates coins); without it nothing ever changes.

### RenovationMaterialsOnce: renovations take their materials from every resource storage

`TenementController.RemoveRequiredItems` (`TenementController.cs:1052`) runs `tenementResourceStorage.RemoveItem(id, out remaining, amount)` for each tenement resource storage, always asking for the full `amount` instead of what is still missing, then removes `remaining` (what the last storage couldn't give) from the inventory. With one storage, or none, that is right. With two or more, every storage that has the material gives the full amount, and the inventory pays again whenever the last storage is short. Resource storages are furniture with a `TenementResourceStorage` component (`TenementResourceStorage.cs`), which registers itself on `Start`, so building a second one is enough. `TenementUpgrade.TakeRequiredItemsAndMoney` (`TenementUpgrade.cs:178`) is the only caller.

Fix: a prefix (last, after the framework's extra inventory sources have lowered the amount) walks the storages with the amount still missing and takes only the rest from the inventory. With fewer than two storages it leaves the game's code to run. Ported from the reference framework's renovation tweak (`TakeRemainingOnly`). Check: build two resource storages, put 10 planks in each, order a renovation needing 10 planks: without the fix both storages end at 0, with it one keeps its 10. The tenement level loaded with `new_scene` had no resource storage, so this hasn't been watched in game yet.

### StrayLightControllers: lamps without a light log an error when their level loads

`LightController.Awake` (`LightController.cs:77-78`) takes `GetComponent<Light>()` and reads its `shadowResolution` on the next line. Three objects in 0.5.13 have the script on a lamp's mesh with no `Light` on the same object (found by scanning every level file for a `LightController` without a `Light`):

- Open Sewer Tenement: `_Environment/Lights/Tenement Bg Timed lights/Timed_light_001`. There is no light at all, and its four copies next to it have no controller, so the script is a leftover.
- Interior Bazaar Pontikka Roof: `Scene/_Stuff/GameObject/Bazaar_Back_Alleys/exterior_light_002`. Its child "Volumetric Spot Light (1)" has the light and a controller of its own.
- Interior Bazaar Mallment: `Stuff/_Karaoke/room4/paper_ceiling_lamp_001_gradient`. Its child "Point Light" has the light and two controllers of its own; the `GradientLamp` beside the stray one colours the light directly.

So every load of those levels logs a `NullReferenceException` from `LightController.Awake` (seen twice in Open Sewer Tenement on 2026-10-29). Nothing else goes wrong: Unity switches off a behaviour whose `Awake` throws, so `Start` never adds it to the light lists and its `Update`, which reads the light without a null check, never runs. Pointing it at the child's light would be wrong, since both children already have a controller.

Fix: a prefix switches a controller with no `Light` next to it off before `Awake` runs, the same end state without the error. Tested on 0.5.13 with a controller added to an empty object in Interior Tenement Kolhola: with the fix off, one `NullReferenceException` in Player.log; with it on, none. Either way the controller ends switched off and outside `LightsController.controllersAll`. A controller with a light is untouched (on, distances set as before). Check in the level: Open Sewer Tenement loads without a `LightController.Awake` error.

### ItemGiverRefillTime: refilling item givers count down twice as fast after a load

An item giver that refills over time (`spawnSettings.respawnEnabled`, e.g. the cup dispensers in Interior Tenement Kolhola) subscribes `DeltaSeconds` to the static `TimeOfDayAzure.SecondsPassed` from `InitCoroutine`, which `Start` begins (`InteractableItemGiver.cs:192-219`). `OnLoadingGame` (`:148-150`) calls `Start()` itself, and Unity calls it too; `Start` checks a `startDone` flag (`:125`, `:194`) that is never set, so a giver in a level loaded from a save (every door) runs the coroutine twice and is subscribed twice. `SpawnSettings.TimeToSpawn` (`SpawnSettings.cs:133`) subtracts the passed seconds on every call, so its refill timer runs at double speed (givers on an every-day schedule are not affected). `OnDestroy` (`:537`) takes one copy off; the other stays on the static event after the level unloads, and on the next tick `DeltaSeconds` finds itself destroyed, logs "InteractableItemGiver is null!" and unsubscribes (`:511-515`). Seen on 2026-10-29/30: one error per cup dispenser each time the player left Kolhola, and two subscriptions per dispenser while there.

Fix: a transpiler on `InitCoroutine`'s state machine replaces the subscription with one that takes the same handler off first, so a giver is subscribed once however often its setup runs. Setting `startDone` instead would also skip the second `Start` where it is the only one that starts the coroutine (a giver loaded while inactive), and that giver would never refill. Tested on 0.5.13: after walking into Kolhola with the fix on, each dispenser had one subscription (two before) and no destroyed giver was left on the clock.

### DateWeekdayShift: weekday schedules run a day late

`TimeAndDay.GetWeekday` (`TimeAndDay.cs:203`) takes `AzureTimeController.GetDayOfWeek(year, month, day)`, which is .NET's `DayOfWeek` (Sunday is 0), and maps 0 to `Weekday.Monday`, 1 to Tuesday and so on, so every weekday it reports is a day late: 16 November 2026, a Monday, comes out as Tuesday. The clock's own `weekDay` (`TimeOfDayAzure.GetDayOfWeek`, `TimeOfDayAzure.cs:547`) subtracts one first and is right; doors, rent day and the framework's `GameClock` use that. Only two places use `GetWeekday`: `Autofarm.CurrentTime` for its replant days (`Autofarm.cs:100`) and `DateRelay` for weekly intervals (`DateRelay.cs:94`).

A scan of all 124 level files found 11 `DateRelay`s, none of them set to a weekday (daily refreshes, the bus schedule and market bells run on day or hour intervals), and these autofarms with weekday replants, each running a day after its list: the Potato Mine's three (Monday, Wednesday and Friday at 07:00), Tenement A's two factory mushroom farms (Monday to Friday at 07:00), Kolhola's aquarium (Monday at 12:00) and the Fungi Boutique's two growing cabinets (Monday at 10:00). The autofarm's own tooltip says the day runs from 0 to 6, Monday first, so the listed days are the intended ones. Found 2026-10-30 when the Potato Mine farmer's Monday replant ran on Tuesday the 17th.

Fix: a postfix moves the answer back one day (Monday becomes Sunday). Tested on 0.5.13 in the Potato Mine with every replant logged: with the fix on, the three autofarms replanted at 07:00 on Monday the 16th and Wednesday the 25th and not on Tuesday the 24th; with it off, nothing on Monday the 23rd and all three on Tuesday the 24th. `new TimeAndDay { day = 16, month = 11, year = 2026 }.GetWeekday()` is Monday with the fix, Tuesday without.

### ScreenshotFolder: F11 and F12 never save a screenshot

`TakeScreenshot.getFilename` (`TakeScreenshot.cs:37-50`) creates `Application.dataPath + "/../Screenshots"`, the folder next to the exe, and looks for a free number there, but returns the relative name `Screenshots/<scene>_<date>_<n>.png`. `ScreenCapture.CaptureScreenshot` resolves a relative name inside `Obenseuer_Data` in a player build, where there is no Screenshots folder, so every shot ends in "Failed to store screen shot" while the game logs "Saved screenshot as: Screenshots/..." (seen 2026-10-29).

Fix: a postfix on `getFilename` returns the full path of the folder the game created. Tested on 0.5.13: a faked F11 and F12 a few seconds apart wrote `Screenshots/Interior Player Tenement_10_31_2026_2.png` and `_3.png` next to the exe, the F11 one with the HUD and the F12 one without. Two shots within a few frames still get the same number (the game checks for the file before Unity has written the first one), as before.

### ScreenshotHiddenHud: a screenshot without the HUD leaves the hide-HUD key needing two presses

The no-UI screenshot (F12) runs `ScreenshotNoUICO` (`TakeScreenshot.cs:22-35`): it notes whether the HUD is showing, adds `GameController`'s object to `GameUIController.hiddenUI` (`HideGameUI`, a plain list, `GameUIController.cs:160-164`), takes the shot, and removes the object again only when the HUD was showing before. The hide-HUD key toggles the same object (`GameController.cs:106-108`, `ToggleGameUIVisibility(base.gameObject)`). With the HUD already hidden by the key, the list ends up holding the object twice, and one press of the key takes out only one: the HUD stays hidden until the second press. With the HUD hidden by anything else (a mod, a cutscene), the screenshot's entry was never taken out at all.

Fix: when the HUD is already hidden, `ScreenshotNoUI` takes an ordinary screenshot instead (there is nothing to hide). Tested on 0.5.13: HUD hidden with the key's object, then F12: with the fix off the list held the object twice and one show left the HUD hidden; with it on, once, and one show brought it back.

### HeadLampPrompt: the "Head Lamp" prompt stays after taking off the hat

Putting on a hat with a head lamp (Hard Hat with Headlamp, 17730) calls the lamp's `Flashlight.onEquip`, which shows a "Head Lamp [B]" prompt until the lamp is first switched on (`Tutorial.ShowHeadLightTutorial`, `Tutorial.cs:462-471`); `onUnequip` hides it again (`Flashlight.cs:605-610`). But the head slot's `CharacterSlotController.UpdateItem` (`CharacterSlotController.cs:138-146`), which runs when the hat is taken off or swapped, from the inventory screen or `Inventory.Unequip`, destroys the lamp's object without calling `onUnequip`, so the prompt stayed on screen until a lamp was switched on somewhere (seen 2026-10-30).

Fix: a prefix on `UpdateItem` for the head slot hides the head lamp prompt (clears `HeadLightTutorial` and the prompt, as `onUnequip` does, without touching other prompts' flags). A lamp put on again shows it again. Tested on 0.5.13 with the prompt's "done" flag cleared: hat on, prompt shown; hat off without switching the lamp on: with the fix off the prompt was still up, with it on it was gone.

### AutoConsumeLastItem: auto-eat eats the last item twice and throws

The workbench's auto-eat (`PlayerAutoConsume.ConsumeNeeds`, run by `ShopBaseUI.Craft` and every crafting second) eats up to five items per need while the need is still at or over its threshold (`ConsumeFor`, `PlayerAutoConsume.cs:121-135`), each time picking the best candidate from `Inventory.GetInventoryItemData`. When an item empties a stack, `Slot.UpdateSlot` (`Slot.cs:205-212`) only queues the slot's `ItemData` for destruction at the end of the frame, and that object keeps its own copy of the stack (amount 1), so `GetCandidate` (`:192-196`, which checks `IsInSlot`, the copy's amount) picked it again in the same frame: its food was applied a second time, then `SlotController.TakeItem` threw a `NullReferenceException` on the emptied stack, which also broke the Craft press (seen 2026-10-30 at a Work table).

Fix: a prefix on `GetCandidate` skips an item whose slot's own stack is empty. Tested on 0.5.13 with the inventory open, two apples at hunger 75: with the fix off hunger went to 45 (three apples' worth) and `TakeItem` threw; with it on, 55 and no error.

### LanternStaleTimer: a relit lantern goes out at the old candle's end

Lighting a candle lantern starts a `TimeOfDayAzure.Timer` with the candle's burn time (`Lantern.CandleInserted`, `Lantern.cs:119-139`) whose callback is `CandleExtinguished`. Blowing it out (`Interact`, `:86-90`) calls `CandleExtinguished` (`:141-147`), which drops `candleTimer` without `Destroy()`ing it, so the timer stays on the clock. When it runs out it calls `CandleExtinguished` again: a candle lit in the meantime goes out at the old candle's end time, and an empty lantern plays the burn-out sound and fires its burnt-out relay (found 2026-10-31 in Interior Mines Potato Mine).

Fix: a prefix on `CandleExtinguished` destroys a timer that still has time left (one that ran out is calling it itself and is taken off the clock right after). Tested on 0.5.13 in the Potato Mine with 120 s candles, blown out the game's way with 90 s left and relit at once: with the fix off the new candle went out after 6 real seconds with 30 s of it left; with it on it burned its full time (8 real seconds).

## Not fixed (yet)

Bugs found and left alone, with the reason.

- `Money.Account.CanAfford` (`Money.cs:140`) tests `f.RM >= funds.RM && f.OC >= funds.OC`, backwards (it asks whether the price is at least the funds). Nothing calls it.
- Monthly and biweekly subscriptions use `date.day - daysSinceLastCheck < 0` (`PlayerSubscription.cs:73-130`), written for a 0-based day, but `TimeAndDay.day` starts at 1. On the first of the month the result is 0, not below 0, so a monthly subscription only fires when two or more days pass between checks. `PlayerSubscriptionsController.CheckActiveSubscriptions` (`:106`) also subtracts a day per full week. Weekly ones look right. Not touched because the schedule logic is tangled and no shipped subscription was checked.
- `PlayerSubscription.HandleBankPayment` (`PlayerSubscription.cs:182`) uses `Money.FindOrCreateBankAccount`, which creates an account object but never adds it to `bankAccounts` (`Money.cs:584`). With no account the payment changes a throwaway object while the notification still says money was taken.
- `BaseHealthCondition.ApplyCondition` (`PlayerStatsConditions/BaseHealthCondition.cs:47-65`) only adds the condition to the active list in the `canStack` branch, so a non-stacking condition that isn't active yet is never added, and `UpdateStats` removes from the list it is looping over. `PlayerStatsRadiation.UpdateStats` (`PlayerStatsRadiation.cs:40`) passes `InverseLerp(0, threshold, threshold - Dose)`, which is 0 once the dose is over the threshold. Conditions don't drive anything visible yet (nothing else reads `activeConditions`), so these are unfinished rather than harmful.
- `SpawnItemsPrefab` (`SpawnItemsPrefab.cs:49`, `:74`) compares `Random.Range(0, 1)` with the item's probability (always true), and `NPCStats.ActivityTrigger.Trigger` (`NPC/NPCStats.cs:29`) does the same with `chance`. Neither component is used by anything I could find.
- Storage spawn chances (`Storage.cs:1009`, `:1156`, `:1161`) roll `Random.Range(1, 100) <= chance`. The roll is 1 to 99, so a chance of 99 or 100 always succeeds and every other chance is about 1% off. Not worth a transpiler.
- `Fishspawner.DeltaSeconds` (`Fishspawner.cs:55`) loops to `Count - 1` while calling `RemoveAt`, so a destroyed fish can stay in the list for a tick. `ChinaHandBotPlayer` (`ChinaHandBotPlayer.cs:31`) has the `Count - 1` slip too (the bot never picks its last down card) but it's inside an async method, which is awkward to patch for a card game bot.
- `SoundscapeArea.PlayRandomSoundsCoroutine` (`SoundscapeArea.cs:228-233`) is meant to re-roll when it picks the clip it just played, but the `while (audioClip != currentAudioClip)` inside the `if (audioClip == currentAudioClip)` can never run, so the same clip can repeat. `BottleRecyclingPanel.BottleType` (`BottleRecyclingPanel.cs:307`) rolls 0 to 6 for a face where case 7 (":I") is never shown.
- `Windows.cs:66` loops `j <= LitMaterials.Length` and relies on a catch to swallow the out-of-range read.
- Player.log noise: NPCs whose `NPC_FOV` has no head position (seen for "Seamus Liepa" and "Malone Petronis(Clone)") log "is missing head position" with a stack trace from `NPC_FOV.Update` (`NPC_FOV.cs:148`) every check, and `PauseNavigationHandler.OnPauseEnabledStateChanged` (`PauseNavigationHandler.cs:474`) logs "handler was null" each time the pause menu opens. Storage "Tool cart" logs "STORAGE HAS NO SPAWNABLE ITEMS" on every scene load (`Storage.cs:1029`, a content gap).
- `TimeAndDay(float seconds, float minutes, float hours)` (`TimeAndDay.cs:57`) assigns `seconds` to all three fields. Nothing calls it with arguments, so it has no effect today.
- `WeatherManager`'s rain and wind intensity getters (`WeatherManager.cs:244-305`) print a log line on every call while it rains or blows. They're only called when the soundscape updates (hourly or on area changes), so it's a few lines, not a flood.
- Closing the F1 console sets `Achievements.instance.globalDisable` back to false, so using the console doesn't keep achievements off. Probably intended.
