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

## Not fixed (yet)

Bugs found and left alone, with the reason.

- Bank interest never accrues. `Money.CurrentTime` (`Money.cs:644-650`) multiplies balances and debts by `Mathf.Pow(1 + interest, num / 7)` where `num` is the number of days since the last check and the division is an int division. The check runs every day with `num` = 1, so the exponent is 0 and neither savings interest (`Interest`, 0.015) nor debt interest (`DebtInterest`, 0.03) ever applies unless 7 or more days pass between two checks. The intended period (weekly? the loan text says "payment time of N months and an interest of 3%") isn't clear, and turning it on makes existing debts start growing, so it's left for a decision.
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
