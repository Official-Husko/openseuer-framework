# Custom furniture, blueprints and building areas

`ObenFramework.Building` lets a mod add new pieces of furniture. Each is a copy of one of the game's pieces (the template) with its own id, title, description, prices, icon, build menu group and, if you like, its own OBJ model. It behaves like its template: a copy of a chair can be sat on, a copy of a shelf stores things. Pieces can be sold in furniture shops, found as flatpack loot, and crafted from a blueprint the player finds or gets in the mail.

Add `"ObenFramework"` to your mod's `dependencies` and `using ObenFramework.Building;`. Placing, moving and removing pieces from code, and the placed/removed events, are in [world.md](world.md#placed-furniture); models in [assets.md](assets.md#models).

## Defining a piece

```csharp
public override void OnLoad()
{
    ModelRegistry.Load(this, "stool", "models/stool.obj", done: model =>
    {
        string id = CustomFurniture.Define(this, "stool", new FurnitureDefinition
        {
            Template = "Red Metal Chair",      // the name add_furniture takes, an asset path, or a title
            Title = "Mushroom Stool",
            Details = "Grown, not built.",
            PriceOC = 80,
            Model = model,                     // null keeps the template's look
            Category = "Mod Furniture",        // a game group (Chairs, Tables, ...) or a new one
            Shops = { "<shop name or GUID>" }, // furniture shops, see shops.list
            ShopStock = 2,
        });
        CustomFurniture.AddFlatpackLoot(this, id, "Trash", weight: 3);
        CustomFurniture.DefineBlueprint(this, id, new BlueprintDefinition
        {
            Items = { [35180] = 4 },           // 4 scrap metal
            Seconds = 1800f,
            Skill = PlayerStats.PlayerSkillType.Carpentry,
            Pools = { CustomFurniture.GeneralPool },
        });
    });
}
```

`Define` returns the piece's id, `oben:<modId>/<key>`. Keep the key the same between versions: the id is what saves find the piece by. Defining throws on a bad definition (unknown template, a group the game keeps for itself such as All, Items, None, Wallpaper or Surfaces, colliders without a model) and when the id is already taken. The piece is built on first use: the build menu, a shop, a save that has one, or `Get`.

The definition fields:

| Field | Meaning |
|---|---|
| `Template` | The game piece to copy. Surfaces, wallpapers and trains can't be copied. Confirmed templates in 0.5.13: `Red Metal Chair`, `Wooden Chair`, `Wooden Table`, `Small Wooden Table` (653 of the console's 655 pieces qualify). |
| `Title`, `Details`, `Icon` | Null keeps the template's; with its own `Model` and no `Icon` the piece gets a picture of the model (see [assets.md](assets.md)). |
| `PriceOC`, `PriceRM` | -1 keeps the template's. |
| `Model`, `Colliders` | A `ModelTemplate` from `ModelRegistry`. It goes under the piece's child 0 (the part the game rotates), centred on the template's visible mesh and standing on its base, at its own size. The template's renderers there are hidden and its solid colliders swapped for boxes around the model on the same objects, so sitting and storage still work. `Colliders` gives your own boxes (in the model's space, at most 64). The framework keeps its own copy of the mesh, so the piece stays intact after your mod unloads. |
| `Placement` | `Template` (default), `Floor`, `Wall`, `Ceiling`, `FloorAndWall`, `Any`. |
| `Category` | Build menu group. A new name gets its own group, numbered from 1000 and named through a patch on the game's group names. |
| `Shops`, `ShopStock` | Furniture shops that sell it and how many after each restock. |

Other calls:

```csharp
global::Furniture piece = CustomFurniture.Get(id);   // builds it if needed; null when not defined or the build failed
CustomFurniture.Give(id, 2);                         // into the building inventory
CustomFurniture.GiveFlatpack(id);                    // as the game's Furniture Flatpack item (19410)
bool custom = CustomFurniture.IsCustom(piece);
List<string> ids = CustomFurniture.Ids;
List<string> missing = CustomFurniture.Missing;      // ids that loaded as stand-ins this session
```

Defined pieces are added to the developer console's furniture list, so `add_furniture Mushroom Stool` works, and `FurnitureKinds.Find` and `FurnitureBuilder.Place` accept the id.

## Saves, and when a mod goes away

The game saves a piece of furniture as just its asset path. A custom piece shares its template's path, so the framework has the game save it in full instead (id, title, prices, path) and links it back by id on load. What a player sees when things are missing:

- **The mod is gone, the framework is there:** the piece loads as a stand-in: a copy of its template with the saved id and title. It keeps its place, its contents and its id, can be taken and placed again, and becomes the real piece again once the mod is back. The log names the missing mod once. `CustomFurniture.IsStandIn` tells them apart.
- **The framework is gone too:** the game links the piece to its template, so it simply becomes the template piece. Nothing breaks.
- **Blueprints** are saved by the game in full. With the mod gone a blueprint stays usable and crafts the stand-in; without the framework it crafts the template piece.

Pieces of a mod that unloads during a session (a hot reload) keep working until the level changes; after that they resolve again by id.

`CustomFurniture.Loaded` is raised for each placed custom piece once the world is ready after a load, for mods that keep per-piece state of their own.

## Shops

A furniture shop that lists the piece (`Shops`, by shop name or GUID as in [shops.md](shops.md)) gets it each time it restocks. A shop loaded from a save that never offered the piece (the mod is new to the save) gets it once. The shop also buys the piece back. Which shop offered what since its last restock is kept per save (`mods/ObenFramework.customfurniture.json` in the save folder), so a piece the player bought out doesn't reappear on every load.

## Loot

`AddFlatpackLoot(owner, id, category, weight)` lets a loot category hold the piece as a flatpack. The framework puts the flatpack item into the category's pool (through `LootPools`, with the summed weight of every mod piece in it, at most 100) and fills each flatpack the category spawns with one of the pieces, picked by weight. Containers, ground spawners and packages rolled from the category are covered. The entry ends when the owner unloads or you dispose the result.

## Blueprints

`DefineBlueprint(owner, furnitureId, definition)` gives a piece a blueprint (`<furniture id>/blueprint`) that crafts it at the game's workbenches:

| Field | Meaning |
|---|---|
| `Items` | Item id to amount, up to 16. |
| `Furniture` | Other pieces as ingredients, one each, up to 8: game pieces by name or path, mod pieces by id. |
| `Seconds`, `Amount` | Crafting time in game seconds; pieces per craft (1-100). |
| `Skill`, `MinimumSkill` | The skill it trains and needs. |
| `Pools` | Where players find it: `CustomFurniture.GeneralPool` (""), `CustomFurniture.StartPool` ("Start"), or a blueprint category of the level. Empty: only through code or mail. |
| `Title`, `Details`, `Icon` | Null: "&lt;title&gt; Blueprint", empty, the piece's icon. |

```csharp
CustomFurniture.GiveBlueprint(id);              // with the game's notification; quiet: true for none
CustomFurniture.MailBlueprint(id, "Uncle Pekka", parcel: false);
bool known = CustomFurniture.IsBlueprintKnown(id);
TaskItemBlueprint blueprint = CustomFurniture.BlueprintOf(id);
```

The game's pools are shared assets; the framework adds one list of its own to each and takes it out again when it unloads. A level that is already running gets new blueprints added to its live pools too.

`Blueprints` covers every blueprint of the level, the game's and the mods': `Blueprints.All()` (`BlueprintInfo`: `Id`, `Title`, `Result`, `Known`, `Custom`), `Blueprints.Find(idOrTitle)`, `Blueprints.IsKnown(blueprint)`, `Blueprints.Give(blueprint)`.

## Building areas

```csharp
BuildingAreaInfo? here = BuildingAreas.Current;     // Id (zone GUID), RemoveOnly, MenuDisabled
BuildingAreas.Entered.Subscribe(this, area => ...);
BuildingAreas.Exited.Subscribe(this, area => ...);
```

Raised from the game's own area triggers when the player walks into or out of a building zone. The zones themselves are listed by `FurnitureBuilder.Zones()`.

## Dev bridge

| Command | Does |
|---|---|
| `furniture.custom` | defined pieces (built, title, group, price, blueprint) and the ids that loaded as stand-ins; with `key` and `template` (and `title`, `priceOC`, `category`, `model`, `shop`) defines a test piece owned by the bridge |
| `furniture.give` | gives `id` (`amount`, `flatpack`) |
| `furniture.loot` | adds `id` to loot `category` as flatpacks (`weight`), or `clear` |
| `furniture.blueprints` | blueprints of the level, game and mod (`text`, `max`); `give` hands one over |
| `building.area` | the zone the player is in; `events=true` returns entered/exited/loaded events |

```sh
oben call furniture.custom key=test template="Red Metal Chair" title="Test Chair" category="Mod Furniture"
oben call furniture.give id=oben:DevBridge/test
oben call furniture.blueprints text=chair
oben call building.area
```
