# Player

Reference notes on the player character in Obenseuer (Unity 6000.0.77f1, Mono, Windows build under Proton).
All paths are relative to `decompiled/Assembly-CSharp/`. Line numbers are from the current decompile and drift if
the game updates. This is from reading code only: anything that depends on scene or prefab layout (which GameObject a
component sits on, hierarchy names) is marked "not verified".

`Player.cs` is NOT the player. It is the abstract base for card-game players (`CardGame`, `CardHand`).

## 1. Finding the player

These are per-scene singletons set in `Awake`/`Start`. Levels load with `LoadSceneMode.Single`
(`SaveController.cs:792`), so every `instance` is destroyed and reassigned on a level change. Never cache them across a
load; test with Unity null (`(bool)x`) before use.

| What | Access | Notes |
|---|---|---|
| Player body | `PlayerLocator.instance` (`PlayerLocator.cs:6`) | `.transform` is what moves. Tagged `"Player"` (triggers use `CompareTag("Player")`). |
| Body parent | `PlayerLocator.instance.root` | Chairs re-parent the body back to it. |
| Head / rig / animator | `.playerHead`, `.playerRig`, `.playerRigUnrigified`, `.playerAnimator` | Rigs are deactivated in first person (section 4). |
| Look | `.playerAppearance` (`Appearance`) | Section 6. |
| Movement | `OpenSewerCharacterController.instance` (`:164`) | ECM `BaseCharacterController`; `.movement` is `CharacterMovement`. |
| Camera rig | `ThirdPersonCameraController.instance` (`:138`) | Detaches to scene root in `Awake`. Holds `rotX`/`rotY`. |
| First/third person | `ThirdPersonCameraCollision.instance` (`:109`) | `.firstperson`, `.distance`. |
| Real camera | `PlayerCamera.instance` (`PlayerCamera.cs:36`) | `.playerCamera`, `.audioListener`. Use this transform for aim. |
| Arms | `FirstPersonHands.instance` | Animator fetched in `Start`, not `Awake`. |
| Arms camera | `UnityStandardAssets.Characters.ThirdPerson.FirstPersonSettings.instance` | `.firstPersonCamera`. |
| Stats | `PlayerStats.instance` (`PlayerStats.cs:130`, set at `:200`) | `SavableScript`, GUID `"PlayerStats"`. |
| Identity | `PlayerIdentity.identity` (`PlayerIdentity.cs:27`) | Name, age, `gender` ("male"/"female"). `Height` is a constant 1.57. |
| Hand items | `PlayerHandItems.instance` | `righthandItem`, `lefthandItem`. |
| Inventory | `Inventory.instance` (`Inventory.cs:187`) | `CharacterSlots` holds held/worn items. |
| Death | `Death.instance` (`Death.cs:13`) | GameObject is `HideInHierarchy`; use the singleton. |
| Sleep/wait | `WaitingController.instance` | |
| Crime | `Crime.instance`; `Prison` has no static, find it in the scene | |
| Noclip / console | `DeveloperMovement.Instance`, `DeveloperConsole.Instance` | Console is `DontDestroyOnLoad`. |

Layout (not verified in the scene): `PlayerLocator`, `OpenSewerCharacterController`, `Rigidbody`, `CapsuleCollider` and
`PersistLocation` each call `GetComponent<Rigidbody>()` on their own object and the game treats that body as "the
player", so they are almost certainly one GameObject. `PlayerStats`, `Death`, `Crime` live elsewhere; use the statics.
The camera rig is a scene-root object, not a child of the body.

Level/save lifecycle: static events on `SaveController` (`:273-283`): `PlayerWillChangeLevel`, `PlayerWillLoadGame`,
`SavingStarted/Done`, `LoadingStarted/Done`; static bool `SaveController.Loading`. Use `LoadingDone` as "scene ready".

## 2. PlayerStats

`PlayerStats : Stats : SavableScript` (`PlayerStats.cs:10`). Saved with FullSerializer as global data; public fields are
saved, private and `[fsIgnore]` ones are not.

Top-level flags: `Godmode` (`:74`, only gates `death()`; health still drops to 0), `Active` (`:76`, stats tick only while
true; set false during dialogue `DialogueController.cs:213`, start menu, talk interactions), `StatsProgressDisabled`
(`:78`, console `disable_progress`), `AutoConsume` (`:80`), `LocalSeconds`, `Skills`. These flags are saved, so a loaded
game can come back with `Godmode` on.

| Member | Class | Fields (range, direction) |
|---|---|---|
| `health` | `PlayerStatsHealth` | `currentStatus` 0-100 (100 full), `HealthToRegenerate`, `HealthRegenerationRate`=1/min, `bleeding`/`Bleeding` 0-100 |
| `hungerAndThirst` | `PlayerStatsHungerAndThirst` | `Hunger` (-100..100), `Thirst`, `Bowel`, `Bladder` (100 = bad), `HungerRate`=10/h, `ThirstRate`=15/h, `ToiletNeed` = max(Bowel, Bladder) |
| `tiredness` | `PlayerStatsTiredness` | `currentStatus` (-100..100, 100 = pass out), `tirednessToAdd`, `sleepdebt`, `Exhaustionrate`=6/h, `RestRate`=10/h, `SleepTimeSpeed`=150 |
| `mentalHealth` | `PlayerStatsMentalHealth` | "Depression": `currentStatus` 0 fine .. 100 worst, `effectiveMentalHealth`, `mentalHealthTarget` (default 33), `expectationLevel`, `usedItems` |
| `alcohol` | `PlayerStatsAlcohol` | `AlcoholInBlood` (drunk fx above 15, pass out above 100), `AlcoholDrunk` (stomach queue), `AlcoholBurnRate`=0.5/min, `AlcoholAbsorbionRate`=2.5/min |
| `mushrooms` | `PlayerStatsMushrooms` | `MushroomsHigh`, `MushroomsHighOrangeNormal/Bad`, `MushroomsUsed*`, `SMVProgression` (100 = death), `SMVProgressionRate`, `CollectedGlows` |
| `addictions` | `PlayerStatsAddictions` | `Alcohol/Mushroom/Smoking/GamblingAddiction` and `...Need`, all 0-100 |
| `hygiene` | `PlayerStatsHygiene` | `Hygiene` 0-100 (100 clean, default 75), `HygieneRate`=-2/h |
| `sauna`, `radiation`, `duck`, `gambling` | `PlayerStatsSauna` (`Overheat`), `PlayerStatsRadiation` (`Dose`), `PlayerStatsDuck`, `PlayerStatsGambling` (`OCWon`, `OCLost`) | |
| `conditions` | `PlayerStatsDiseasesAndConditions` | `activeConditions` (`List<BaseHealthCondition>`, ScriptableObjects in `PlayerStatsConditions/`) |

Skills: `PlayerSkillType` (`:29`, 24 skills plus `None`), `GetSkill(type)` = floor of `Skill` (start 1, `maxLevel` 10),
`IncreaseSkill(type, amount, notification)` (`:384`, touches `PlayerSkillIncreaseUI.instance`), `GetSkillWithEffects`.

### How stats tick

Stats follow the in-game clock, not Unity time. `PlayerStats.StartLate` (`:219`) waits for time to be enabled, then
subscribes the private `DeltaSeconds(int sec)` (`:306`) to the static event `TimeOfDayAzure.SecondsPassed`
(`TimeOfDayAzure.cs:315`, invoked at `:464`), which carries the in-game seconds since the previous call.

`DeltaSeconds`: skips everything (except `BarUpdate()`) unless `Active && !StatsProgressDisabled`. Otherwise
`LocalSeconds += sec`, and once it exceeds 59 the per-minute block runs and `LocalSeconds` resets:
`health.UpdateStats()`, `mushrooms`, `mentalHealth`, `hungerAndThirst`, `tiredness`, `addictions`, `hygiene`,
`radiation`, `conditions`. On every call regardless: `alcohol.UpdateStats(sec)`, `duck`, `sauna`,
`ProcessItemSpecialEffects()`. Most per-hour rates are divided by 60 and applied once per block, so they assume one
block per game minute.

Consequences: sleeping or waiting sets `TimeOfDayAzure.instance.SetSpeedFactor(150)` (`WaitingController.cs:317,353`),
so a night runs the stats fast; hunger/thirst/hygiene use reduced rates while `IsSleeping`.
`DifficultyController.instance.currentDifficultyInfo` scales needs, addiction and SMV rates. For co-op the clock is the
thing to keep in agreement.

Rules worth knowing: hunger or thirst at 100 deals `ThristStarvationDamage/60` per block; `Bowel > 100` runs
`Defecation()` (30 `ShitSeizure` damage and a world item) unless a toilet is in use; `tiredness > 100` calls
`WaitingController.PassOut()`; `effectiveMentalHealth >= 100` randomly picks `Depression.Suicide()` (`death(Suicide)`),
`HurtYourself()` or drinking/mushroom use from the inventory; `SMVProgression > 100` calls `death(SMV)` and health is
capped at `100 - SMVProgression`; bleeding drains `bleeding*0.1` health per block. Mental health is forced to 0 when the
difficulty has `mentalHealthDisabled`.

### Changing stats from a mod

All fields are public; assign directly, with care:
- Bars refresh from `PlayerStats.instance.BarUpdate()`. Call it after changing stats while `Active` is false.
- `health.Damage` does not clamp to 100. Negative damage heals but can overshoot (`InteractableWait.cs:326` does this).
  Over-time healing is `health.HealthToRegenerate += x` (negative = poison).
- Setters that keep side effects: `mentalHealth.ChangeMentalHealth(v, direct)` (`:269`),
  `ChangeMentalHealthTarget(v, mode)` (`:214`), `alcohol.SetAlcohol(v)` (`:53`), `mushrooms.ChangeSMVProgression(v, direct)`
  (`:159`), `hygiene.UpdateValues(float)`. `PlayerStatsEdit` (`PlayerStatsEdit.cs`) wraps these.
- `ResetStats()` (`:576`) and `ClearAllNeeds()` (`:641`, also sets health 100).
- Eating/drinking: `ItemConsumable.Invoke(ItemData, bool worlduse)` (`OS.Items/ItemConsumable.cs:117`) ->
  `PlayerStats.UpdateValues(ItemData, ItemConsumable)` (`:230`) -> `Inventory.OnItemConsumed` (event
  `Inventory.ItemConsumed`; subscriber exceptions are swallowed).

### Stale sub-objects after a load (gotcha)

`PlayerStats.OnLoadingGame` goes through `SaveController.ReplaceProperties` (`SaveController.cs:1028`), which copies
field values from a deserialized copy onto the live object. Members marked `[fsCheckChildFields]` (`mushrooms`,
`mentalHealth`, `hungerAndThirst`, `hygiene`, `conditions`) are updated in place; the rest (`health`, `alcohol`, `sauna`,
`addictions`, `tiredness`, `duck`, `radiation`, `gambling`, `Skills`) are replaced by new objects. Never keep a reference
to `PlayerStats.instance.health` etc.; go through `PlayerStats.instance` each time.

## 3. Damage, death, prison

### Damage

- The real entry point is `PlayerStatsHealth.Damage(float damage, DamageTypeNames reason, bool silent = false)`
  (`PlayerStatsHealth.cs:91`). Falls (`FallDamage.cs`), explosions, hunger, drowning, trains, NPC violence
  (`NPC/Act_Robber.cs:646`), sauna, console all call it. `Bleed` adds to `Bleeding`; `Mushrooms` and `Gass` are
  filtered by `FaceSlotController` protection. When `currentStatus < 0` (strict) it calls `death(reason)` and clamps to 0.
- `PlayerStats.Damage(DamageType, float effectfactor)` (`:705`) is NOT damage; it only plays the hurt sound and flash.
- `DamageTypeNames`: Unspecified, Violence, Hunger, Thirst, ShitSeizure, SMV, Drown, Fire, Radiation, Fall, Poison,
  Withdrawal, Suicide, Bleed, Mushrooms, Explosion, Gun, Gass, Force, Liquid, Electric.
  `DamageController.instance.GetDamageType(name)` returns the `DamageType` asset (sounds, random death text via
  `GetMessage()`); an unknown name returns an empty `DamageType` whose sounds are null, so `GetDeathSound()` throws.
- `HealthEffect`, `DepressionEffect`, `SleepyEffect`, `DrunkEffect` poll `PlayerStats.instance` in `Update` and only set
  post-process weights. They never write stats. Drunk (`DrunkEffect.cs:78`) starts above 15 blood alcohol and feeds
  `PlayerDrunkControls`, which perturbs movement input in `OpenSewerCharacterController.HandleInput`.

### Death

`PlayerStats.death(DamageTypeNames reason)` and `death(int reason = 0)` (`:340`, `:348`) do nothing under `Godmode`,
else `Death.instance.PlayerDeath(reason)` (`Death.cs:19`), which once only: sets `dead = true`, plays the death sound,
`PlayerStats.ShowDeathEffect` (`PlayerStatsUI.cs:281`: hides the bars, picks a message, `DeathMessage.ChangeDeathMessage(string)`
and `DeathEffect()`), `PauseMenu.PausemenuDeath()` (`PauseMenu.cs:296`: opens the pause menu with Resume and Save disabled),
`Death.ToggleDeadthMessage(true)` (`DeathMessage.ShowDeathMessage`).

There is no respawn. `Death.dead` is written only at `Death.cs:23`; the way out is the pause menu's load, which reloads
the scene. While dead, `ThirdPersonCameraController.DeathCheck` stops the camera following and `PauseMenu.Pause()` is
blocked. A co-op revive has to be built: prefix `Death.PlayerDeath` (return false to cancel everything), or after the
fact set `dead = false`, restore health, re-activate the `PlayerStatsUI` bars and fix the pause menu. Console helpers:
`DeveloperConsoleRoutines.Kill()` (`:141`), `ToggleGodMode()` (`:1833`).

### Crime, arrest, prison

`Crime` (`Crime.cs`): `crimeRecord` (list of `CrimeRecord { generic, type, victim, amount }`), `timesInJail`,
`crimePerPrisonHour`=250. `CommitCrime(NPCReference victim, int severity, CrimeType type, bool silent)` (`:223`) does a
`Find` for a generic record of that type; `Crime.Start` (`:120`) creates one per `CrimeType`, so calling it earlier
throws NRE.

Arrest flow:
1. `Crime.GoToPrison(double severityFactor = 1.0)` (`:303`) (from `NPC/Act_Police.cs:771`, `logic_crime`, dialogue, or
   sleeping with crimes via `CheckCrimesForArrest`) calls `WaitingController.PassOut(true, Type.Arrest)` with an arrest
   event in 10 s and fires static `Crime.OnGoingToPrison`.
2. `SleepEventController.Event_Arrest` (`:186`) removes stolen goods, then `Crime.TeleportToPrison()` (`:331`).
3. That calls `SaveController.ChangeLevel(info_game_logic.prisonLevelName, prisonEntrypoint)`; defaults
   `"Interior Tenement Gatehouse"` / `"prison"` (`info_game_logic.cs:46,48`). The level change autosaves.
4. In the new scene `Prison` registers its entrypoint (`Prison.cs:127`) with `_OnPlayerSpawn = ImprisonPlayer` (`:136`):
   random cell, `Teleport.TeleportPlayerToTargetStatic(cell.playerSpawnpoint)`, confiscate items, subscribe to
   `SecondsPassed`.
5. `Prison.DeltaSeconds` burns crime amounts and calls `Release()` (`:217`) when they reach 0 (after 300 s) or after
   `100 + 12*timesInJail` hours. `Release()` clears the record and fires scene outputs; those do the walk-out.

Check `Crime.instance.ArrestInProgress` before starting your own level change. Alternatives to prison: `PayFine()`,
`TryToBribe()`.

## 4. Movement and camera

### Controller

`OpenSewerCharacterController` (OSCC, `OpenSewerCharacterController.cs`):
- Speeds `walkSpeed`=2.5, `runSpeed`=5, `swimmingSpeed`=5, `crouchSpeed`=1, `backwardSpeed`, `strafeSpeed` (setters
  clamp >= 0). Running is "not walking" through a private flag; the public `walk` property is never assigned, so infer
  run/walk from `movement.velocity`.
- Crouch: `isCrouching` (`:136`), `eyeHeight` (1 to 0.5 at 2/s), `Crouch()`/`StandUp()` (`:415`/`:409`); `StandUp` waits
  until a sphere cast overhead is clear. Capsule, rigidbody are cached in `Awake`.
- Other state: `movement.velocity`, `movement.isGrounded`, `movement.rotation`, `firstPersonEnabled`,
  `WaterController.instance.playerIsSwimming`.
- `Animate()` (`:280`) sets animator floats `Forward` (speed/runSpeed), `Turn` (`Atan2(x, z)`), `Jump` (vertical velocity)
  and bools `OnGround`, `Crouch`; the same `Forward` feeds `FirstPersonHands.UpdateHands`.
- `FixedUpdate` (`:203`) returns at once when the rigidbody is kinematic. That is how noclip and chairs freeze the body.

### Locking input

`GameController.instance` (`GameController.cs`). The lock is a list of owner GameObjects, not a counter:

```csharp
void ControlsDisabled(GameObject gameObject, bool ShowCursor = false, bool disableKeys = true, bool writeLock = false,
    bool lockedFirsperson = false, bool firstpersonSoftLock = false, Transform softLockTarget = null,
    bool disableCamera = true, bool disableMovement = true)                                   // :285
void ControlsEnabled(GameObject gameObject, bool disableFirstpersonSoftLock = false)         // :344
void ControlsDisabledMove(GameObject go, bool lockedFirsperson = false, bool firstpersonSoftLock = false,
    Transform softLockTarget = null, bool lockedMovement = true, bool cameraMovementDisabled = false,
    bool noclip = false, bool instantly = false)                                              // :393
void ControlsEnabledMove(GameObject go, bool disableFirstpersonSoftLock = false, bool noclip = false) // :409
```

- Use your own GameObject as key and release with the same one. Input returns only when every list is empty
  (`keysDisabled`, `writeLock`, camera lock via `CheckDisableStatus()`); dialogue, sleep, chairs, camera animations
  stack with you.
- Movement has its own list: `OSCC.DisableControls(go, noclip)` / `EnableControls(go, noclip)` (`:174`/`:187`),
  `ControlsDisabled()` is true while non-empty. Locked, `moveDirection` is zeroed but gravity still applies.
- Public state worth reading first: `GameIsPaused`, `keysDisabled`, `inventoryDisabled`, `writeLock`.
  `DisableInventory(go)`/`EnableInventory(go)` is the same pattern for the menu.
- `GameController.Update` is a long chain of string-keyed `InputManager` lookups every frame. Keep patches on it light.

### Teleporting

Game helper: `PlayerLocator.instance.Teleport(Transform location)` (`PlayerLocator.cs:43`), reached via
`Teleport.TeleportPlayerToTargetStatic` (`Teleport.cs:86`) and `PlayerLevelEntrypoints.Entrypoint.TeleportPlayer()`.
It sets `transform.position`, `eulerAngles` and **`localScale`** from the target, writes position/rotation to the
`Rigidbody` and zeroes both velocities, sets `ThirdPersonCameraController.rotY = target.y + 180` and `rotX = 0`, calls
`MoveToTarget()` to snap the camera, and updates `PersistLocation` (so a save right after keeps it).

Gotchas:
- It takes a `Transform`, not a point. Pass a temporary GameObject with scale `Vector3.one`; the player's scale is
  overwritten with the target's. It always resets pitch.
- No ground or collision check; the game only targets authored transforms (entrypoints, cell spawn points, exam bed).
  Raycast down yourself.
- Seated: the body is parented to the chair and kinematic. Stand up first with
  `(InteractObjects.instance.savedInteractable as InteractableChair)?.ForceStandUp()` (`InteractableChair.cs:267`).
- Light option (what chairs do, `InteractableChair.SetPlayerPosition` `:635`): set `transform.position`,
  `Rigidbody.position`, zero velocity. Ladders (`InteractableLadder`) and `Node.OnPlayerEnter` also write the position
  directly, so a teleport hook on `Teleport()` does not see them.
- Do not teleport during `Changelevel.ChangeLevelActive`, `SaveController.Loading`, `Crime.ArrestInProgress`.
  Cross-level travel is `SaveController.ChangeLevel(string newLevel, string newLevelEntrypoint = "NONE")` (`:307`);
  the entrypoint teleport runs ~0.1 s after load (`MovePlayerToEntrypointDelay`).

### Noclip

Console `noclip` -> `DeveloperConsoleRoutines.NoClip()` (`DeveloperConsoleRoutines.cs:1737`) ->
`DeveloperMovement.Instance.ActivateDebugMovement()` (`DeveloperMovement.cs:120`): disables every `Collider` under
`PlayerLocator.instance.transform.parent`, calls `GameController.ControlsDisabledMove(GameController.instance.gameObject,
..., noclip: true)` (OSCC owner key = that object, rigidbody kinematic), and `Update` then moves its own
`gameObject.transform` along the camera axes (6 m/s, x2 with Walk, 5 m/s vertical). Turn off with
`DeactivateDebugMovement()` (`:132`), which must use the same owner key (`ControlsEnabledMove(..., noclip: true)`).
`shouldHandleMovement` tells you if it is on. The state is not saved. Which object `DeveloperMovement` sits on
is not verified (presumably the body).

### Camera

- `ThirdPersonCameraController` `LateUpdate` (`:153`) applies mouse input unless `controlsDisabled`, `menuEnabled` or
  `moveDisabled`. Pitch clamp +-60 (chairs: `clampChairEnabled` with own limits). API: `MoveCamera(rotX, rotY)`,
  `MoveToTarget()`, `GetEyePosition()` (= `1.57 * eyeHeight - 0.075`), `DisableRotation()`/`EnableRotation()`,
  `DisableMovement()`/`EnableMovement()`, `FollowTarget`, `followPlayerDisabled`.
- `ThirdPersonCameraCollision.FixedUpdate` (`:116`) flips mode at `distance < 0.5`. `FirstPerson()` (`:199`) calls
  `HidePlayerModel()` (deactivates `playerRig`/`playerRigUnrigified`, disables all renderers under the player);
  `ThirdPerson()` (`:221`) reverses it. `CameraModeChange` is a static event invoked without a null check
  (`PlayerHandItems` keeps one subscribed). `FirstPersonOnly(bool)` pins first person.
- First-person yaw: `FirstPersonSettings.RotateCharacter()` copies the camera yaw into `OSCC.movement.rotation` every
  `LateUpdate` while controls are enabled, so body yaw == camera yaw there.
- `PlayerCamera.DisableCamera()/EnableCamera()` toggle camera and audio listener. `CameraShake.Shake(duration, amount)`
  is static. `PlayerCameraAnimations.instance` plays pickup/eat/drink/wake-up animations using the same keyed lock plus
  `PauseMenu.DisableSave(go)` (`:287`).

### Sitting and sleeping

`InteractableChair` (`InteractableChair.cs`; chairs, sofas, the doctor's exam bed via `DoctorExaminationBed`):
- Static `PlayerIsSitting` (`:137`); instance `PlayerSittingHere`, `Sit(bool immediately = false)` (`:375`),
  `StopSit()`/`StopSit(bool)` (`:577`), `ForceStandUp()`, `SitToggle()`, `DisableStopSit(bool)`.
- `Sit` (private overload `:380`): `GameController.ControlsDisabledMove(chair)`, `SetParent(chair)`, rigidbody
  kinematic, camera follow disabled and clamped, camera coroutine to `cameraPosition`. The player has no sit animation;
  `animationNodes` are for NPCs. `StopSitEnd` (`:615`) restores the parent (`PlayerLocator.root`), kinematic state
  and the lock; it is also subscribed to `SaveController.PlayerWillChangeLevel`. `leavePosition` is the stand-up spot.
  `playerSitting` is saved and re-applied after load.

Beds: `Sleep : Interactable` (`Sleep.cs`). `Use()` (`:158`) refuses when `occupied`, owned by another NPC, or
`!tiredness.CheckTiredness()` (tiredness > 10 or blood alcohol > 75). Otherwise `WaitingController.ShowTimeSetUI(...)`
opens the sleep UI. The body is not moved into the bed or animated; the control lock, a black overlay and the time
speed change.

`WaitingController` (`WaitingController.cs`): `waitingType` (`None, Sleeping, Passout, Waiting, Working, HavingFun,
Reading, Playing, StaringWall, Arrest, WatchingTV, Talking, Studying`), `IsSleeping`, `IsWaiting`,
`Sleep(int setTime)` (`:242`), `Wait(int, Type, float speedFactor, bool ignorePlayerNeeds, reasons)` (`:329`),
`PassOut(bool canBeArrested, Type)` (`:217`), `Wakeup(bool alarm, bool onlyClearWaiting)` (`:372`). Private
`StartSleep` (`:261`) locks controls with the controller's own object, rolls a sleep event or arrest, and sets time speed
to `tiredness.SleepTimeSpeed`. Stat-driven auto wake-up goes through `CanSleep` reasons. Static
`WaitingController.BlockWaiting(Object)`/`UnblockWaiting` forbids sleeping (use it for a bed vote). Sleep speeds up the
global clock for everything, so it is the main thing peers must agree on.

## 5. Hands, items, inventory link

- `Inventory.CharacterSlots` is `SlotController[8]` built in `Inventory.Start` (`:192`). Constants (`Inventory.cs:56-70`):
  `watch=4`, `head=2`, `face=3`, `backpack=5`, `rightHand=6`, `leftHand=7`; slots 0/1 take `hat`/`accessory_1`.
  Slot 6 = "Wieldable Primary" (key 1), slot 7 = "Wieldable Secondary" (key 2). Held item: `CharacterSlots[6].itemStack.itemId`
  (`-1` empty), `.itemAmount`, `.Meta`. `Inventory.Slots` is the 35-slot grid.
- Slot callbacks (`OnDropped`/`OnTaken`/`OnReturn`) wire slots 2-7 to the head/face/backpack/hand controllers. Equip through
  the slot's `AddItem` or `ItemOperations.AddItem(Item item, int owner, SlotController slot, object[] meta, bool notify,
  bool ignoreCrime)` (`ItemOperations.cs:51`) so they fire; writing `itemStack` directly skips them.
- Other inventory API: `ItemOperations.AddItem(int id, ...)` (`:33`), `AddItems`, `Inventory.RemoveItem(...)` (`:982`),
  `CheckForItem`, `PlayerStats.Instantiate(Item)` (drop one at the feet).
- `PlayerHandItems.IPlayerHandItem` (`:8`): `item`, `meta`, `equipped`, `fullyEquipped`, `objectFirstPerson`,
  `objectThirdPerson`. `righthandItem`/`lefthandItem` are null when empty; holstered means `equipped == false` with an item
  present (`primaryUnequipped`/`auxiliaryUnequipped` are saved). `UpdateItemInHand(string hand, int amount, GameObject
  prefab, Item itm, ItemEquipable equipable)` (`:155`) with `hand` = `"Wieldable Primary"` / `"Wieldable Secondary"`
  instantiates the prefab under `playerHead` at local (0, -0.33, 0). In first person `CameraModeChange` (`:243`) moves the
  first-person object under the arms rig and hides the third-person one.
- `FirstPersonHands.UpdateHands(float forwardAmount, Vector3 move)` (`:46`) sets `Forward`/`Turn` each frame.

## 6. Appearance and the character editor

### Data model

`Appearance : MonoBehaviour` (`Appearance.cs`) sits on the model root (player rig, NPC rigs, the editor preview). Its
inputs come from a `Character` ScriptableObject (`createdCharacter`, `Character.cs`).

Body values (floats, 0..1 unless noted; `...Range` arrays map them to real units in `UpdateRigOffsets`): `gender`,
`shape` (-1..1; negative = muscle, positive = fat blend shape), `height`, `advanced` (bool), `headSize`, `faceSize`,
`neckLength`, `shoulderWidth`, `breastSize`, `hipWidth`, `handSize`, `footSize`, `eyeSize`, `eyeDistance`, `eyePosition`,
`eyeDepth`, `earSize`, `earRotation`, `noseRootRotation`, `noseTipRotation`, `noseWidth`, `lipThickness`, `jawPosition`,
`chinPosition`, and `male` (bool). `Character` holds the same numbers in PascalCase (`Weight` = `shape`) plus
`GenderString` ("Male"/"Female", exact case), `Age`. The "Deprecated" headers in `Character` are misleading; these are
the live fields.

Wearables: `Wearable : ScriptableObject` (`Wearable.cs`: `Name`, `Material`, `MainColor/SecondaryColor/TertiaryColor`,
`Texture`, `Mask`, `Smoothness`, `showMeshes`, `hideMeshes`, `blendShapes`). Slots, as `selected*` on `Appearance`:
`selectedHead`, `selectedHat`, `selectedHair`, `selectedGlasses`, `selectedBeard`, `selectedShirtT` (jacket),
`selectedShirtB`, `selectedPantsT`, `selectedPantsB`, `selectedShoes`, `selectedSocks`. An asset named `"-"` is "none".
Catalogs are loaded with `Resources.LoadAll<Wearable>` from `Wearables/HeadsMale`, `HeadsFemale`, `Beards`, `Glasses`,
`Hairs`, `Hats`, `ShirtsT`, `ShirtsB`, `PantsT`, `PantsB`, `Shoes`, `Socks` in `Appearance.Start` (per instance) and
`AppearanceController.Start` (global `AppearanceController.instance`; do not read before its `Start`).

Sync by asset name (`Wearable.name`), not array index: `LoadAll` order is not guaranteed and
`Appearance.GetWearableIndex` (`:1090`) returns 0 on a miss.

### How it is applied

- `Appearance.Start` (`:259`): 14 `Resources.LoadAll` calls per instance (hitch when spawning many avatars); if
  `createdCharacter` is null it does `Resources.Load<Character>("Characters/" + transform.parent.parent.name)`, falling
  back to `"Characters/Random NPC"`; then `LoadCharacter()`.
- `LoadCharacter()` (`:297`) = apply everything from `createdCharacter`: `LoadCharacterValues`, `UpdateGender` (may call
  `ChangeGender`, which resets `selectedHead` to head 0), `VerifyOutfit` (`:712`, fills "-" defaults and copies
  `createdCharacter.X` into `selectedX`), `UpdateOutfit` (`:806`), `UpdateSkins` (`:674`), `UpdateShapes` (`:485`:
  blend shapes `Fat`, `Muscle`, `Breast`, `Lip Thickness` plus rig target offsets).
- `UpdateCreatedCharacter()` (`:624`) is the reverse: copies `Appearance` fields and `selected*` into `createdCharacter`,
  then refreshes. The editor calls it on every change. The property setters (`height`, ...) are auto-properties and do
  nothing visible until `UpdateShapes()` or `UpdateCreatedCharacter()`.
- Gotcha, shared assets: `Resources.Load<Character>` returns the shared in-memory asset, and the editor writes into it;
  that is how the main-menu result reaches the in-game player without a save file. For remote avatars use a private
  copy (`UnityEngine.Object.Instantiate(character)`; the `(Clone)` suffix also keeps it off the `"Random NPC"` check)
  and assign `appearance.createdCharacter` **before** `Start` runs (instantiate the avatar inactive, assign, activate).
- Gotcha, `Random NPC`: when `createdCharacter.name == "Random NPC"`, `LoadCharacterValues` and `VerifyOutfit` randomize
  every field and write them back to that shared asset. A mis-resolved avatar is random and mutates global state.
- `UpdateSkins` always uses `maleBodies[0]`/`femaleBodies[0]`; skin colors come from the selected head.

### Character editor and persistence

`CharacterEditor` (`CharacterEditor.cs`) wraps a `CharacterEditorCharacter` (holds an `Appearance`) and a `Character`
asset named `"Created Character"`. `Start` (`:192`) assigns it to the preview `Appearance` and sets `Age = 18`; sliders and
index buttons edit values and call `appearance.UpdateCreatedCharacter()`. Name, age, gender and difficulty go through
`PlayerIdentityMainMenu.identity` (`DontDestroyOnLoad`) and `PlayerIdentity.GetCharacterEditorIdentity()` copies them once
(`mainMenuInitDone`).

I found no code that writes the player's look into the save. `PlayerIdentity.clothes` (`int[10]`) is saved but nothing reads
or writes it, and `slots` beside it is unused. The look appears to live only in the in-memory `Character` asset and the
scene's player prefab. Check at runtime what a loaded save shows before relying on a persisted look.

### Mesh-based system (NPC models)

`CharacterAppearance` (`CharacterAppearance.cs`) stores a look per child renderer: active flag, `sharedMaterials`, blend
shape name/value pairs, eye transforms and `male_skeleton` bone scales in `Character.appearance`.
`SaveCharacterAppearance()` (`:29`) captures, `UpdateAppearance()` (`:93`) applies. It matches child names and holds
`Material` references, so it only copies between two models in the same session. `CharacterModel` requires it.

### Recipe for avatars

1. Sender: read from `PlayerLocator.instance.playerAppearance`: `male`, the float values, the `selected*` wearable
   names. Re-send when `UpdateCreatedCharacter` runs.
2. Receiver: instantiate the avatar model inactive, clone a `Character`, set `GenderString`, floats and wearable
   references (look them up by name in `AppearanceController.instance` arrays), assign to `appearance.createdCharacter`,
   activate (or call `LoadCharacter()` afterwards).

## 7. Pose and state to sync

| What | Read from | Notes |
|---|---|---|
| Position | `PlayerLocator.instance.transform.position` | World space; parented to the chair while sitting. |
| Body yaw | `transform.eulerAngles.y` | Camera yaw in first person, move direction in third. |
| Aim | `PlayerCamera.instance.transform.eulerAngles`, or `ThirdPersonCameraController.rotX/rotY` | `rotY` is not body yaw (`Teleport` uses +180). Prefer the camera transform. |
| Velocity / grounded | `OSCC.movement.velocity`, `.isGrounded` | `Forward` = speed / `runSpeed`. |
| Crouch / eye | `OSCC.isCrouching`, `eyeHeight` | |
| Swimming | `WaterController.instance.playerIsSwimming` | |
| Climbing | `InteractableLadder.playerClimbingGlobal` | `protected static`; reflection. |
| Minitrain | `TrainController.instance.UsingMiniTrain()` | |
| Sitting | `InteractableChair.PlayerIsSitting` | Chair: `InteractObjects.instance.savedInteractable`. |
| Sleeping | `WaitingController.instance.waitingType` | Body does not move. |
| Held / worn | `Inventory.instance.CharacterSlots[6]`/`[7]` hands, `[2]` head, `[3]` face, `[4]` watch, `[5]` backpack; `righthandItem?.equipped` | Send `itemStack.itemId`. |
| Alive | `Death.instance.dead`, `health.currentStatus` | |
| View mode | `ThirdPersonCameraCollision.instance.firstperson` | Local only. |

The local player model (`playerRig`, `playerRigUnrigified`) is deactivated in first person, so `playerAnimator` is not a
reliable pose source. Sync the values above and drive the remote avatar's animator with the same parameter names:
`Forward`, `Turn`, `OnGround`, `Jump`, `Crouch`. The arms animator (`FirstPersonHands`) is first-person only; never sync
it. A 10-20 Hz snapshot of position/yaw/pitch/velocity plus events for sit/stand, equip, crouch and death is enough.

## 8. Harmony hook points and gotchas

Signatures as in the decompile. Private members need `AccessTools`.

| Hook | Signature | Notes |
|---|---|---|
| Stat tick | `PlayerStats.DeltaSeconds(int sec)` private (`:306`) | Prefix to skip ticking, postfix to publish. |
| Any damage | `PlayerStatsHealth.Damage(float damage, DamageTypeNames reason, bool silent = false)` (`:91`) | Return false to cancel. Negative = heal. |
| Death entry | `PlayerStats.death(DamageTypeNames reason)` and `death(int reason = 0)` (`:340`, `:348`) | Two overloads; give the parameter types. Honors `Godmode`. |
| Death effects | `Death.PlayerDeath(DamageTypeNames reason)` (`Death.cs:19`) | Prefix returning false cancels `dead = true` and the UI. |
| Consume | `ItemConsumable.Invoke(ItemData itemData, bool worlduse)`; event `Inventory.ItemConsumed` | |
| Teleport | `PlayerLocator.Teleport(Transform location)` (`:43`) | Misses chair/ladder/node position writes. |
| Level change | `SaveController.ChangeLevel(string newlevel, string newLevelEntrypoint = "NONE")` (`:307`), events | |
| Input lock | `GameController.ControlsDisabled(...)`, `ControlsEnabled(GameObject, bool)` (`:285`, `:344`) | Watch who locks; add your own key. |
| Camera mode | `ThirdPersonCameraCollision.FirstPerson()`/`ThirdPerson()`, static `CameraModeChange(bool)` | |
| Sit | `InteractableChair.Sit(bool immediately, bool onLoadGame)` private (`:380`), `StopSit(bool)` (`:582`), `ForceStandUp()` | Public `Sit(bool)` only forwards; patch the private one to catch load-time sits. |
| Sleep | private `WaitingController.StartSleep(...)` (`:261`), `PassOut(bool, Type)`, `Wakeup(bool, bool)`, `Wait(...)` | |
| Arrest | `Crime.GoToPrison(double)` (`:303`), `TeleportToPrison()` (`:331`), event `Crime.OnGoingToPrison` | |
| Look changed | `Appearance.UpdateCreatedCharacter()`, `Appearance.LoadCharacter()` | |
| Held item | `PlayerHandItems.UpdateItemInHand(string hand, int amount, GameObject handitemPrefab, Item itm, ItemEquipable equipable)` (`:155`) | |

Gotchas:
- **Per-scene singletons.** Re-resolve everything after `SaveController.LoadingDone`. Only `DeveloperConsole`,
  `SaveController`, `InputManager`, `LoadingScreen`, `Achievements` and `PlayerIdentityMainMenu` survive scenes.
- **Cached at Awake/Start:** OSCC capsule/rigidbody (`Awake`), `PlayerCamera.playerCamera` (`Awake`),
  `FirstPersonHands.animator` (`Start`), `FirstPersonSettings` culling mask (`Awake`), `Appearance`/`AppearanceController`
  catalogs (`Start`). Changes made before those run can be overwritten.
- **Stale `PlayerStats` sub-objects after a load** (section 2).
- **Per-frame code to avoid piling onto:** `GameController.Update`, OSCC `Update`/`FixedUpdate`, `FallDamage.FixedUpdate`,
  `ThirdPersonCameraCollision.FixedUpdate`, `ThirdPersonCameraController.LateUpdate`, the four effect `Update`s,
  `PlayerStatsUI.Update`. Cheap postfixes are fine; allocations and `FindObjectOfType` are not.
- **`HidePlayerModel`/`ShowPlayerModel`** call `GetComponentsInChildren` and toggle every renderer under the player on each
  mode flip. Anything you parent under the player model has its renderers toggled too.
- **Mono inlining:** tiny members (`PlayerIsSitting`, `isCrouching`, `IsSleeping`) may be inlined at call sites, so a
  patch on them may miss callers. Poll them instead.
- **`SecondsPassed` subscribers** (`PlayerStats`, `Prison`, `Inventory`, perishing slots) share one static event, and
  `TimeOfDayAzure.UpdateCurrentTimeAndDay` wraps the whole invoke in one try/catch (`:464`), so an exception in any
  subscriber aborts the rest for that tick. Catch inside your own handler.
- **Console:** F1 opens `DeveloperConsole`, which already has `god_mode`, `kill`, `noclip`,
  `player_stats <name> add|subtract|set <amount>`, `change_scene`, `add_crime`, `reset_crime`, `add_item`. The routines in
  `DeveloperConsoleRoutines` are public statics and a known-good way to drive these features.
- **Spelling:** copy names exactly (`ToggleDeadthMessage`, `AlcoholAbsorbionRate`, `ThristStarvationDamage`,
  `dayLenghtInMinutes`, `ControlsDisabled` with `lockedFirsperson`).
