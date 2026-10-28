# Performance Fixes

`mods/PerformanceFixes` removes work the game repeats every frame for no visible reason. Every fix is a feature of its own, so any of them can be switched off from the F1 console (`features PerformanceFixes/<id> off`) if it ever misbehaves, and the notes below say how to compare each one on and off.

The mod follows the ranked list in `docs/research/performance.md` (section 12). That document was written from a static read of the code, so every claim used here was checked again against `decompiled/Assembly-CSharp/` (game 0.5.13) before patching, and the patch targets (methods and private fields) were looked up by name in the running game. Nothing has been timed in a real session yet; the `perf` command below is how to do that.

The rule for what went in: the visible result must be the same, only the work and the garbage go down. Where a fix keeps a cache, the cache is checked against the live values on every call instead of being trusted, so it can't go stale. Anything that would change timing, save contents or the look of the game stayed out (see the end).

## The perf command

`perf [seconds]` in the F1 console prints, for the last N seconds (default 10, at most 300):

```
Last 10.0 s, 1432 frames
Average 6.98 ms (143.2 fps)
Worst 18.20 ms, 99th percentile 9.10 ms, slowest 1% average 12.40 ms
Allocated about 310 KB/s, 2 garbage collections, heap 412 MB
```

`perf reset` forgets the history. Frames are recorded into fixed arrays from `OnUpdate`, so the recording itself allocates nothing. The allocation figure is the growth of the managed heap from one frame to the next, with frames where a collection ran counted as zero. Mono hands memory out in blocks, so treat it as an estimate that is good for comparing on against off, not as an exact number.

The frame times are unscaled, so they don't change when the game is paused or sped up.

## Testing a fix

The same procedure works for all of them. Stand still in the same place (so the scene, lighting and NPCs are the same), do the thing the fix is about, and compare:

1. `features PerformanceFixes/<id> off`, do the action for 30 seconds, then `perf 20`.
2. `features PerformanceFixes/<id> on`, do the same, `perf 20`.

Expect lower "Allocated" and fewer garbage collections with the fix on; the average frame time moves by less (these are small costs), the 99th percentile and the worst frame are where a difference shows. Run each side twice, since the first run after toggling includes the cost of patching and the JIT. `features` with no arguments lists every feature and its state.

Each fix is also checked for "looks and behaves exactly the same", which is what matters more than the numbers.

## The fixes

All on by default.

### InventoryRefreshThrottle

`Inventory.DeltaSeconds` (`Inventory.cs:303`) is subscribed to `TimeOfDayAzure.SecondsPassed`. With the default day length one real second is 60 game seconds, so the event fires on nearly every frame. While the inventory screen is open it calls `UpdateAllSlotsNow`, which calls `Slot.UpdateSlot` on every inventory, backpack, clothing and foreign slot: perishing check, item data, five child objects toggled, texts and sprite assigned. That is roughly a couple of thousand UI writes and thousands of small allocations per second while the player looks at the screen.

What it changes: the clock-driven refresh runs at most every 0.25 seconds (`InventoryRefreshSeconds` in the mod's `config.json`, clamped to 0.05 to 2). Two cases always run at once, so nothing is ever late because of the fix: the first clock tick after the screen opens, and anything else that refreshes the slots. A postfix on `UpdateAllSlotsNow` records every refresh, including the ones the game asks for itself after adding, removing, moving or using an item (`UpdateAllSlots`, trading, crafting), so after a real change the next clock tick is skipped rather than repeating the work.

Why it's safe: the refresh is idempotent and draws state that already exists. Perishing inside it is based on timestamps (`ItemPerishabledata.CountPerishingTime` compares total minutes), so doing it less often only changes how soon a spoiled item turns into its spoiled form on screen, by at most a quarter of a second. Nothing reads the `sec` argument.

How to test: open the inventory (Tab) in a room with a full pack.
- `perf` on and off as above. With the fix on, "Allocated" should drop a lot (it is the largest term) and the frame time should be flatter.
- Visible behaviour: pick an item up while the screen is open, drag a stack onto another, split a stack, eat or use something, drop something. Each must show immediately, the same as with the fix off. Hold an item that is perishing (fresh food) and watch the freshness text for a minute: it should still tick over without visible lag.
- Open and close the screen quickly several times: it must always open fully drawn.

### InteractableLookup

`InteractObjects.GetInteractable` (`InteractObjects.cs:387`) runs every frame while the crosshair is on a usable object. It called `GameObjectExtensions.GetInterfaces<IInteractable>`, which gets every `MonoBehaviour` on the object (an array), then for each one calls `GetType().GetInterfaces()` (reflection, another array) with a LINQ `Any` and a closure, then `ToArray`, then `Where(...).FirstOrDefault()` with another closure. A few hundred bytes to a few KB of garbage per frame, all the time.

What it changes: the method is replaced by one that fills a reused list with the same components (`GetComponents` into a list), keeps a `Dictionary<Type, bool>` of "this type implements `IInteractable`", and returns the first component, in the same order, whose `IsInteractDisabled()` is false. Everything after that (the "(Use)" text, `UpdateUseMessage`, the quick slot hint, the `Growing` special case) is copied unchanged.

Why it's safe: nothing is cached per object. The object is asked for its components and for `IsInteractDisabled()` every frame, exactly as before, so a door that unlocks, a component added at runtime or an object that is destroyed is seen on the same frame. The only thing remembered is whether a class implements an interface, which can't change while the game runs. A nested call (if an `IsInteractDisabled` ever looked something up through the same method) falls back to the original.

How to test: walk around a base and a street and look at doors, containers, plants, beds, shops and NPCs.
- The "(Use)" prompt and every context message (locked, open, "needs key", plant growth text) must read the same as with the fix off. Lock and unlock a door with the crosshair on it: the prompt must change on the same frame.
- `perf` on and off while sweeping the crosshair over interactables: "Allocated" should drop.
- Third-person mode has its own path (`ThirdPersonInteract`) and isn't changed; it still ends in the same method.

### LightZoneCheck

`LightsController.FixedUpdate` asks every `LightCullZone` whether the player is inside until one says yes (`LightCullZone.CheckIfPlayerInZone`, `LightCullZone.cs:340`). That method used `zones.Any(...)` and `zones.All(...)` with lambdas that capture `this`, so every zone allocates a closure, a delegate and an enumerator on every physics tick.

What it changes: the same two tests as plain `for` loops. Same order, same short-circuiting, same call to the (unchanged) `IsWithinBoxBounds`, and `LightsController.CurrentZone` is set and cleared in exactly the same situations, including the empty-list case where "all zones are further away" is vacuously true. The one private helper (`ClosestPointOnBounds`) is copied line for line.

Why it's safe: it's the same logic with the LINQ removed; no timing or order changes. It is deliberately not throttled to 10 Hz as the research notes suggested, because that would change when lights switch.

How to test: go through a few buildings and walk in and out of rooms, with doors open and closed.
- Lights must switch on and off in each room at the same moment as with the fix off (walk the same threshold both ways, with the feature on and off).
- `perf`: allocation drops per physics tick; the difference grows with the number of zones in the level (check an interior with many rooms).

### InventorySlotBuffers

`Inventory.AllSlots` (`Inventory.cs:170`) joins the inventory, backpack and clothing slot arrays with `Concat(...).ToArray()` on every call. There are about fifty callers (item counts, category checks, `ShowTime.Update` while the inventory is visible, dialogue helpers, `UpdateAllSlotsNow`).

What it changes: the joined array for each of the three combinations (clothing, backpack, backpack plus clothing) is kept. Every call first compares the cached array with the live source arrays element by element (a few dozen reference comparisons, no allocation). If they all match it returns the cached array; if anything differs (a different backpack equipped, a slot replaced, a resized list) it builds a new array and caches that, leaving the old one exactly as it was. The "ignore the backpack and the clothing" case already returned the `Slots` array itself in the original, and still does.

Why it's safe: a caller holding an array from an earlier call can never see it change, because contents only ever change by making a new array. The cache is never trusted, always compared. No caller in the game writes into the returned array (checked all call sites); the only behavioural difference is that two calls may now return the same array instance.

How to test:
- `perf` with the inventory open and a clock equipped (so `ShowTime` runs every frame): allocation should drop.
- Equip, unequip and swap backpacks (default, rucksack, cooler bag) and check that items in the backpack still count: craft something that needs an ingredient stored only in the backpack, and check that picking an item up puts it in a free slot of the backpack when the pockets are full.
- Dialogue and shops that check for an item (`CheckForItemDialogue`) with the item in the backpack versus the pockets.

### AmbientColorCache

`Areaportal.LateUpdate` (`Areaportal.cs:51`) runs for every portal on every frame and, for the default `useAmbientColor`, calls `WeatherManager.CurrentOutsideAmbience`. That does `ActiveWeather().FindProperty("Environment Ambient Color")`, a linear string search through the profile's properties, then evaluates a gradient with the current time, sun and moon values. `WindowNaturalLight` calls the same method. The answer is identical for all portals in a frame.

What it changes: a prefix remembers the last answer together with its inputs: the active `AzureWeatherProfile` and the sky clock's timeline, sun elevation and moon elevation. If all four are exactly equal (same profile object, same floats) it returns the remembered colour. Otherwise it lets the original run and stores the result in a postfix. If the weather or clock isn't there at all it leaves it to the original, which fails the way it always did.

Why it's safe: `GetColorOutput` is a pure function of those four inputs (checked in `AzureProfileProperty.cs`), and the profile assets aren't modified at runtime (the weather controller blends into separate override outputs). So the cached colour is always exactly the colour the game would have computed.

How to test: stand somewhere with area portals (the exterior views from inside a building).
- The sky clock moves every frame, so the first portal in a frame misses and every other portal in the same frame hits (and all of them hit while time is frozen, such as in a menu that pauses the clock). The more portals in view, the bigger the saving.
- Visually compare the glow through portals and windows at dawn, noon, dusk and during a weather change with the fix on and off: they must match.
- `perf`: modest drop in frame time in scenes with many portals.

### IdleStatEffects

`PlayerStats.DeltaSeconds` (`PlayerStats.cs:306`) runs on each clock tick and calls `ProcessItemSpecialEffects`, which starts with `new List<...>(unprocessedSpecialEffects.Keys)`. The dictionary is empty almost all the time (it holds only what laxatives, coffee, radiation and similar items still have to apply), so this is a list allocated about 60 times a second for nothing.

What it changes: a prefix returns early when the dictionary is empty. With nothing pending, the original's loop has nothing to iterate, so this is exactly equal.

How to test: eat or drink something with a special effect (coffee, a laxative, a sleeping pill) and check the effect still applies (tiredness, bowel). `perf`: a very small allocation drop.

### TimerSnapshot

`TimeOfDayAzure.UpdateTimers` (`TimeOfDayAzure.cs:501`) runs every frame and starts with `timers.ToArray()` so that timers can remove themselves while the loop runs. About 60 files create `TimeOfDayAzure.Timer`s (cooldowns, delayed events, NPC activities).

What it changes: the same pass over a reused list that is filled from the real list, with an early return when there are no timers. The countdown, the callbacks (`Callback`, `Callback2`, each in the same try/catch with `Debug.LogException`), the removal and the order are copied from the original. A timer added by a callback isn't in the snapshot, so it first runs on the next pass, as before.

Why it's safe: same snapshot semantics, just not a fresh array. If the pass were ever entered while it is running (it isn't, the method is private and called once from `Update`), it falls back to the original.

How to test: start something that uses a timer, such as a plant growing, an NPC sleeping and waking on schedule, or a shop restock, and check that it fires at the same in-game time. Sleep through a night with the fix on and check that scheduled events (morning NPC routines) still happen.

## Config

`Mods/PerformanceFixes/config.json` is written on first load:

```json
{ "InventoryRefreshSeconds": 0.25 }
```

## Not done, and why

Everything from the ranked list that is not above, with the reason it stayed out:

- Coalescing `TimeOfDayAzure.SecondsPassed` to 10 Hz (item 3). It would help most, but it changes when 35 different subscribers run. Most only accumulate `sec`, but a few (`Fuel`, shops, `Prison`, `ContestController`, `TenementEventController`) could be sensitive to the frame they fire in. It needs a subscriber-by-subscriber review and ideally a mod-wide setting, so it's left for later.
- `PlayerStats.BarUpdate` throttling (part of item 4). Bars would visibly step. Only the allocation was removed.
- `Areaportal.LateUpdate` throttling (item 6). The ambient lookup it does most of its work in is cached above; skipping frames would change the fade, so it wasn't touched.
- `CullingController` rewrite (item 7). Index consistency with `CullingGroupEvent` is risky, and the saving is at scene load.
- `WindowNaturalLight.UpdateLights` (item 8). Moving the renderer loop inside the `textureModifyNext <= 0` branch looks harmless, but `AppendWindowsList` adds renderers at runtime that then rely on that loop to get their property block, so it could leave a window dark.
- `ShowTime.Update` (second half of item 9). Throttling it would delay the clock appearing when a clock is picked up or unequipped. The `AllSlots` garbage it caused is removed by `InventorySlotBuffers`.
- Lighting knobs (item 10: `lodBias`, shadow distance, volumetric samples). Those are quality settings that change how the game looks and need their own switches and a decision from the owner, not a silent optimisation.
- `FaceBlendShapeFollower`, `NPC_FOV`, wander destination coroutines (items 11 to 13). All change per-NPC behaviour or replace coroutines; `GenerateNewDestinationCO` would change which random numbers are drawn, so existing NPC movement would differ. Not behaviour-preserving enough to ship without playtesting.
- Save and load (item 14). Anything touching `SaveController` risks saves, so it's out of this mod.
- `SalsaConfigGuard`, probe rendering and database preload (item 15). Load-time only, and they change what happens at scene load.
- `TimeOfDayAzure` weather/time `FixedUpdate` at 10 Hz, `AzureEnvironmentController` guarded writes, `PowerMeter`, `TextUpdate`, `MaterialScroll`. Each is small and visible or per-instance; a future pass can pick them up one at a time.
