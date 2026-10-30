# Crafting

`ObenFramework.Crafting` covers the game's crafting stations (stoves, workbenches, stills, grow boxes, dishwashers), recipes, and growing. Adding your own recipes is in [items.md](items.md#adding-recipes). For how the game models crafting, see [items-crafting.md](../../game/items-crafting.md).

All calls are for the main thread and need a loaded world. Everything a mod registers (speeds, quality factors, filters, sources, subscriptions) is owner-tracked and leaves with the mod.

`IsAvailable` on `CraftingEvents`, `CraftingSources`, `CraftingSpeed`, `CraftingQuality`, `RecipeFilter`, `Recipes`, `Plants`, `Dishwashers`, `Recycling` and `Autofarms` says whether the framework's crafting hooks applied. When a game update breaks one, the whole crafting area is switched off: the events stay quiet and the factors, filters and sources do nothing.

## Events

```csharp
CraftingEvents.CraftStarted.Subscribe(this, s => Log.Info($"{s.Station.Name} started {s.Name} ({s.RecipeId}) x{s.Multiples}"));
CraftingEvents.Crafted.Subscribe(this, d => Log.Info($"{d.Name} {(d.Success ? "made" : "failed")}: {string.Join(", ", d.Results)}"));
Recipes.Unlocked.Subscribe(this, u => ...);    // found (a note, a book) or learned (crafted the first time)
```

Every station takes ingredients and makes its output through the same two game methods, so the events cover the player's own crafts and stations that run by themselves (stills, grow boxes, autocrafting machines). The quality and time previews a window shows are not crafts. Nothing is raised while a save loads, which also skips crafts the game finishes offline during the load.

A dishwasher makes up a recipe for each wash (named after the washed item), so its crafts carry `RecipeId` -1: use `Name` there. The game files such a recipe as learned, but `Recipes.Unlocked` leaves it out.

## Stations

```csharp
foreach (var s in CraftingStations.All())      // a scan; not every frame
    Log.Info($"{s.Id} {s.Kind} {s.Name} busy={s.Busy} powered={s.Powered} q={s.EquipmentQuality}");

CraftingStations.Start(id);    // still: process the loaded batch; workbench-like: craft the selected recipe; dishwasher: wash the selected item; grow box: plant one
CraftingStations.Stop(id);     // a still's batch with liquid output fails, as when opening the lid early
CraftingStations.Pause(id);
CraftingStations.Resume(id);
bool ok = CraftingStations.HasIngredients(station, recipe, multiples: 2);
bool? sun = CraftingStations.HasNaturalLight(station);
```

- `StationInfo.Id` works like a container id: the GUID, or `furniture:{id}:{n}` for the n-th station inside furniture the player placed. `Kind` is the game class (`manufacturing`, `manufacturingProcess`, `Growing`, `Dishwasher`, `Generator`), `Name` what its window shows.
- The control calls refuse while a save loads or while the player has a station window open. `Start` checks the ingredients first and refuses stations that need the player present.
- `CraftingUpgrades.All()`, `Of(station)` and `SetOn(id, on)` cover the add-ons that give stations recipes, conditions or equipment quality.

## Speed and quality

```csharp
CraftingSpeed.Set(this, CraftingSpeed.AllStations, 2f);    // everything crafts twice as fast
CraftingSpeed.Set(this, "Stove", 0.5f);                    // by station name...
CraftingSpeed.Set(this, "manufacturingProcess", 1.5f);     // ...or class
CraftingQuality.Set(this, CraftingQuality.AllStations, 1.2f);
CraftingSpeed.Remove(this, "Stove");
```

- A station's speed is the product of the factors for `*`, its class and its name, from all mods together, kept within 0.01 to 100. A craft keeps the speed it started with; windows show the changed time. Grow boxes grow at the plants' own pace.
- Quality factors multiply the same way and the result stays within the game's 0.05 to 2. Recipes with a fixed quality and items whose quality is fixed are left alone.

## Extra ingredient sources

`CraftingSources` lets a station use containers as if their contents were in its input slots: a pantry next to the stove, a crate beside the workbench.

```csharp
var pantry = CraftingSources.Register(this, station => ContainersNear(station.Position, 3f), priority: 10,
    slotFilter: (storage, index, stack) => stack.ownerId <= 0);     // optional: never use other people's stacks
CraftingSources.Consumed.Subscribe(this, t => Log.Info($"took {t.Amount} of {t.ItemId} from {t.Storage.name}"));

CraftingSources.SetPriority(pantry, 20);           // change a registration in place
CraftingSources.SetSlotFilter(pantry, null);       // or its filter; null lets it use every slot
pantry.Dispose();                                  // or leave it to the mod's unload
```

- While a station lists, checks or takes ingredients, its input storage's slot list is swapped for its own slots plus the extra ones, and swapped back right after. The game's own recipe rules (item groups, liquids in bottles, ammo, variants, multiples) apply unchanged, and the station's own slots are used first, then the extra containers in priority order (higher first, then registration order).
- The game only checks ingredients when a window refreshes, and a container next to the station can be emptied in the meantime, so starting a craft at a station with extra sources checks again and refuses when they no longer hold enough.
- The provider is asked once per station and frame; keep it quick and return an empty list for stations it doesn't serve. Dishwashers never use extra sources.
- `SetPriority` and `SetSlotFilter` take what `Register` returned and apply from the next check on; a changed priority keeps its place among equal priorities. Both return false once the registration has ended.
- `CraftingSources.For(station)` lists the containers a station would use now, `Entries()` every registration with its owner, priority and whether it has a filter.

## Recipes

```csharp
Recipe r = Recipes.ById(42);
var making = Recipes.Making(itemId);          // recipes whose output has the item
var uses = Recipes.Using(itemId);             // recipes that take it
var soups = Recipes.Search("soup");
bool known = Recipes.IsKnown(r);
bool carried = Recipes.CanCraftFromInventory(42);
bool here = Recipes.CanCraftAt(42, station);  // the station lists it: type and conditions
Recipes.Unlock(42);                           // as if the player found it
```

`RecipeInfo` is a snapshot with ingredients, results, fail results, skill, conditions and tags. The framework never writes the game's Recipes.json.

### Filtering the recipe lists

```csharp
string search = "";
RecipeFilter.Add(this, ctx => RecipeFilter.NameContains(ctx.Recipe, search));
search = "soup";
RecipeFilter.Refresh();      // rebuild the open window's list
```

A recipe shows only if every filter keeps it; a filter that throws keeps it, so one broken mod can't empty the list. Selecting with a controller follows the filtered list.

## Growing

```csharp
foreach (var g in GrowingStations.All()) ...        // spots, what is planted, paused
GrowingStations.SetPaused(id, true);
foreach (var p in Plants.All()) ...                 // age, stage, ready fruit; Id is the plant's save GUID
var fruit = Plants.Fruit(plantId);
PlantActions.Harvest(plantId);                      // into the inventory
PlantActions.SetAgePercent(plantId, 90);
PlantActions.Plant(spotId, seedItemId);
Plants.Harvested.Subscribe(this, h => ...);         // whole-plant harvests
Plants.Picked.Subscribe(this, p => ...);            // single fruit
Autofarms.All(); Autofarms.SetGrowPercent(id, 20, 60); Autofarms.Replanted.Subscribe(this, r => ...);
```

## Machines

- `Dishwashers`: list with progress, `Contents(id)`, `Finished`. The game's dishwasher washes the owner off stolen items (it takes washing powder and power); each wash takes the item's value times 10 game seconds, at least 100.
- `Recycling`: bottle machines with the deposit they owe, `SetOwed`, `Recycled`.

## Checking in game

Bridge commands: `crafting.stations`, `crafting.control`, `crafting.speed`, `crafting.quality`, `crafting.events` (craft, source, recipe, plant, dishwasher, recycling and autofarm events), `crafting.sources` (the bridge's own sources: a container or every container near the station, priority and an item slot filter changed in place), `recipes.filter` (the bridge's own name filter), `recipes.find`, `recipes.unlock`, `worlditems.list kind=plants|growing|autofarms|dishwashers|recyclers|upgrades`.
