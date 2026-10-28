# Environment: flags, settings, sky, screen effects, power, hazards and fixtures

`ObenFramework.Env` covers the world around the player: the game's global flags, the player's options, the sky, full-screen effects, power devices, hazards, radiation, water, weather details, wall coverings and fixtures such as lanterns, tanks, flood gates, showers and saunas. Add `"ObenFramework"` to your mod's `dependencies` and `using ObenFramework.Env;`.

The namespace is `Env`, not `Environment`, so it does not hide `System.Environment` in code that sits in the framework's namespaces. The files live in `src/ObenFramework/Environment/`.

Events follow the rules in [world.md](world.md): subscribe with an owner, nothing runs per frame while nobody listens, nothing is raised while a save loads, and payloads carry a `ChangeSource` so a mod can tell its own changes from the game's and the player's.

## Overrides belong to a mod

Several things here are *held*: a mod sets them, they stay while the mod wants them, and they go away when the mod clears them or unloads (a hot reload included). When several mods hold the same thing the newest one shows, and the older one comes back when the newest lets go. The game's own value is kept underneath the whole time and comes back at the end.

| Held | Set | Clear | Notes |
|---|---|---|---|
| A forced flag | `GlobalFlags.Force(this, key, on)` | `GlobalFlags.Release(this, key)` | never written into the save |
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

`Set` keeps a flag's stored value; `Set(key, active, data)` replaces it. `Remove` deletes a flag your mod made up (not allowed on a forced one). While a flag is forced the game can still write it: the write is remembered underneath and shows once the force ends. Forced values are swapped out while `GlobalState` serialises, so a save made during a force holds the game's own value.

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

Fades run in real time, so they also work in the pause menu, on a component that is only enabled while something fades. Vignette colours are sRGB and converted for the game's linear rendering.

## Power devices

```csharp
foreach (var device in PowerDevices.All())                       // appliances, generators, consumers, batteries
    Log.Info(PowerDevices.Describe(device).Name);
PowerDevices.SetOn("3979b050-a3fb-42c7-9f1c-7ef6924f56e6", false); // a camp fire in the Bazaar
PowerDevices.DeviceChanged.Subscribe(this, c => ...);            // SwitchedOn/Off, FuelRanOut, BatteryStarted/Stopped
```

In 0.5.13 power runs through `PowerGrid`s: crafting stations with a power source (appliances; `Generator`s make power), `PowerGridListener` consumers in the scene, and `BatteryPowerSource`s that run their relays while they have charge. Stations that are always on and on no grid (flower pots, compost) are not listed. An appliance switched on with no capacity or fuel stays switched on and starts when it can, like with the player's switch. Grids themselves are in [world.md](world.md): `PowerGrids.SetEnabled`, `SetBaseCapacity`.

`Blackouts` is about the player passing out (`BlackoutTrigger`, the casino and a story flat), not power cuts: `Started`, `ScheduleWakeUp`, `WakeUp`.

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

`BlastSites` lists the mines' dynamite cones (dynamite in, linked cones, already blown), sets one off with `Detonate` (only when it and every linked cone have dynamite, like its fuse) and raises `Detonated` per cone.

Zones a mod switches are back to the scene's state after the level loads again; radiation sources save their switch with the game. A damage zone of the radiation type adds a radiation zone component to itself; `Entered` reports it once, as the damage zone.

## Weather details and zones

`Clock.GameWeather` picks the weather. `WeatherConditions` reads what it is doing (`Temperature`, `RainIntensity`, `SnowIntensity`, `WindSpeed`, `IsThunderActive`, `SetThunder`) and raises `WeatherChanged` when the profile is switched. `WeatherZones` lists the scene's local weather zones (the time of day's own sky zone is left out) and holds per-mod overrides of their weather.

## Surfaces

`Surfaces.All()` lists the walls, floors and ceilings that take wallpaper, paint or panels, with the item and variant on them. `Surfaces.SetCovering(id, itemId, skin)` puts one on without using the player's items (the same fit checks as by hand), `Surfaces.Clear(id)` scrapes it off, `CoveringItems()` lists the items, `SurfaceChanged` reports changes.

## Fixtures

| Class | Read | Change | Event |
|---|---|---|---|
| `Lanterns` | lit, seconds left | `Light`, `Extinguish` | `LanternChanged` (Lit, Extinguished, BurntOut) |
| `Tanks` | liquid item, litres, capacity, lock, supply | `Fill(tank, item, litres)`, `Empty`, `SetLocked` | none |
| `FloodGates` | open, moving, powered | `SetOpen`, `SetPowered` | `GateChanged` |
| `Showers` | on, running | `SetOn` | `ShowerChanged` |
| `Saunas` | on, temperature, steam, passive | `SetOn`, `ThrowWater` | `SaunaChanged` |
| `MultiStateLights` | on, state, states | `SetOn`, `SetState` | `StateChanged` |

Liquids are items of the `Liquid` category (Clean Water 10840, Dirty Water 12980, Diesel 15900). Taps and sinks fed from elsewhere have a negative capacity and refuse a fill; fill what feeds them instead (`WorldWater.Supply`).

## Dev bridge

`flags.list/get/set`, `settings.get/override`, `sky.get/override/clear`, `screenfx`, `power.devices/set/grids/grid`, `hazards.list/set`, `blasts.list/detonate`, `radiation.info`, `water.info/set`, `weather.conditions/zones/zone`, `surfaces.list/set`, `fixtures.list/set`. Overrides made through the bridge belong to the bridge, or to the mod named in `mod`.
