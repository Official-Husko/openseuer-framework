# Player

The `ObenFramework.Player` namespace reads and changes the player: where they are, their stats, health and death. Add `"ObenFramework"` to your mod's `dependencies` and `using ObenFramework.Player;`.

Everything is safe to call at the main menu: reads return null, false or empty, and writes throw an `InvalidOperationException` that says there is no player. Nothing is cached across scenes, because the game rebuilds all its player objects on every level load.

For how the game's player works underneath see [../../game/player.md](../../game/player.md).

## Finding the player

```csharp
if (PlayerRef.Exists)
{
    Vector3 feet = PlayerRef.Position.Value;
    float yaw = PlayerRef.Yaw.Value;        // body, degrees
    float pitch = PlayerRef.Pitch.Value;    // camera, positive looks down
    string scene = PlayerRef.SceneName;
}
Transform body = PlayerRef.Body;            // null without a player
```

`PlayerRef.Locator`, `.CameraTransform`, `.Rigidbody`, `.Velocity` and `.LookYaw` are there too, and `PlayerRef.Require()` returns the `PlayerLocator` or throws the standard error.

`PlayerState` holds the flags: `Godmode` (get and set), `IsDead`, `IsSeated`, `IsSleeping`, `IsCrouching`, `IsSwimming`, `IsFirstPerson`, `InputLocked` (something has disabled movement or keys) and `IsTransitioning` (a level change, load or arrest is running).

## Teleporting

```csharp
PlayerTeleport.To(new Vector3(10f, 1f, -4f));            // keep facing
PlayerTeleport.To(new Vector3(10f, 1f, -4f), yaw: 90f);  // face east, camera level
PlayerTeleport.To(markerTransform);                      // position and facing of any transform

if (PlayerTeleport.TryFindGround(wanted, out var floor))
    PlayerTeleport.To(floor);
```

What it does that the game's own `PlayerLocator.Teleport` does not:

- never copies the target's scale onto the player, so any marker object is fine,
- stands the player up from a chair or bed first and puts the body back under its normal parent,
- zeroes velocity (skipping kinematic bodies, which log a warning otherwise),
- turns the camera rig along with the body when you give a yaw, and keeps the position a save would write.

It refuses (throws) during a level change, load or arrest. `PlayerTeleport.CanTeleport(out reason)` tells you ahead of time. There is no collision or ground check, so use `TryFindGround` if you are not sure what is below. Use `GameEvents.WhenWorldReady` before teleporting right after a load.

## Stats

`PlayerStat` lists every stat. The names match the F1 console's `player_stats <name> set|add|subtract <amount>`, so what a player types and what a mod does agree.

```csharp
PlayerStat.Thirst.Value = 20f;                  // set
PlayerStat.Hunger.Add(-30f);                    // eat something
float drunk = PlayerStat.AlcoholInBlood.Value;

var stat = PlayerStat.Require("hygiene");       // by console name, throws with the valid names
foreach (var s in PlayerStat.All)
    if (s.TryGet(out float v)) Log.Info($"{s.Name} = {v}");

PlayerStatRates.HungerRate.Value = 5f;          // tuning rates live in PlayerStatRates
```

Each `PlayerStatRef` has `Name`, `Group`, `Description`, `Min`, `Max`, `Value`, `TryGet`, `Set` and `Add`. Writes are clamped to `Min` and `Max` (for example health 0 to 100) and the stat bars refresh afterwards. They go straight to the game's fields like the console does, so side effects of eating or drinking (cravings, skill XP) do not happen; use items for that.

Scales: health 0-100 (100 full), hunger and thirst -100 to 100 (100 starving), bladder and bowel 0-100, tiredness -100 to 100 (100 passes out), hygiene 0-100 (100 clean), depression 0-100 (100 worst), alcohol in blood above 15 shows drunk effects and above 100 passes out.

The groups are `health`, `needs`, `mind`, `body`, `alcohol`, `mushrooms`, `addictions` and `rates`.

## Health, damage, death

```csharp
PlayerVitals.Damage(15f, DamageTypeNames.Fall);   // the call falls and explosions use
PlayerVitals.Heal(25f);                           // now, capped at 100
PlayerVitals.HealOverTime(40f);                   // like medicine
PlayerVitals.FullHeal();                          // health, bleeding, queued healing
PlayerVitals.ClearNeeds();                        // the game's clear_needs
PlayerVitals.Kill();                              // like the console's kill
```

`Damage` plays the hurt effects and, at 0 health, calls the game's death. God mode blocks death but not the health loss. The game has no respawn: once dead, loading a save is the way out.

## Events

```csharp
PlayerEvents.Died.Subscribe(this, reason => Log.Info($"Died of {reason}"));
PlayerEvents.Damaged.Subscribe(this, hit => Log.Info($"-{hit.Amount} ({hit.Reason})"));
```

`Died` fires once per death (not when god mode blocks it). `Damaged` fires when the damage call actually removed health or added bleeding; healing does not raise it. Subscribe with `this` as the owner so a reload cleans up. Handlers run in their own try/catch.

## Dev bridge commands

| Command | Does |
| --- | --- |
| `player.get` | Position, yaw, pitch, scene, state flags and every stat. |
| `player.teleport x= y= z= [yaw=] [pitch=] [ground=true]` | Teleport (also `pos=[x,y,z]`). |
| `player.stat name= [value=] [mode=set\|add]` | Read or change one stat. |
| `player.stats` | Every stat with group, range, description and value. |
| `player.heal [amount=] [needs=true]` | Full heal, or restore an amount; optionally clear needs. |
| `player.godmode [enabled=]` | Read or set god mode. |

```sh
oben call player.stat name=thirst value=0
oben call player.teleport x=12 y=1 z=-3 yaw=180 ground=true
```
