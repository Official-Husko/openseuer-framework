# Player: movement, rest, skills and more

The rest of the player module, next to [player.md](player.md) (position, stats, health, look, survival). Everything here is in `ObenFramework.Player` (`using ObenFramework.Player;`). Like the rest of the framework:

- Reads are safe at the main menu and return null, false, 0 or an empty list.
- Anything a mod sets up takes the mod as owner (`this`) and goes away when the mod unloads; most also return an `IDisposable` to take it back earlier.
- Events are `GameEvent`s: `Subscribe(this, handler)`, each handler in its own try/catch.
- Multipliers from several mods combine by multiplying; when one mod removes its own, the others stay. Setting the same one again from the same mod replaces it (calling it from a slider is fine).

## Movement

```csharp
PlayerMovement.Set(this, MovementStat.Speed, 1.5f);   // every speed 50% faster
PlayerMovement.Set(this, MovementStat.Jump, 2f);      // jump twice as high
var slow = PlayerMovement.Set(this, MovementStat.Run, 0.5f, key: "injury");
slow.Dispose();                                        // or PlayerMovement.Remove(this, MovementStat.Run, "injury")
```

`MovementStat` is `Speed` (walk, run, swim, sneak, backwards and sideways together), `Walk`, `Run`, `Swim`, `Crouch` and `Jump`. A walk speed is base × Speed × Walk. Multipliers go from 0 to 10, and so does a stat's combined value. They work at the main menu too and are put on the new controller the game builds for every level, from the game's own values read once per controller; when the framework unloads the game's values go back.

`PlayerMovement.Current` and `.Base` give a `MovementInfo` (the speeds in m/s, jump height, `IsCrouching`, `IsWalking`, `IsSprinting`, `IsGrounded`, `ControlsDisabled`), or null without a player. `RunSpeed` includes the orange mushroom's boost. `Multiplier(stat)` is the combined value, `Active` lists every mod's entry. `Crouch()` and `StandUp()` work like the keys.

The cheat menu's move speed slider is one of these multipliers.

**Holding the player still.** `PlayerControls.Lock(this)` stops walking, the camera and the game's keys the way dialogue does (`showCursor: true` for a window the player clicks in); `PlayerControls.LockLook(this)` only stops the camera turning. Both return an `IDisposable`, are held per mod (the player moves again when the last one lets go), come back after a level load while held, and outlast the game's own camera toggles without undoing them. `Locked` and `LookLocked` tell whether a mod holds one.

## Sleeping and waiting

```csharp
PlayerSleep.Sleep();                       // until rested; false if the player isn't tired enough
PlayerSleep.Sleep(alarmSeconds: 8 * 3600); // with an alarm
PlayerSleep.Wait(3600, WaitKind.Reading);  // an hour on the clock as "Reading"
PlayerSleep.WakeUp();

PlayerSleep.Wait(this, 2 * 3600, WaitKind.Working,
    new WaitOptions { FreezeNeeds = true, NoPassOut = true, Label = "Fixing the boiler" },
    result => Log.Info($"Worked {result.Minutes} min{(result.Interrupted ? " (interrupted)" : "")}"));
```

Everything goes through the game's `WaitingController`, so time speeds up, sleep events roll and NPCs react as for a bed or a bench. `CanRest(out reason)` says whether a rest could start now; `IsSleeping`, `IsWaiting`, `IsResting`, `Current` (the game's `WaitingController.Type`) and `SecondsRested` describe the one running.

A mod's own wait calls back exactly once: when it runs out, the player cuts it short or needs end it, and also (with `Interrupted`) when a level change or another wait replaces it. Only one mod wait runs at a time. `FreezeNeeds` keeps hunger, thirst, tiredness, hygiene and mood where they are and stops needs from waking the player; `NoPassOut` blocks passing out; `Label` replaces the text on the clock. If the mod unloads mid-wait it isn't called back and the wait goes on as a plain one.

Events: `SleepStarted` (`AlarmSeconds`, `PassedOut`), `WaitStarted` (the kind) and `WokeUp` (`Kind`, `Seconds`, `Alarm`).

**Sleep events.** While the player sleeps the game may roll a theft, a beating, an arrest, junk, a nightmare and so on (`SleepEventController.SleepEvents`), scaled by how safe the spot is.

```csharp
SleepEvents.Intercept(this, roll =>
{
    if (roll.Chosen == SleepEventController.SleepEvents.StealMoney)
        roll.Cancel();                     // roll.Candidates lists what could have happened
});
SleepEvents.Occurred.Subscribe(this, e => Log.Info($"While sleeping: {e}"));
```

`SafetyFactor` (0 to 1, 1 is safe), `PlayerInSafeArea`, `InProgress`, `Planned` and `SecondsUntilPlanned` describe the current sleep. An arrest for open crimes is rolled separately and isn't covered by `Cancel`.

**Time picker.** `TimePicker.Open(this, "Work for", 30, 480, 120, "Start", minutes => ..., onCancel: () => ..., stepMinutes: 30, validate: m => m > 240 && tired ? "Too tired" : null)` asks for a length of time with the game's sleep clock (slider, arrow keys, its buttons). A validate reason shows in red and disables the button. Nothing happens to the player; the mod decides what to do with the minutes.

## Skills

```csharp
float cooking = PlayerSkills.Get(PlayerStats.PlayerSkillType.Cooking);   // 1 to 10, whole part = level
PlayerSkills.Add(PlayerStats.PlayerSkillType.Cooking, 0.5f);              // like practice: banner, achievements
PlayerSkills.Set(PlayerStats.PlayerSkillType.Mining, 5f);                 // quietly
PlayerSkills.SetLearningMultiplier(this, 2f);                             // learn every skill twice as fast
PlayerSkills.SetLearningMultiplier(this, 0f, PlayerStats.PlayerSkillType.Bribery);
PlayerSkills.Changed.Subscribe(this, c => { if (c.LeveledUp) Log.Info($"{c.Skill} is now {c.LevelAfter}"); });
```

Skills start at 1 and the game counts one as learned once it passes 1. `Changed` fires for every change, the game's own learning included. Learning multipliers scale positive gains only; the game's call shows the banner for the scaled amount.

**Custom skills.** A mod's own skills, kept per save:

```csharp
var fishing = CustomSkills.Register(this, "fishing", "Fishing");        // max 10 like the game's
fishing.Add(0.25f);           // level ups show the game's skill banner ("New skill Fishing learned!")
int level = fishing.Level;
fishing.Value = 3f;           // quietly
CustomSkills.Changed.Subscribe(this, c => ...);
CustomSkills.ShowBanner("Caught a pike!");   // any text in the skill banner
```

Values live in the framework's save data (`<save>/mods/ObenFramework.customskills.json`), keyed `modId:skillId`; values of mods that aren't installed stay untouched. Changes are refused at the main menu and while a save is running. Learned custom skills (level 1 or more) are listed on the character page under the game's skills, in the game's own row; a skill with another maximum is drawn on the same 10 bars. Skill banners from mods and the game now wait their turn instead of replacing each other; with no mod banners the game behaves as before.

## Effects, conditions and doctors

`PlayerEffects.IsActive(effect)` and `Strength(effect)` (0 to 1) for `Drunk`, `Mushroom`, `OrangeMushroom`, `Wasteland`, `Sleepy`, `Depression` and `LowHealth`; `Active()` lists them. `Changed` fires when the drunk, mushroom, orange mushroom or wasteland effect switches (the others follow stats, watch those with `StatThresholds`). `StartWasteland()` and `StopWasteland()` use the game's transitions.

`PlayerConditions.All()` lists the health conditions the player can get (from low hygiene and radiation) or has, with `Name` (the asset name the other calls take), `DisplayName`, `Count`, `Strength`. `Has`, `Add` and `Remove` take the name; `Changed` reports gains, losses and strength changes. `Add` also works for a non-stacking condition the player doesn't have yet, which the game's own call only strengthens.

`Medical.Treatments()` lists what the level's doctors sell (`MedicalTreatment`, title, price, currency, whether it is offered now). With a doctor's menu open, `CanTreat(kind)` and `Treat(kind)` buy one through the game's purchase. `Medical.Treated` fires for every treatment bought.

## Seats and interactions

```csharp
foreach (var seat in Seats.All())                     // title, address, position, occupied, toilet
    Log.Info($"{seat.Title} at {seat.Address}");
var chair = Seats.Find(savedAddress);                 // an address from an earlier session
if (chair != null)
    Seats.Sit(chair);                                 // within 2 m by default
Seats.Leave();
Seats.Sat.Subscribe(this, s => Log.Info(s.ByPlayer ? "Player sat" : $"{s.Npc.name} sat"));
```

`Sit` and `Leave` act like the interact key and refuse when the player can't move, the seat is taken or the seat won't let go. `Seats.Current` is the player's seat. NPCs placed on seats while a level loads aren't reported.

```csharp
var radio = CustomInteractions.Add(this, someObjectWithCollider, "Old radio", "Listen", () => PlayTune());
radio.Reason = () => batteries == 0 ? "Needs batteries" : null;   // red text, use refused
radio.Enabled = false;
radio.Dispose();
```

The object needs a collider of its own and must not be one of the game's interactables. It is moved to a layer the interact ray sees if needed and put back afterwards. The prompt, the key and controller support are the game's. Uses of the game's own interactables are in `ObenFramework.World.Interactables.Interacted`.

## Wardrobe, weapons and notes

`Wardrobe.WornOn("Head")` (also `Face`, `Watch`, `Backpack`) gives the item id worn or -1. `Equip(itemId)` puts an item on from the bag in the slot its category says; `Unequip(slot)` takes it off. Items that belong to someone else are left alone. `Changed` (`Slot`, `Before`, `After`) fires once per slot for every change, also for items dragged between slots.

`Weapons.Held()` and `HeldLeft()` describe the tools in hand (`ItemId`, `Kind` Melee/Explosive/None, ammo loaded and capacity). `Reload()`, `FlashlightOn`, `FlashlightCharge` and `SetFlashlight(on)` do what the keys do. Events: `Fired` (detonator), `MeleeSwung`, `Reloaded`, `Hit` (target kind, receiver, damage before and after). `SetHitMultiplier(this, HitTarget.Mining, 2f)` doubles the damage of the player's swings on rock; the targets are `Mining`, `Chopping`, `Other` (nodes), `Breakable` and `Animal`. Fractions round up by chance so averages stay right, and only hits from the player's own swing are scaled.

`PlayerNotes.Held()` lists the notes and books the player holds (`Id`, `Title`, `IsBook`, pages collected, `Read`). `Has`, `IsRead` and `Give(idOrPath)` take the game's id, which for most notes is the asset path (`Assets/Content/Notes/.../Note.asset`); a note that isn't loaded yet can still be given by its path. `Loaded()` lists the notes the game has in memory (not all of them: the rest load on demand). Events: `Collected` (a note, a new book or a new page) and `Read`.

## Subscriptions

`PlayerSubscriptions.Active()` lists the running subscriptions (electricity, mail and the rest) with name, cost and interval; `IsActive(id)` checks one. `Subscribe(idOrPath)` and `Unsubscribe(id)` work like the game's own offers, notification and first invoice included. Phone plans are listed but left to the phone shop.

## Jobs and achievements

`Jobs.All()` lists the level's work spots (`Title`, `Address`, pay per hour in both currencies, `Skill`, `RequiredSkill`, `Hardness`, `ShiftMinutes`); `Jobs.Worked` fires after a shift is paid with the hours worked and the pay that reached the wallet.

`GameAchievements.All()`, `Get(id)` and `IsUnlocked(id)` read the achievements; `Unlock(id)`, `AddProgress` and `SetProgress` change them; `Items()` lists the items that count towards one. These are the player's Steam achievements: unlocking one here unlocks it on Steam, so only do it for something the player really did.

## Dev bridge commands

| Command | Does |
| --- | --- |
| `player.speed [stat=] [multiplier=] [clear=true]` | Current and base speeds, every multiplier; set or clear the bridge's own on a stat. |
| `player.skills [skill=] [value=] [add=] [learning=]` | Game and custom skills (`modId:skillId`); set, raise, or scale learning. |
| `player.effects [wasteland=true\|false]` | Every effect with its strength. |
| `player.conditions [add=] [remove=]` | Conditions by asset name. |
| `player.sleep [action=sleep\|wait\|wake] [seconds=] [kind=]` | Rest state, planned sleep event, safety; start or end a rest. |
| `player.seats [sit=address] [leave=true] [limit=]` | Seats nearest first; sit or stand. |
| `player.wardrobe [equip=itemId] [unequip=slot]` | What is worn per slot. |
| `player.notes [give=idOrPath]` | Notes and books held. |
| `player.weapons [target= multiplier=] [flashlight=] [reload=true]` | Tools in hand, flashlight, hit multipliers. |
| `player.jobs`, `player.medical`, `player.achievements [unlocked=]` | Work spots, doctors' treatments, achievements (read-only). |

Multipliers set through the bridge belong to the bridge mod: they stay until cleared or the bridge reloads.

```sh
oben call player.speed stat=Speed multiplier=2
oben call player.sleep action=wait seconds=1800 kind=Reading
oben call player.skills skill=Cooking add=1
```
