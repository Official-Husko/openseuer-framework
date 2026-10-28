# Patching and the framework's parts

Two things keep one broken piece from taking everything else down: patch areas (`ObenFramework.Patching`) and the framework's parts (`ObenFramework.Modules`).

## Patch areas

A patch area is a group of a mod's Harmony patches that stand or fall together. When one patch of an area fails (usually because a game update renamed or removed the method it hooks):

- the area's other patches come off again and its later ones are skipped, so a feature never runs half patched,
- the failure is logged and shows on the Errors page,
- the rest of the mod keeps running.

`PatchAreas.IsAvailable(this, "checkout")` (or `PatchAreas.IsAvailable("ObenFramework", "Shops")`) tells whether an area still stands. `PatchAreas.All()`, `Of(modId)` and `Failed()` list them. Areas are forgotten when their mod unloads, so a hot reload tries again.

### Patch classes, area by area

The loader normally applies every `[HarmonyPatch]` class of a mod at once, and one failing class stops the whole mod from loading. To get areas instead, switch that off in mod.json and apply the classes yourself first thing in `OnLoad`:

```json
"autoPatch": false
```

```csharp
public override void OnLoad()
{
    ModPatches.ApplyClasses(this);  // returns how many classes failed
    ...
}
```

A class's area is its `[PatchArea("Name")]`, or else its namespace below the mod's root namespace (`MyMod.Shops` is `Shops`, classes in `MyMod` itself are `Main`). Classes with `[HarmonyPatchCategory]` are left to their `ModFeature` as before.

The framework does exactly this. Its areas are its namespaces (`Economy`, `World`, `Npcs`, `Player.Survival`...), with the shop patches in their own `Shops` area. Give a group of patch classes its own `[PatchArea]` when a namespace is too coarse.

### Patching from code

`ModPatches` patches through the mod's own Harmony instance, so the loader takes everything off at unload:

```csharp
// Method found by name; the name must be unique on the type unless the argument types are given.
ModPatches.Postfix(this, typeof(MarketShop), "Pay", null, typeof(MyHooks), nameof(MyHooks.AfterPay), area: "checkout");
// AddItems has three overloads, so the argument types pick one.
ModPatches.Prefix(this, typeof(ItemOperations), "AddItems",
    new[] { typeof(int), typeof(int), typeof(int), typeof(object[]), typeof(SlotController), typeof(bool), typeof(bool) },
    typeof(MyHooks), nameof(MyHooks.BeforeAddById));

// Or with the method and handlers in hand (private methods too):
var target = MethodFinder.Find(typeof(MarketShop), "Start");
ModPatches.Patch(this, target, postfix: MethodFinder.Handler(typeof(MyHooks), "AfterShopStart"), area: "checkout", priority: Priority.Low);
```

- `Prefix`, `Postfix`, `Finalizer`, `Transpiler` and `Patch` return false instead of throwing; the reason is in the log.
- Without an `area` a failed patch only fails itself. With one, it switches the area off.
- `MethodFinder.Find(type, name, args)` searches the type's own methods, public or not: `.ctor` and `.cctor` for constructors, `get_X`/`set_X` for properties. Without argument types an overloaded name is refused, so an overload added by an update is noticed instead of patching the wrong one.
- `ModPatches.DisableArea(this, area, reason)` switches an area off on purpose, for example when a check of your own shows the game changed.

## The framework's parts

`FrameworkMod.OnLoad` starts about 30 parts (events, saves, clock, shops, NPCs...) one by one. A part whose setup throws is logged and listed on the Errors page; the others still start. Each part's per-frame update runs in its own try/catch too, logged once per part.

```csharp
if (!FrameworkModules.IsAvailable("shops"))
    Log.Warn("Shop changes are off: the framework's shop part isn't working on this game version.");
```

`FrameworkModules.All` lists every part with `Started`, `Error`, `PatchArea`, `PatchesOk`, `IsAvailable`, `StartMs` and `UpdateError`. A part is available when its setup ran and its patch area still stands. The log's "Ready" line says which parts aren't fully working.

## Dev bridge

```sh
oben call framework.modules            # every part
oben call framework.modules failed=true
oben call patches.areas mod=ObenFramework
oben call patches.areas failed=true    # what got switched off, and why
oben call mods.list                    # "warning" shows a mod made for another game version
```
