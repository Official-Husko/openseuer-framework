# World props

`ObenFramework.World.Props.WorldProps` places objects in the world that stay there: they are kept in the save and built again every time their scene loads. A prop is one of:

| Kind | Made with | What it is |
| --- | --- | --- |
| Model | `WorldPropSource.Model("MyMod/crate")` | a model loaded through [`ModelRegistry`](assets.md#models) |
| Primitive | `WorldPropSource.Primitive(WorldPrimitive.Cube)` | a cube, sphere, cylinder or plane |
| Furniture | `WorldPropSource.Furniture("Red Metal Chair", alt: 0)` | the look of a piece of furniture (id, short name or title, as `FurnitureKinds.Find` takes it) |
| Item | `WorldPropSource.Item(itemId)` | the world model of an item |
| Light | `WorldPropSource.Light(WorldPropLight.Point, Color.yellow, range: 6f)` | a point or spot light |
| Anchor | `WorldPropSource.Anchor()` | an empty object: a marker, or an invisible use point |

Furniture and item props are decoration only. The game's prefab is copied under an inactive holder (so none of its scripts wake up), every script is stripped, rigid bodies are made kinematic and sounds and particles stopped, and only then is the copy moved into the scene. A prefab that keeps a script the framework can't remove is refused. For real furniture the player can use and the build menu knows, use `FurnitureBuilder` ([world.md](world.md)).

## Placing

```csharp
using ObenFramework.World.Props;

public override void OnLoad()
{
    ModelRegistry.Load(this, "crate", "assets/models/crate.obj",
        new ModelOptions { Pivot = ModelPivot.BottomCenter, Collider = ModelCollider.Box });

    WorldProps.OnUse(this, "open-crate", use =>
        Log.Info($"{use.PropId} used, data {use.Data}"));

    GameEvents.WhenWorldReady(this, scene =>
    {
        if (scene != "Interior Player Tenement" || WorldProps.Get(this, "crate1") != null)
            return;
        WorldProps.Place(this, "crate1", new Vector3(3.2f, 0f, -1.5f), Quaternion.identity,
            WorldPropSource.Model("MyMod/crate"),
            new WorldPropOptions { InteractKey = "open-crate", Title = "Crate", Data = "loot:3" });
    });
}
```

- `Place(owner, propId, scene, position, rotation, scale, source, options)` records the prop for `scene` and builds it when that scene is loaded; for another scene it is only recorded. The short overload places in the active scene at scale one. Placing an id again replaces the old prop. The live object is returned (null when the scene isn't loaded or the prop was refused).
- `Move(owner, propId, position, rotation)`, `Remove(owner, propId)`, `Clear(owner)` change or drop the mod's own props. A prop placed with `IsStatic` can't move; place it again.
- `Get(owner, propId)` is the live object or null. `List(owner, scene?)` and `All(scene?, modId?)` give `WorldPropInfo` records (kind, source, pose, options, live object).
- `Spawned` is raised whenever a prop is built, placed or restored: `WorldProps.Spawned.Subscribe(this, info => ...)`.
- A refused call returns null or false, logs the reason once and leaves it in `WorldProps.LastProblem`. Reasons: no game loaded (main menu), the game is saving or loading, a pose that isn't finite or a scale outside 0.01 to 100 per axis, an interact key without a collider, more than 256 props for one mod, a model that isn't loaded, furniture without a prefab, an item without a world model.

### Making a prop usable

Give it an `InteractKey` (and a collider, the default) and register what happens with `WorldProps.OnUse(owner, key, handler)`. The player then sees the game's own prompt (`Title`, or the prop id) and the interact key runs the handler with a `WorldPropUse` (mod, key, prop id, scene, data, object). The prompt is the game's `Interactable` without a GUID, so the save system never writes it; every part of the prop with a collider gets one, moved to a layer the game's interact ray can use. The handler registration ends when the mod unloads; props placed before the handler is registered get their prompt as soon as it is.

`Data` is up to 256 characters of free text kept with the prop, handy to tell props with the same key apart.

## What happens when

| Moment | Props |
| --- | --- |
| World ready (`GameEvents.WorldReady`) | every recorded prop of the scene whose mod is loaded is built |
| Level change, main menu | the live props are destroyed with the scene; the records stay |
| Save | the framework's `props` file is written with the other per-save mod data ([saves.md](saves.md)) |
| Load | the records are read from the save before the world is restored, then built at world ready |
| A mod unloads (or hot reloads) | its props leave the world, and so do props built from its models |
| A mod loads mid-game | the scene's props are built again; models that load in the background are retried once a second for 30 seconds |
| The framework unloads | every live prop is destroyed; the records are handed to the next copy |

Props of mods that aren't installed stay in the save untouched and come back when the mod is installed again. Limits: 256 props per mod, 1024 in the world at once, ids up to 64 characters, scene names up to 128.

The records live in `<save>/mods/ObenFramework.props.json`, never in the game's own files, so a save loads fine without the framework.

## Dev bridge

| Command | Parameters | |
| --- | --- | --- |
| `props.list` | `scene?`, `mod?` | every recorded prop and whether it is live |
| `props.spawn` | `id`, `kind?` (cube, sphere, cylinder, plane, model, furniture, item, light, spot, anchor), `source?`, `pos?`, `yaw?`, `scale?`, `scene?`, `collider?`, `static?`, `interact?`, `title?`, `data?`, `color?`, `range?`, `intensity?`, `mod?` | place a prop (owned by DevBridge unless `mod` names another loaded mod) |
| `props.move` | `id`, `pos`, `yaw?`, `mod?` | move one |
| `props.remove` | `id` or `all=true`, `mod?` | remove one or all of a mod's |

```sh
oben call props.spawn id=test kind=cube scale=0.5
oben call props.spawn id=chair kind=furniture source="Red Metal Chair"
oben call props.list
oben call props.remove all=true
```
