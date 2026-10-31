# Environment: flags, settings, sky, screen effects, power, hazards and fixtures

`ObenFramework.Env` covers the world around the player: the game's global flags, the player's options, the sky, full-screen effects, power devices, hazards, radiation, water, weather details, wall coverings and fixtures such as lanterns, tanks, flood gates, showers and saunas. Add `"ObenFramework"` to your mod's `dependencies` and `using ObenFramework.Env;`.

The namespace is `Env`, not `Environment`, so it does not hide `System.Environment` in code that sits in the framework's namespaces. The files live in `src/ObenFramework/Environment/`.

Events follow the rules in [world.md](world.md): subscribe with an owner, nothing runs per frame while nobody listens, nothing is raised while a save loads, and payloads carry a `ChangeSource` so a mod can tell its own changes from the game's and the player's.

Every change to the world from code (a flag set for good, a tank filled, a fixture or hazard switched, a dose set, a shake or fade) is refused (false, nothing changed) while a save is written or loaded, so a save never holds a half-made change. The held overrides in the table below are not changes to the world and are always accepted. Each class with events has an `IsAvailable` that is false when its hooks failed to go on (a game update renamed what they hook), so its events stay silent; for flags that is `GlobalFlags.HooksAvailable`, since `GlobalFlags.IsAvailable` says whether the scene has flags at all.

## Overrides belong to a mod

Several things here are *held*: a mod sets them, they stay while the mod wants them, and they go away when the mod clears them or unloads (a hot reload included). When several mods hold the same thing the newest one shows, and the older one comes back when the newest lets go. The game's own value is kept underneath the whole time and comes back at the end.

| Held | Set | Clear | Notes |
|---|---|---|---|
| A forced flag | `GlobalFlags.Force(this, key, on)` | `GlobalFlags.Release(this, key)` | never written into the save; false when the hooks that keep it out are off |
| Field of view | `GameSettings.OverrideFov(this, 75)` | `GameSettings.ClearFov(this)` | options file untouched |
| Brightness | `GameSettings.OverrideBrightness(this, 140)` | `ClearBrightness(this)` | options file untouched |
| Mouse sensitivity | `GameSettings.OverrideMouseSensitivity(this, 600)` | `ClearMouseSensitivity(this)` | options file untouched |
| Sky parts | `WorldSky.Set(this, new SkyOverride { ... })` | `WorldSky.Clear(this)` | lasts across level changes, off on the main menu |
| Vignette | `ScreenFx.Vignette(this, color, strength, fade)` | `ScreenFx.ClearVignette(this, fade)` | own overlay, below the HUD |
| Blur | `ScreenFx.Blur(this, amount, fade)` | `ScreenFx.ClearBlur(this, fade)` | the game's menu blur stays underneath |
| Black screen | `ScreenFx.FadeToBlack(this, speed)` | `ScreenFx.FadeFromBlack(this, speed)` | dropped on a level change |
| Weather zone | `WeatherZones.Set(this, zoneId, weather, blend)` | `WeatherZones.Clear(this, zoneId)` | kept by zone id, applied whenever its level loads |

## Global flags

The game keeps named on/off switches with an optional value in `GlobalState`, saved once for the whole game. Story scripts, the tenement and quests set them; a flag that was never set reads as off.

```csharp
bool met = GlobalFlags.Get("init_player_bank_account");
GlobalFlags.Set("mymod_intro_done");                // for good, saved with the game
GlobalFlags.Force(this, "some_story_flag", false);  // until Release or unload, never saved
GlobalFlags.FlagChanged.Subscribe(this, c => Log.Info($"{c.Key}: {c.WasActive} -> {c.Active} ({c.Source})"));
```

`Set` keeps a flag's stored value; `Set(key, active, data)` replaces it. Both return false outside a gameplay scene. `Remove` deletes a flag your mod made up (not allowed on a forced one). While a flag is forced the game can still write it: the write is remembered underneath and shows once the force ends. The game only collects `GlobalState` in its save step and writes the file at the end of `SaveController.SaveGame`, so the values underneath go in when it collects the flags and the forces come back once the file is written: a save made during a force holds the game's own value.

## Game settings

```csharp
var display = GameSettings.Display;     // Width, Height, Mode, VSync, Fov, Brightness, Language, ...
var graphics = GameSettings.Graphics;   // Preset, Custom, ShadowQuality, Ssao, Antialiasing, PostFx, ...
var controls = GameSettings.Controls;   // MouseSensitivity, InvertX, InvertY, ToggleSprint, ToggleCrouch
var volume = GameSettings.Volume;       // SpeakerMode, Master, Sound, Music, Nasty
GameSettings.Changed.Subscribe(this, area => ...);   // Display, Graphics, Controls, Volume, Hints
```

The options objects exist from the main menu on. `Changed` fires when an options file is written (a slider moved, the menu's apply button, a `Save*` call); one apply counts once. The overrides in the table above change only what the game shows. The `Save*` methods (`SaveFov`, `SaveBrightness`, `SaveMouseSensitivity`, `SaveHintsEnabled`) write the player's options like the menu does; use them only for an explicit settings feature.

## Sky

```csharp
WorldSky.Set(this, new SkyOverride { FogEnabled = true, FogDensity = 0.04f, FogColor = new Color(0.4f, 0.45f, 0.5f), SunIntensity = 0.3f });
SkyState now = WorldSky.Current;   // fog, ambient, exposure, stars, clouds, sun: overrides included
WorldSky.Clear(this);
```

Parts left null keep the game's value; each part comes from the newest mod that sets it. Overrides are written every frame after the weather and time of day have written theirs; the game's sky writers (Azure's environment, the interiors' sun, the lights controller, the underwater effect) are wrapped so whatever they write stays the value underneath. Colours are ordinary colours, the same kind `Current` reads back.

## Screen effects

```csharp
ScreenFx.Shake(0.5f, 0.2f);                                      // seconds, metres
ScreenFx.Vignette(this, new Color(0.5f, 0f, 0f, 0.8f), 0.7f, 1f);    // red edges, fading in over a second
ScreenFx.Blur(this, 0.6f, 0.5f);
ScreenFx.FadeToBlack(this, 0.1f);  /* ... */  ScreenFx.FadeFromBlack(this, 0.1f);
ScreenFx.SetDeathMessage("Ate the wrong mushroom.");
```

Fades run in real time, so they also work in the pause menu, on a component that is only enabled while something fades. Vignette colours are sRGB and converted for the game's linear rendering. A held blur stays on through the inventory and the pause menu, and the game's own menu blur works as before once it is cleared. `Shake`, `FadeToBlack`, `FadeFromBlack` and `SetDeathMessage` return false while a save is written or loaded, when the game runs the screen itself (a release refused that way keeps the hold; ask again afterwards).

## Power devices

```csharp
foreach (var device in PowerDevices.All())                       // appliances, generators, consumers, batteries
    Log.Info(PowerDevices.Describe(device).Name);
PowerDevices.SetOn("3979b050-a3fb-42c7-9f1c-7ef6924f56e6", false); // a camp fire in the Bazaar
PowerDevices.DeviceChanged.Subscribe(this, c => ...);            // SwitchedOn/Off, FuelRanOut, BatteryStarted/Stopped
```

In 0.5.13 power runs through `PowerGrid`s: crafting stations with a power source (appliances; `Generator`s make power), `PowerGridListener` consumers in the scene, and `BatteryPowerSource`s that run their relays while they have charge. Stations that are always on and on no grid (flower pots, compost) are not listed, and `Find` doesn't find them either. An appliance switched on with no capacity or fuel stays switched on and starts when it can, like with the player's switch. Grids themselves are in [world.md](world.md): `PowerGrids.SetEnabled`, `SetBaseCapacity`.

A battery feeds its outputs whenever it has charge; its switch only decides whether it drains. `BatteryStarted` and `BatteryStopped` follow the charge (found when the level starts or the switch is used, run out while draining). A switch that also starts or stops it raises `SwitchedOn`/`SwitchedOff` first and the start or stop after, with the new switch already stored.

`Blackouts` is about the player passing out (`BlackoutTrigger`, the casino and a story flat), not power cuts: `Started`, `IsRunning`, `ScheduleWakeUp`, `WakeUp`. `ScheduleWakeUp` sets the wake-up after the level a blackout carries the player to has loaded (allowed during that level change); `WakeUp` wakes the player with the pose and delay the blackout chose for a wake-up by hand, not the scheduled ones.

## Hazards, radiation and water

```csharp
foreach (var h in Hazards.All()) { /* RadiationZone, DamageZone, RadiationSource, Detector */ }
Hazards.SetEnabled(id, false);
Hazards.Entered.Subscribe(this, e => Log.Info($"player walked into {e.Kind} {e.Id}"));

float dose = Radiation.Dose;          // mSv; Radiation.SetDose, AddDose
float rate = Radiation.RateHere;      // mSv per hour at the player's head, zones included
bool? on = Radiation.DosimeterOn;     // null without one in hand

WorldWater.UnderwaterChanged.Subscribe(this, under => ...);
WorldWater.ResetBreath();
WorldWater.SetVolume(id, drownDamage: 0f);
```

`BlastSites` lists the mines' dynamite cones (dynamite in, linked cones, already blown), sets one off with `Detonate` (only when it and every linked cone have dynamite, like its fuse) and raises `Detonated` per cone. Going by the scene files, 231 of 0.5.13's cones are in `Open Sewer Mines`, a few in `Interior City Gates` and one in the Bazaar bank.

Zones a mod switches are back to the scene's state after the level loads again; radiation sources save their switch with the game. A damage zone of the radiation type adds a radiation zone component to itself; `Entered` reports it once, as the damage zone. A radiation zone switched off by `SetEnabled` stops adding to the dose at once, also while the player stands in it: the game's own switch (`TriggerRadiation.Enable`) only keeps new visitors out, so the framework switches the component off too. `RateHere` leaves out zones made to never hurt (`dontDamagePlayer`) once the dose has reached the damage threshold, as the game does. `SetEnabled`, `SetDose`, `AddDose` and `SetVolume` return whether the change took.

## Weather details and zones

`Clock.GameWeather` picks the weather. `WeatherConditions` reads what it is doing (`Temperature`, `RainIntensity`, `SnowIntensity`, `WindSpeed`, `IsThunderActive`, `SetThunder`) and raises `WeatherChanged` when the profile is switched. `WeatherZones` lists the scene's local weather zones (the time of day's own sky zone is left out) and holds per-mod overrides of their weather.

## Surfaces

`Surfaces.All()` lists the walls, floors and ceilings that take wallpaper, paint or panels, with the item and variant on them. `Surfaces.SetCovering(id, itemId, skin)` puts one on without using the player's items (the same fit checks as by hand), `Surfaces.Clear(id)` scrapes it off (true when it is bare afterwards), `CoveringItems()` lists the items, `SurfaceChanged` reports changes.

## Fixtures

| Class | Read | Change | Event |
|---|---|---|---|
| `Lanterns` | lit, seconds left | `Light`, `Extinguish` | `LanternChanged` (Lit, Extinguished, BurntOut) |
| `Tanks` | liquid item, litres, capacity, lock, supply | `Fill(tank, item, litres)`, `Empty`, `SetLocked` | `TankChanged` (liquid and litres before and after) |
| `FloodGates` | open, moving, powered | `SetOpen`, `SetPowered` | `GateChanged` |
| `Showers` | on, running | `SetOn` | `ShowerChanged` |
| `Saunas` | on, temperature, steam, passive | `SetOn`, `ThrowWater` | `SaunaChanged` (with the temperature and steam) |
| `MultiStateLights` | on, state, states | `SetOn`, `SetState` | `StateChanged` |

Every change returns whether it took (true when the fixture already was that way).

Liquids are items of the `Liquid` category (Clean Water 10840, Dirty Water 12980, Diesel 15900). Taps and sinks fed from elsewhere have a negative capacity and refuse a fill or an empty (they throw); fill what feeds them instead (`WorldWater.Supply`).

`TankChanged` sees the game's changes too: a pour from the tank's window (`Source` is `LocalPlayer`), a running shower draining the tank it draws from, fillers and the janitor. The game moves a level in small steps, so the steps are gathered and reported once the level settles (within half a second), or every two seconds while it keeps moving (a shower that runs); `Fill` and `Empty` report at once. Lock changes come through `Locks.Changed` ([world.md](world.md)).

`Lanterns.Extinguish` also stops the candle's timer. The game's own blowing out leaves it running, and when it runs out it puts out whatever candle was lit since (TODO.md, LanternStaleTimer). Lit, Extinguished and BurntOut follow the candle's timer, so a candle put out early by that old timer reports Extinguished.

A flood gate whose power is cut on the way stops where it is and no longer reports `Moving`; with power back the game either finishes at once or turns it round (`GateChanged` reports the turn). `SetOpen` refuses a gate without power. `ThrowWater` refuses a passive sauna and a throw while the last one settles (a second and a half); water only makes steam once the stove is over 60 degrees.

Where they are, going by the scene files of 0.5.13: saunas in Interior Tenement A (five), Kolhola (two), Interior Bazaar Sauna (four) and a dozen other interiors; flood gates only in Interior Tenement B (eight); lanterns only in Interior Mines Potato Mine and Interior Bazaar Slum Barrack House 3 (both jack-o'-turnips); showers in most interiors and one in Open Sewer Bazaar, which draws from its own barrel. No level has a light with several states; they come with furniture.

## Dev bridge

`flags.list/get/set`, `settings.get/override`, `sky.get/override/clear`, `screenfx`, `power.devices/set/grids/grid`, `hazards.list/set`, `blasts.list/detonate`, `radiation.info`, `water.info/set`, `weather.conditions/zones/zone`, `surfaces.list/set`, `fixtures.list/set`, and `env.events` (the events of this page and the grids' since recording started). Changes answer with `ok`. Overrides made through the bridge belong to the bridge, or to the mod named in `mod`.
