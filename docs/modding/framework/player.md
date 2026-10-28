# Player

The `ObenFramework.Player` namespace reads and changes the player: where they are, their stats, health and death. Movement speed, sleeping and waiting, skills, effects, seats, gear, notes, jobs and achievements are in [player-actions.md](player-actions.md). Add `"ObenFramework"` to your mod's `dependencies` and `using ObenFramework.Player;`. The look and survival parts live in `ObenFramework.Player.Look` and `ObenFramework.Player.Survival`.

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

## The player's look

`using ObenFramework.Player.Look;`. A look is a `LookData`: `male`, `advanced`, 23 body and face sliders (`height`, `shape` from -1 muscular to 1 heavy, the rest 0 to 1) and the worn items by **asset name** (`head`, `hat`, `hair`, `glasses`, `beard`, `jacket`, `shirt`, `pants`, `leggings`, `socks`, `shoes`). Never by index: the order the game loads its wearables in is not stable. An empty name is the slot's "none" item.

```csharp
LookData look = PlayerLook.Capture();             // null without a player
string json = look.ToJson();                      // for saves and logs
byte[] wire = look.ToBytes();                     // about 50 bytes plus the names, for the network
LookData back = LookData.FromBytes(wire);         // throws FormatException on bad data

PlayerLook.Apply(puppetAppearance, back);         // dress any Appearance
PlayerLook.ApplyToPlayer(back);                   // or the local player, until the next level load
PlayerLook.Release(puppetAppearance);             // when the puppet is destroyed

PlayerLook.Changed.Subscribe(this, l => Send(l.ToBytes()));
```

What to know:

- `Capture()` reads the live values from the player's `Appearance`, not from its `Character` asset. In a loaded save the player's `createdCharacter` can be the shared "Random NPC" asset, which says nothing about how the player looks. The player's `Appearance` is found by searching the player's children (skipping the first-person arms) because `PlayerLocator.playerAppearance` is empty in some scenes. `PlayerLook.PlayerAppearance` returns it.
- `Apply` never writes to a shared asset. The game builds a body from a `Character`, and the ones it hands out are shared: the character editor writes into "Created Character" and all NPCs without their own share "Random NPC" (which also randomises itself and writes the result back). `Apply` gives the target a private `Character` and destroys it on the next `Apply`, on `Release` or when the framework unloads. Only Characters it created are ever destroyed.
- If the target has not run its `Start` yet (a puppet instantiated inactive) the Character is just handed over and the game applies it when the puppet starts. Otherwise the body is rebuilt immediately. A name the game does not know falls back to the slot's empty item; a head that is missing falls back to the first head of that gender.
- The wearable assets are loaded once per folder (`Resources.LoadAll`) and cached; the game does 14 loads for every `Appearance` it creates. `PlayerLook.Options("hat", male)` lists the valid names of a slot, handy to validate a look received from another machine. `PlayerLook.SlotNames` lists the slots.
- `Changed` fires when the character editor edits the look (at the main menu too), when `Apply` dresses the player, and when the player's body is rebuilt with a look different from the last one announced. Several edits within a quarter second come as one event, and the handler gets its own copy of the `LookData`.
- `PlayerLook.Keep(this, look)` dresses the player and keeps them dressed across level loads (the game builds a new body for every level and would otherwise bring back the player's own look). It is put on before the new body starts where it can, so the old look doesn't flash up. If several mods keep a look the newest wins and the previous one returns when it goes; dispose the result (or unload the mod) and the player's own look comes back at once. `PlayerLook.KeptLook` is the look being kept. The game doesn't save the player's look, and neither does this.
- `LookData.SameAs(other)` compares two looks; `LookCodec` holds the JSON and binary forms. The binary form is versioned and counts its fields, so a build that adds a slider can still be read by an older one.

## Survival

`using ObenFramework.Player.Survival;`. Everything takes your mod as the owner, so a reload or unload removes it. None of it runs per frame.

**Threshold callbacks.** Fire once each time a stat crosses a value (not while it stays beyond it). Stats are sampled on the clock's minute tick, which is how often the game updates most of them. The first sample after a load only sets the starting point.

```csharp
StatThresholds.OnCrossed(this, PlayerStat.Hunger, 80f, c =>
    Log.Info($"Hunger {(c.Rising ? "rose to" : "fell to")} {c.Value:0}"), CrossDirection.Rising);
```

**Drain multipliers.** Scale how fast hunger, thirst, hygiene and the four cravings change (`PlayerDrain`). Each mod sets its own, and they combine: two mods that halve hunger make it a quarter; when one goes away the other stays.

```csharp
var slowHunger = StatDrain.Set(this, PlayerDrain.Hunger, 0.5f);      // half as hungry
StatDrain.Set(this, PlayerDrain.Thirst, 1f, offsetPerHour: 2f);      // a little more thirst per hour
slowHunger.Dispose();                                                // take it back
```

The game's rate (already adjusted for the difficulty) is multiplied by the product of all multipliers, then the offsets are added. Setting the same drain again from the same mod replaces the earlier value; pass `key:` when one mod needs several. `StatDrain.Active` lists the adjustments, `Multiplier(drain)` the combined multiplier, and the dev bridge shows them as `player.drain`. A multiplier of 0 stops the drain. Hygiene's game rate is negative (the player gets dirtier), so a multiplier below 1 slows that.

**Eating, drinking and damage.** Handlers can look at, scale or cancel. A cancelled item is not used at all: no animation, no cooldown, no sip taken from a bottle, nothing eaten.

```csharp
ConsumeHooks.Intercept(this, c =>
{
    if (c.ItemId == forbiddenId) c.Cancel();           // c.Data is the game's ItemData
});

DamageHooks.Intercept(this, d =>
{
    if (d.Reason == DamageTypeNames.Fall) d.Amount *= 0.5f;   // half fall damage
    if (d.Reason == DamageTypeNames.Bleed && d.Amount < 1f) d.Cancel();   // ignore scratches
});
```

`ConsumeContext` has `Data`, `ItemId`, `ItemTitle` and `Cancel()`; for a drink in a container the item is the container. `DamageContext` has `Original`, `Amount` (settable), `Reason`, `Silent` and `Cancel()`. For bleeding the amount is the bleeding added. A handler that damages the player is not called for its own hit. Handlers run in the order they were added, each in its own try/catch, and a cancel sticks whatever later handlers do. `PlayerEvents.Damaged` reports what was really lost after your changes.

**Breakdowns.** When effective depression reaches 100 the game picks one thing: die, hurt themselves, eat mushrooms or drink. `BreakdownHooks.Intercept(this, b => ...)` sees each (`b.Kind` is `Suicide`, `SelfHarm`, `Mushrooms` or `Alcohol`) and can `Cancel()` it; the game then tries its next act, which you can cancel as well. Only the automatic reaction is covered.

**More events.** `SurvivalEvents.PassedOut` fires as the player passes out (exhaustion or drink, not sleep or arrest). `SurvivalEvents.Consumed` fires with the item id and title after a food, drink or other consumable took effect. Death is `PlayerEvents.Died`.

## Dev bridge commands

| Command | Does |
| --- | --- |
| `player.get` | Position, yaw, pitch, scene, state flags and every stat. |
| `player.teleport x= y= z= [yaw=] [pitch=] [ground=true]` | Teleport (also `pos=[x,y,z]`). |
| `player.stat name= [value=] [mode=set\|add]` | Read or change one stat. |
| `player.stats` | Every stat with group, range, description and value. |
| `player.heal [amount=] [needs=true]` | Full heal, or restore an amount; optionally clear needs. |
| `player.godmode [enabled=]` | Read or set god mode. |
| `player.look [bytes=true]` | The player's captured look as JSON, optionally the compact byte form as base64. |
| `player.drain` | Every drain multiplier mods have set, who set it, and the combined result per need. |

The commands for movement, rest, skills and the rest are listed in [player-actions.md](player-actions.md#dev-bridge-commands).

```sh
oben call player.stat name=thirst value=0
oben call player.teleport x=12 y=1 z=-3 yaw=180 ground=true
```
