# Items and recipes

`ObenFramework.Items` wraps the game's item code: finding items, giving them to the player and hearing the inventory change, held items, stacks with quality or liquid, spawning pickups, adding your own items and recipes, loot tables, containers in scenes that aren't loaded, and the world objects that hand out items. Crafting stations, recipes and growing are in [crafting.md](crafting.md). For how the game models items, see [items-crafting.md](../../game/items-crafting.md).

All calls are for the main thread. Giving, taking and spawning need a loaded world.

## Finding items

```csharp
using ObenFramework.Items;

Item turnip = ItemLookup.ById(12);
var turnips = ItemLookup.ByTitle("Turnip");        // two items share this title: 12 (food) and 10070 (plant)
var hits = ItemLookup.Search("bottle", max: 10);   // partial match, exact titles first
var food = ItemLookup.ByCategory("Food");
```

`ItemLookup.Icon(id)` gives an item's inventory sprite and `ItemLookup.QualityOf(stack.Quality)` the game's quality grade. Titles are not unique, so title lookups return lists. Take the id once you know which item you mean. The lookup tables rebuild themselves when the game reloads its database or a mod adds or removes items.

## The player's items

```csharp
PlayerItems.Give(12, 3);                    // true if everything fit
PlayerItems.Has(12, 2);
int n = PlayerItems.Count(12);
bool took = PlayerItems.Take(12, 2);        // all or nothing
```

- Grants use owner -1 and skip crime checks, so a reward is never theft and stacks merge with the player's own.
- **Overflow:** `Give` fills the inventory grid first. What doesn't fit is dropped on the ground at the player's feet by the game, and `Give` returns false. Call `GiveWithoutDropping(item, amount)` to add as many as fit (grid, equipment slots, then the equipped bag) and get back how many did not.
- `Take` removes nothing when the player has fewer than asked. Stacks owned by NPCs are taken without a theft check. Liquids are refused, because the game's remove call matches a bottle by the liquid in it and would take the whole bottle; use `StackActions.TakeLiquid`. If the game removes a different number than asked, the difference is put back and `Take` returns false.
- `PlayerItems.Stacks()` lists every stack with its details (`ItemStackInfo`: quality, freshness, liquid, owner, stolen flag, charge, wallet coins, written text). `FreeSlots` and `Capacity` count the grid and the equipped bag.
- **Money ids:** item 0 (Open Sewer Coin) and 1 (Real Money) are the two currencies, not stacks. `Give`, `Take` and `Count` route them to the [wallet](economy.md) in whole units. Don't use these ids for anything else.

## Spawning pickups

```csharp
WorldItems.Spawn(12, 5, new Vector3(10, 1, 4));   // one pickup holding 5
WorldItems.SpawnAtPlayer(12, 1);                  // on the ground in front of the player
```

Both return the pickup `GameObject`, or null when the item is unknown or has no pickup prefab (the reason is logged).

## Adding items

An item is a normal `Item` object. Ids must come from the block reserved for mods (1,000,000 to 16,000,000), and `ReservedIds.For` gives a stable id from your mod id and a key, so saved games keep working between sessions.

```csharp
public override void OnLoad()
{
    var sprite = LoadMySprite();   // from a PNG or an asset bundle
    var rustyKey = new Item
    {
        ID = ReservedIds.For(Info.Id, "rusty_key"),
        Title = "Rusty Key",
        Description = "Opens something, probably.",
        Categories = new[] { "Misc" },
        BaseValue = 4,
        BaseStackable = 1,
        Appearance = new Item.ItemAppearance { Material = "Metal", Sprite = sprite },
    };
    ItemRegistry.Register(this, rustyKey);
}
```

- `Register` takes the mod as owner. The item is put back into the database whenever the game rebuilds it, and removed when your mod unloads.
- **A sprite is required.** The game draws a slot empty when the item has none. Assign `Appearance.Sprite` directly and leave `SpritePath` empty.
- **Pickup prefab:** assign `Appearance.Prefab` (and `PrefabMany`) to a GameObject with a `CollectibleItem`, for example another item's `Prefab`. Without one the item spawns as the game's ERROR prefab. Items may share a prefab: the framework points the pickup at the right item when it spawns. Pickups of modded items are not restored from saves yet, because the game rebuilds dropped items from a `Resources` path.
- `ItemRegistry.Derive(template, id, title)` copies an existing item (actions, meta, categories, look) as a starting point.
- If your mod is removed later, its items in a save become a "Missing item" stand-in that keeps the id (see below), and are the real item again once the mod is back.
- Loot tables and shops don't know your item. Give it to the player in code, or add it to a shop yourself.
- `ItemRegistry.OwnerOf(id)` is the mod that registered an item.

## Items from removed mods

A save only keeps an item's id. Without the mod that adds it, the game would show such a stack as Open Sewer Coin on the first lookup and lose the id for good. `ItemPlaceholders` stops that: any lookup of a missing id in the mod range (`ReservedIds`) gets a stand-in with the same id.

- It is called "Missing item (Mod: Title)" with a paper bag icon. The mod and title come from `mods/ObenFramework.items.json`, which every save writes: the items mods register now, plus the missing ones the loaded save listed, so the names survive saving without the mod.
- It can't be used, eaten or sold (shops refuse it), stacks up to any size, keeps the stack's own data (quality, durability) and drops as the game's ERROR model that picks up as the same stand-in.
- It is saved under its own id. Install the mod again and the stack is the real item on the next load. If the mod comes back while the game runs (a hot reload), stand-ins already handed out turn into the real item right away; a mod unloading mid-game leaves its items as stand-ins named after it.
- `ItemPlaceholders.IsPlaceholder(id or item)`, `Active()` (stand-ins handed out this session) and `MissingInSave()` (what the loaded save lists without its mod) tell you about them. `IsAvailable` is false if its patches failed (patch area `ItemPlaceholders`).
- `ItemPlaceholders.Scan()` lists the stacks of missing items in the inventory and the containers of the loaded scenes (item, mod, amount, place) without changing anything.
- `ItemPlaceholders.RemoveMissing(modId)` deletes one mod's missing items from the inventory and the containers in the loaded scenes and stops listing them, for a mod the player removed for good; `RemoveAllMissing()` does it for every mod. They can't come back after that.
- Items in scenes that aren't loaded, and loot that the game rolls from its own tables, aren't touched; they get the stand-in when they're looked at.

## Adding recipes

```csharp
var recipe = RecipeBuilder.Create(ReservedIds.For(Info.Id, "key_recipe"), "Rusty Key", RecipeType.Basic)
    .Input(ItemLookup.ById(12), 2)     // two Turnips
    .Output(rustyKey)
    .Time(30)
    .Build();
RecipeRegistry.Register(this, recipe);
```

- Use an existing `RecipeType`: stations only show the types they were built for. `Basic` is the hand-crafting list.
- The recipe is added once the game's recipes and every item it uses exist, so it can mention your own items. Crafting stations that are already running are refreshed.
- `RequiresLearning()` hides the recipe until the player has learned it (`RecipeController.AddRecipe`).
- The framework only touches the in-memory list. It never calls `RecipeDatabase.AddRecipe` and friends, which would rewrite the game's `Recipes.json`.

## Inventory events

```csharp
InventoryEvents.ItemAdded.Subscribe(this, c => Log.Info($"+{c.Amount} of {c.ItemId}, now {c.Total} ({c.Source})"));
InventoryEvents.ItemRemoved.Subscribe(this, c => ...);
InventoryEvents.HeldChanged.Subscribe(this, h => Log.Info($"{(h.LeftHand ? "secondary" : "primary")} hand: {h.ItemId}"));
InventoryEvents.ItemUsed.Subscribe(this, id => ...);    // ate, drank, read or applied an item from the inventory
```

- The events follow what `PlayerItems.Count` counts: the inventory grid, the clothing and hand slots and the equipped bag. They report the **net** change per item, so moving a stack between two inventory slots raises nothing, while moving it into a chest raises `ItemRemoved`. Equipping a bag adds what is in it.
- The game changes stacks in many places, so the framework doesn't follow each call. Any change to a stack marks the inventory for a recount, which runs once at the next frame and costs nothing on frames without changes. Several changes in one frame come as one net change per item. The framework's own calls (`PlayerItems`, `StackActions`, `HeldItems`) recount before they return, and `InventoryEvents.Flush()` does it on demand after you called the game's methods yourself.
- `Source` is `Mod` for the framework's calls and anything inside `WorldChanges.Enter(ChangeSource.Mod)`, `LocalPlayer` for dragging and quick-moving in the inventory window, `Game` otherwise.
- Nothing is raised while a save loads or between scenes: the inventory the game restores is the starting point. Money isn't in the inventory; use the wallet's events.
- A handler that changes the inventory again is reported on the next frame.

## Held items

```csharp
int right = HeldItems.Primary;      // -1 when empty
HeldItems.Equip(itemId);            // from the grid into the hand slot its category asks for
HeldItems.Unequip(leftHand: false);
```

`Equip` takes a single, unowned stack from the grid and swaps out what was in the hand, like the inventory window. It refuses items that aren't wieldable, a slot holding someone else's item, and two-handed items while the other hand is full.

## Stacks with quality, owners and liquids

```csharp
StackActions.Give(12, 3, new StackSpec { Quality = 1.5f });                     // only if it all fits
StackActions.GiveOrDrop(bottleId, 1, new StackSpec { LiquidItemId = waterId, LiquidAmount = 0.5f });
StackActions.Drop(12, 1, new StackSpec { OwnerId = npcId });                     // at the player's feet, owned by an NPC
float poured = StackActions.AddLiquid(waterId, 1f);                              // into carried containers
bool ok = StackActions.TakeLiquid(waterId, 0.5f);                                 // all or nothing, bottles stay
```

A spec that can't apply (quality on an item without quality, liquid in something that isn't a container) refuses the call.

## Extra containers for blueprints and renovations

`InventorySources` lets containers count as part of the inventory where the game only looks at the inventory: crafting furniture from a blueprint at a workshop, and tenement renovations.

```csharp
InventorySources.Register(this, ctx => NearbyCrates(5f));   // ctx.Kind is FurnitureBlueprint or Renovation
```

While the game checks resources, the containers add to the counts (items, ammo, liquids). While it takes them, the player's inventory (and for renovations the tenement's own resource storages) pays first and the containers only cover what is missing. The provider is asked each time the game checks, so keep it quick. Crafting stations have their own [`CraftingSources`](crafting.md).

## Containers in scenes that aren't loaded

```csharp
var ids = UnloadedStorage.ContainerIds("Interior Player Tenement", out string why);
var box = UnloadedStorage.Read("Interior Player Tenement", guid);      // stacks as the save has them
UnloadedStorage.QueueGive(this, "Interior Player Tenement", guid, 12, 5);
UnloadedStorage.QueueTake(this, "Interior Player Tenement", guid, 12, 2);
UnloadedStorage.Applied.Subscribe(this, c => ...);
UnloadedStorage.Failed.Subscribe(this, c => Log.Warn(c.Reason));
```

- Reading works from the level file of the save folder the game loaded last, which after every door holds the latest state of each visited scene. A scene the player never visited has no file. Use `Containers` for the loaded scene.
- Queued changes are kept with the save and applied through the game's storage methods when the player next enters the scene: a give that doesn't fully fit is taken back out, a take needs the whole amount, and a container that starts late is retried for ten seconds. Each mod can keep 256 changes; changes in a row to the same container and item merge.
- The queue follows the save: a change queued and never saved is gone after a reload, like any other unsaved progress, and a save taken in the scene drops the applied changes.
- Containers are named by GUID. Containers inside furniture the player placed get a new GUID with every save, so only the scene's own containers work. With the ObenSave mod on, the level files aren't plain JSON and reading refuses.

## Loot tables

```csharp
LootPools.Add(this, "Kitchen", rustyKey.ID, weight: 2);              // common pool
LootPools.Add(this, "Kitchen", goldTooth.ID, 1, LootTier.Rare);
LootPools.DefineCategory(this, "MyMod Crates");                      // for your own containers' itemSpawnCategory
LootPools.ContainerFilled.Subscribe(this, f => ...);                 // respawns on the game's clock
```

A pool is a list of ids where the weight is how often an item is listed; vanilla entries usually weigh 1 to 5. `LootPools.Categories()` and `PoolOf(name)` show the table. Additions live in memory only, are put back whenever the game rebuilds the table or a mod item appears, and leave with your mod. StorageSpawnCategory.json is never written.

## Objects that give items

Each of these lists the objects of the loaded scenes with ids that work like container ids (the GUID, or `furniture:{id}:{n}` inside placed furniture), changes them, and has events that stay quiet while a save loads:

| Class | What | Events |
| --- | --- | --- |
| `ResourceNodes` | scrap piles, trees, rocks: stock, outputs, `SetStock` | `Hit`, `Depleted` |
| `Spawners` | timed spawn points: chance, interval, `SpawnNow` | `Spawned` |
| `Breakables` | windows and crates (`Break`, `Repair`), breakable locks (`BreakLock`), barrels and animals that drop items | `Broken`, `Repaired`, `LockBroken`, `Smashed` |
| `Extractables` | things worked loose in several goes: `Work` | `Extracted` |
| `ItemGivers` | taps, flyer boxes, trees: `SetEmpty`, `Refill` | `Gave` |
| `TaskItems` | the journal's keys, keycards, recipes and quest items | `Changed` (old and new amount) |

## Consumables, fuel and the container window

```csharp
var soup = ItemConsumables.Get(soupId).Copy();
soup.Food += 20;
ItemConsumables.Set(this, soupId, soup);           // every bowl of soup, until your mod unloads

foreach (var fuel in FuelTypes.All()) ...          // burn time and what is left after burning

TaskItems.Add("Key_Basement");                     // journal items by id; TaskItems.Remove takes them

StoragePanelButtons.Add(this, "Sort", press => SortInto(press.Container.Storage));
```

- Consumable values live on the item definition, so they apply to every copy. Changes from several mods stack (the newest wins; removing it brings back the one under it, or the game's own values). They aren't saved: set them in your mod's load.
- Container window buttons are copies of the game's "Take all" button, hidden while trading and for containers that belong to someone. A press on a full container shows the game's "storage full" feedback instead of calling your action.

## Checking in game

Bridge commands: `items.find`, `items.give`, `items.count`, `items.spawn`, `inventory.stacks`, `inventory.equip`, `inventory.liquid`, `inventory.events`, `loot.pools`, `storage.unloaded`, `storage.queue`, `worlditems.list kind=...`, `worlditems.events`, `registry.items` (mod items and their mods), `registry.placeholders` (missing mod items), `registry.missing` (missing stacks and where they are), `registry.removemissing mod=...` or `all=true` (delete them for good). The `*.events` commands start listening on their first call.
