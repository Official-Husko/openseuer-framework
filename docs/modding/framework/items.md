# Items and recipes

`ObenFramework.Items` wraps the game's item code: finding items, giving them to the player, spawning pickups, and adding your own items and recipes. For how the game models items, see [items-crafting.md](../../game/items-crafting.md).

All calls are for the main thread. Giving, taking and spawning need a loaded world.

## Finding items

```csharp
using ObenFramework.Items;

Item turnip = ItemLookup.ById(12);
var turnips = ItemLookup.ByTitle("Turnip");        // two items share this title: 12 (food) and 10070 (plant)
var hits = ItemLookup.Search("bottle", max: 10);   // partial match, exact titles first
var food = ItemLookup.ByCategory("Food");
```

Titles are not unique, so title lookups return lists. Take the id once you know which item you mean. The lookup tables rebuild themselves when the game reloads its database or a mod adds or removes items.

## The player's items

```csharp
PlayerItems.Give(12, 3);                    // true if everything fit
PlayerItems.Has(12, 2);
int n = PlayerItems.Count(12);
bool took = PlayerItems.Take(12, 2);        // all or nothing
```

- Grants use owner -1 and skip crime checks, so a reward is never theft and stacks merge with the player's own.
- **Overflow:** `Give` fills the inventory grid first. What doesn't fit is dropped on the ground at the player's feet by the game, and `Give` returns false. Call `GiveWithoutDropping(item, amount)` to add as many as fit (grid, equipment slots, then the equipped bag) and get back how many did not.
- `Take` removes nothing when the player has fewer than asked. Stacks owned by NPCs are taken without a theft check.
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
- If your mod is removed later, stacks of its items in a save appear as Open Sewer Coin until the player drops them. Tell players, or avoid removing items.
- Loot tables and shops don't know your item. Give it to the player in code, or add it to a shop yourself.

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

## Checking in game

Bridge commands: `items.find`, `items.give`, `items.count`, `items.spawn`.
