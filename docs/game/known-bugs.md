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

## Not fixed (yet)

- `TimeAndDay(float seconds, float minutes, float hours)` (`TimeAndDay.cs:57`) assigns `seconds` to all three fields. Nothing calls it with arguments, so it has no effect today.
- `WeatherManager`'s rain and wind intensity getters (`WeatherManager.cs:244-305`) print a log line on every call while it rains or blows. They're only called when the soundscape updates (hourly or on area changes), so it's a few lines, not a flood.
- Closing the F1 console sets `Achievements.instance.globalDisable` back to false, so using the console doesn't keep achievements off. Probably intended.
- `Random.Range(0, 1)` with ints is always 0 in a few places (see `docs/research/performance.md`).
