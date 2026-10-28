# Animals

`ObenFramework.Animals` covers the animals in the scene the player is in (cats, chickens and chicks, pigs, rats): reading them, making them do things, taming them for good, breeding pens and rat traps. NPCs are in [npcs.md](npcs.md).

Add `"ObenFramework"` to your mod's `dependencies` and `using ObenFramework.Animals;`.

## Reading

```csharp
foreach (var a in Animals.All())
    Log.Info($"{a.Kind} {a.Id} at {a.Position}: {a.State}, tame {a.Tame}, health {a.Health}/{a.MaxHealth}");

var cat = Animals.Nearest(kind: AnimalKind.Cat, maxDistance: 10f);   // from the player
Animals.Died.Subscribe(this, a => Log.Info($"a {a.Kind} died"));
```

An `AnimalRecord` is a copy: `Id` (the object's instance id, for this session), `Kind`, `Name`, `Guid` (the save id its stats are kept under, empty for animals the game does not save), `Position`, `Tame`, `Following` (follows the player), `Running`, `State` (`Default`, `Attacking`, `Eating`, `Sleeping`, `Sitting`), `Health`/`MaxHealth`, `Hunger`/`MaxHunger`, `Age`/`FullGrownAge` (a chick becomes a chicken at its full grown age), and `Live`, the game's `Animal`. Reads look the scene over, so call them when something happened, not every frame. `Animals.Count` and `Animals.Limit` are the game's own count and cap (eggs stop hatching above it).

`Animals.Damage(id, amount)` hurts an animal the way a hit does: it flees, a tame one turns wild, and it dies when its health runs out. `Died` is raised just before the object goes, for every death (killed, starved, a rat trap).

## Making them do things

```csharp
AnimalActions.Sit(cat.Id);
AnimalActions.Run(cat.Id, seconds: 5f);
AnimalActions.SleepUntilWoken(this, cat.Id);       // held by your mod until Wake or unload
AnimalActions.Hunt(this, cat.Id, ratTransform);    // the target needs stats: the player, an NPC, an animal
AnimalActions.StopHunt(cat.Id);
AnimalActions.Wake(this, cat.Id);
```

These go through the game's own animal code. `Sit` and `Sleep` last as long as the game makes them (5 to 50 seconds, a minute or two). `SleepUntilWoken` and `Hunt` are held by your mod: they end when it unloads or the level changes, and an animal one mod keeps asleep refuses every other mod's orders until it is woken (a hit still wakes it). A well fed animal may refuse to hunt, as in the game.

## Taming

```csharp
AnimalTames.SetTame(this, cat.Id, true);    // does not bite the player, can follow them
AnimalTames.SetTame(this, pig.Id, false);
AnimalTames.Forget(this);                   // drops your mod's choices from the save
```

The game keeps an animal's tame flag in its stats but never puts it back on an animal of the scene after a load, so a choice made here goes into the framework's per-save file (`mods/ObenFramework.animals.json`) and is applied again whenever that scene is ready, as long as your mod is installed. When two mods set the same animal, the last choice wins. Animals without a save id (spawned ones that are not saved) are tamed for the session only, and so are two animals that share one save id. A hit still turns a tame animal wild, as in the game. Up to 1024 choices per mod.

## Breeding pens and rat traps

```csharp
foreach (var pen in Breeding.Breeders())
    Log.Info($"{pen.Address}: {pen.Animals}/{pen.Places}, {pen.ChancePerHour:P0} per hour");

Breeding.Bred.Subscribe(this, b => Log.Info($"{b.ItemName} born in {b.Breeder}{(b.CatchUp ? " while away" : "")}"));
Breeding.TrapSprung.Subscribe(this, t => Log.Info($"trap at {t.Position}: {t.Catch}"));
```

A breeding pen is the game's `AnimalBreeder` on a growing station: once per game hour each pair of grown animals may produce young (an item placed in a free spot). `Bred` reports each newborn; `CatchUp` is true for young the game adds after a load to make up for the hours the player was away. `TrapSprung` reports a rat trap snapping shut with what it caught (`Rat`, `GlowingRat` or `Empty`). Pens and traps are named by their `ObjectAddress` string.

## The Osrat

`Osrats` reads the player's handheld signal finder: `Strength` (the steps it shows), `Signal`, `IsBeeping`, `IsEquipped` and `IsDisturbed`. All zero or false outside a level.

## Dev bridge

```sh
oben call animals.list
oben call animals.list kind=cat
oben call animals.act id=12345 action=sit
oben call animals.act id=12345 action=tame
oben call animals.act id=12345 action=hunt          # the player
oben call animals.breeders
```

`animals.act` takes `sit`, `sleep`, `sleepHold`, `wake`, `run` (`seconds`), `hunt`, `stopHunt`, `tame`, `wild` and `damage` (`amount`). Holds and tame choices made from the bridge belong to DevBridge. See [../dev-tools.md](../dev-tools.md).
