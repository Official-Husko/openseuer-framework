# NPCs, dialogue, quests, shops and the economy

Reference notes for mod authors. Paths are relative to `decompiled/Assembly-CSharp/` unless they start with `firstpass/` (that is `decompiled/Assembly-CSharp-firstpass/`). Line numbers are from the current decompile and will drift on game updates; match on member names.

Everything here was read from the decompile and the readable JSON in `Obenseuer_Data/StreamingAssets/`. Nothing was run in game, so anything marked "untested" is inference.

Contents:

1. Conventions and singletons
2. NPCs
3. Dialogue (PixelCrushers Dialogue System)
4. Quests and tasks
5. Mail, notes, phone
6. Money and the bank
7. Shops, vending machines, slot machines
8. Achievements
9. Developer console
10. Harmony hook table
11. Gotchas

---

## 1. Conventions and singletons

Most managers are `SavableScript` subclasses with a static `instance`. Saving goes through `SaveController.SerializeData(this, global)` (FullSerializer JSON in `Application.persistentDataPath/Saves/<Character>/<Save>/`). Dialogue Lua state is saved separately in `Globals.dialogue` (`SaveController.cs:543`, `PersistentDataManager.GetSaveData()`).

| Singleton | Type | Notes |
|---|---|---|
| `NPCManager.instance` | `NPCManager` | per-scene; list of live `NPCInfo` |
| `NPC.NPCDirector.instance` | `NPCDirector` | DontDestroyOnLoad; owns `NPCDirector.globalDatabase` (all `NPCData`) |
| `CharacterDatabase.instance` | `CharacterDatabase` | static `Characters` list loaded from `Resources/Characters` |
| `DialogueController.instance` | `DialogueController` | game UI/controls glue, not the PixelCrushers manager |
| `DialogueCommonMethods.instance` | `DialogueCommonMethods` | Lua helpers + dialogue timers |
| `TaskController.instance` | `TaskController` | task/objective state |
| `TaskItemsManager.instance` | `TaskItemsManager` | key items (not inventory items) |
| `MailController.instance` | `MailController` | mail and parcels |
| `NoteController.instance` | `NoteController` | notes and books |
| `Money.instance` | `Money` | wallet and bank accounts |
| `InvoiceController.instance` | `InvoiceController` | invoices (rent, debt, fines) |
| `ShopController.instance` | `ShopController` | furniture/train shop UI driver |
| `BankPanel.instance` | `BankPanel` | `.activeBank` is the `Bank` being used |
| `Achievements.instance` | `Achievements` | |
| `TimeOfDayAzure.instance` | `TimeOfDayAzure` | clock; `Day` Lua var is set from here |

Time events a framework will want: `TimeOfDayAzure.MinutePassed(int)` (static event fired once per game minute with the constant argument 60, i.e. game seconds, `TimeOfDayAzure.cs:477`), `TimeOfDayAzure.CurrentTimeAndDay(TimeAndDay)`, `DayChanged(int)`. `TimeOfDayAzure.Timer` is the save-aware timer used by dialogue timers, mail delay, shop restock. Wait for `TimeOfDayAzure.instance.updateTimeDisabled == false` before touching NPC or shop state after a load; almost every class in this doc does the same (`yield return null; while (updateTimeDisabled) ...`).

---

## 2. NPCs

### 2.1 Type map

| Type | File | Role |
|---|---|---|
| `Character` (ScriptableObject) | `Character.cs` | static identity: `ID`, `Name`, `LastName`, `Job`, `prefab`, `prefabWithAi`, `appearance`, `resident`. `FullName` = Name + " " + LastName |
| `CharacterDatabase` | `CharacterDatabase.cs` | `Resources.LoadAll("Characters")` into static `Characters`; `FetchCharacterByID(int)` |
| `NPCInfo` (MonoBehaviour, SavableScript) | `NPCInfo.cs` | scene component on the NPC prefab: references to `character`, `npcController`, `interactableTalk`, `characterHeadController`, `npcFov`; `State` enum `Any/Default/Sleeping/Dead`; `ChangeState(State)` (line 409), `Sleeping()` (580) |
| `NPCController` (VersionedMonoBehaviour) | `NPCController.cs` | per-frame brain: runs the current `Activity`, drives A* movement |
| `NPC.NPCData` | `NPC/NPCData.cs` | persistent per-NPC record: `ID`, `scheduler`, `state`, `prefabPath`; `Spawn()`, `MoveToCurrentLocation(NPCInfo)` |
| `NPC.NPCDataState` | `NPC/NPCDataState.cs` | saved runtime state: `currentScene`, `currentPosition`, `currentEntrypoint`, `targetScene`, `interSceneProgress`, `activePath`, `currentActivity`, `queuedActivities`, `schedulerDisabled` |
| `NPC.NPCDirector` | `NPC/NPCDirector.cs` | owner of `globalDatabase`; simulates NPCs that are not in the loaded scene |
| `NPCManager` | `NPCManager.cs` | per-scene registry and spawner; registers NPC Lua functions |
| `NPC.NPCReference` | `NPC/NPCReference.cs` | `{string name; int ID}` handle used by mail, vending machines, markets. `new NPCReference(int)` NREs if `NPCDirector.instance` is null |
| `NPC.Schedule`, `ScheduledAction` | `NPC/Schedule.cs`, `NPC/ScheduledAction.cs` | timetable |
| `NPC.Activity` and `Act_*` | `NPC/Activity.cs`, `NPC/Act_*.cs` | behaviours (see 2.5) |
| `NPCPathfinding` | `NPC/NPCPathfinding.cs` | waypoint-graph path search over A*, plus inter-scene paths |
| `InteractableTalk` | `InteractableTalk.cs` | the "Talk" interactable on NPCs; the thing that actually starts dialogue |

`CharacterManager` and `CharacterSpawner` (`CharacterManager.cs`, `CharacterSpawner.cs`) are a minor legacy random-spawner (shuffles a `characters[]` prefab array over `spawnPoints[]`, rejects duplicates by `GameObject.name`). Real NPC placement does not use them.

### 2.2 Where NPC data lives

- **`Character` assets** are Unity ScriptableObjects under `Resources/Characters`. They are not readable from disk. `StreamingAssets/Characters.json` is a stale 40-entry export (UTF-16, objects separated by commas but without an enclosing array; wrap in `[ ]` to parse). It has IDs, names, ages, jobs, appearance but is not what the game loads. IDs seen: 0 Markku Siltanen, 1 Mr. Duck, 2 Mirjam Freighter (Cashier), 3 Ivan Tsarnov, 11 Pentti Penttilä (Bartender), 24 Oscar Nilsson (Salesperson), 37 Malone Petronis (Moonshiner), 999 "Tenant" (template), and so on. The real set is bigger: `NPCBehavior.json` has IDs that are not in `Characters.json` (the code special-cases ID 107, "Resident/Crazy Neighbor", at `NPCDirector.cs:63`).
- **`StreamingAssets/NPCBehavior.json`** is the schedule database, read with `File.ReadAllText` by `NPCDirector.LoadDatabase()` (`NPC/NPCDirector.cs:192`) and deserialised with FullSerializer into `List<NPCData>`. Plain UTF-8 with BOM. 243 entries, 240 distinct IDs. Per entry: `ID`, `startScene`, `startPosition`, `schedulerDisabledAtStart`, `scheduler {Name, Timetable[], RandomActivities[]}`, `prefabPath`. 155 entries have `scheduler: null`; only 15 have a non-empty `Timetable`. `Activity` objects are polymorphic via `"$type"`: `NPC.Act_Police` (49), `NPC.Act_Sleep` (23), `NPC.Act_sit` (13), `NPC.Act_Stand` (35), `NPC.Act_Tenement` (10), `NPC.Act_Patrol` (7), `NPC.Act_Robber` (7). Editing this file is a supported way to change schedules, since the game only writes it in the editor (`SaveDatabase` returns early outside the editor).
- **`StreamingAssets/CharactersRent.json`** is read by `CharacterRentDatabase` (`CharacterRentDatabase.cs:19`, LitJson). Per resident: `Income1..6`, `Method`, `MinCondition/MaxCondition`, `Smallest/Biggest` flat size, floor range, bathroom/heating/kitchen/water needs, `Family1..4`. This drives who rents which tenement apartment and for how much.
- **Saved state**: `NPCDirector.OnSavingGameSpecial` copies each `NPCData.state` into `savedData`; on load `OnLoadingGameSpecial` replaces `globalDatabase[i].state` with the saved one and sets `dontLoadDatabase` so the JSON is not re-read over the top (`NPCDirector.cs:34-89`). Schedules themselves are not saved; they always come from the JSON.

### 2.3 Spawning and despawning

NPCs are scene prefabs. There is no general pooled spawner.

1. **Scene start.** `NPCManager.Start` runs `StartDelay` (`NPCManager.cs:64`): waits for the clock, then six `WaitForEndOfFrame`s, then loops `NPCDirector.globalDatabase`. For each NPC whose scheduler is not disabled and whose `state.currentScene == activeScene`: if an `NPCInfo` with that ID already exists in the scene, `MoveToCurrentLocation`; otherwise `NPCData.Spawn()`. NPCs whose `currentScene` is some other non-empty scene and that have an `NPCInfo` here (and not `preventDestroy`) are `SetActive(false)`, not destroyed.
2. **Position.** `NPCData.GetLocation` -> `NPCDataState.GetLocation` (`NPCDataState.cs:98`) picks, in order: saved position if there is ground below it, the point along the active inter-scene path, the scheduled activity's `GetLocationInfo()`, the controller's current transform, and finally a random `PlayerLevelEntrypoints` entry.
3. **Instantiate.** `Character.Spawn(Vector3, Quaternion)` (`Character.cs:203`): if `NPCManager.FindNpcById` already has one it just re-enables it; otherwise `Instantiate(prefabWithAi ?? prefab, NPCSpawnUtilities.FindValidSpawnPosition(...))` and `NPCManager.AddNpcToList`. `NPCData.GetCharacterPrefab` falls back to `Resources "Prefabs/Characters/NPC base"`.
4. **Placed spawners.** `info_NPCSpawn` (`info_NPCSpawn.cs`) instantiates a specific NPC once per scene if its `state.currentScene` is empty or equals the active scene.
5. **Arriving mid-scene.** `NPCSceneUtilities.ChangeNPCLevel` / `NPCSpawnUtilities.ChangeNPCLevel` spawn or teleport the NPC at the right entrypoint door (`Waypoint_LevelChange.OnSpawn`), using `NPCManager.MarkNpcSpawnPending(id)` to avoid double spawns.
6. **Despawn.** Leaving a scene destroys the scene's objects; the NPC lives on only as `NPCData`. `NPCInfo.OnDestroy` calls `NPCManager.RemoveNpcFromList`. `NPCInfo.HideRentedResident()` destroys a story NPC whose tenement apartment the player rented (`NPCInfo.cs`, `CheckRentedResidentDelay`). `NPCInfo.ChangeState(State.Dead)` disables `NPCController`, `NPCFov`, `InteractableTalk` and barking but leaves the object.

Simulation while the player is elsewhere: `NPCDirector.DeltaSeconds(int)` is subscribed to `TimeOfDayAzure.MinutePassed` (`NPCDirector.cs:245`). Per NPC, per game minute: accumulate `timeInLevel`; if the NPC is not in the active scene run `NPCSceneUtilities.UpdateNPCTransition` (moves `interSceneProgress` along the path at 1.5 m/s, completes a transition after `maxTransitionTimeInGameSeconds = 3600`); if following a target call `OnFollowingTarget`; else `Schedule.UpdateScheduler`; finally `HandleNPCChangeScene`. So off-screen NPCs are a per-minute state machine over scene names, not real movement.

Lookup API: `NPCManager.instance.FindNpcById(int) : NPCInfo` (prefers an active one), `FindNpcControllerById(int)`, `GetAllNPCs() : List<NPCInfo>`, `NPCDirector.instance.GetNPCDataByID(int) : NPCData`, `InteractableTalk.interactableTalks` (static list; match on `.title == character.FullName`).

### 2.4 What drives an NPC each frame

`NPCController.Update` (`NPCController.cs:238`), gated by `activityDisabled`, `Data != null` and `updateEnabled` (set true by `StartDelay` after the clock is running and `NPCInfo.ActivityRestorePending` is false):

1. If there is no running `Data.state.CurrentActivity`, clear it and pop `queuedActivities[0]` into `StartActivity`.
2. Otherwise call `CurrentActivity.OnActivity()` (virtual; each `Act_*` does its own work, usually state machines that call back into `SetTargetNode`/`SetTargetPosition`).
3. `UpdatePathfinding()`: runs `whileAtNode`/`onLeaveNode` callbacks for the node the NPC stands at, handles queued A* repaths, `currentPath.ArriveAtTarget`, and `CheckStuck()`.
4. If `lookAtPlayer` (set by `OnTalk`), `RotateNPC(player position)`.

The movement layer is Aron Granberg's A* Pathfinding Project: `NPCController.ai : IAstarAI` and `richAI : RichAI` obtained from the same GameObject. `NPCPathfinding.instance.SearchForPath(start, end, NPCReference, Action<Path>)` builds a path over the hand-placed `Waypoint`/`WaypointGraph` network, caching an A* `ABPath` between consecutive waypoints. Free movement (`SetFollowDestination`, `SetChaseDestination`) writes `ai.destination` directly.

Activity control surface (all on `NPCController`):

| Member | Line | Notes |
|---|---|---|
| `bool StartActivity(Activity a, bool interrupt = false)` | 283 | returns false if a running activity blocks it (needs `interrupt`, and priority/`force` rules) |
| `void ForceStartActivity(Activity a)` | 323 | stops current then starts |
| `void ForceStopActivity()` | 340 | also resets the scheduler's next check |
| `void QueueActivity(Activity a)` | 278 | |
| `void DisableActivity()` / `EnableActivity()` | 317 / 312 | `activityDisabled` also makes `Schedule.CurrentTime` return early |
| `SetTargetNode(Node, onArrive, whileAt, onLeave)` | 365 | path to a scene `Node` |
| `SetTargetPosition(Vector3, float minDistance = 0.5, Action onArrive = null)` | 390 | |
| `SetFollowDestination(Vector3, float)` / `SetChaseDestination` | 410 / 438 | direct A*; chase falls back to waypoint path if A* says unreachable |
| `MoveToPosition(Transform, Action, bool parent)` | 634 | scripted move (forced movement, disables `richAI.canMove`) |
| `Act_Follow FollowPlayer(bool run)` and the 3-arg overload | 839 / 844 | |
| `StartPatrol(NodeGroup)`, `StartStand(NodeGroup)`, `MoveToNode(Node)` | 863 / 870 / 855 | build an `Act_*` and start it with `interrupt: true` |
| `bool StartDialogue(string conversation)` | 819 | goes through `InteractableTalk` (see 3.3) |
| `DisableMoving(GameObject)` / `EnableMoving(GameObject)` | 794 / 810 | ref-counted by the object passed; `ai.isStopped` toggles |
| `bool isIdle`, `bool InCurrentLevel()` | 90, 759 | |

### 2.5 Activities and schedules

`Activity` base (`NPC/Activity.cs`): `activityName`, `priority` (default 1), `force`, `loop`, `canBeInterrupted`, events `OnStart/OnFinish/OnInterupt/OnStopped`, virtuals `StartActivity(NPCController)`, `OnActivity()`, `InterruptActivity()`, `GetLocationInfo()`, `GetTargetScene()`, `GetFollowTarget()`. Subclasses: `Act_Stand`, `Act_Sit` (`Act_sit`), `Act_Sleep`, `Act_Patrol`, `Act_Wander`, `Act_Follow`, `Act_Tenement`, `Act_Police` (states `Idle, Stand, Patrol, Wander, ChasePlayer, QuestionPlayer, SearchingPlayer, SearchingPlayerAgressive`), `Act_Robber` (states `Idle, Stand, Patrol, Wander, Chase, Aggro`). The police and robber activities also register Lua functions (section 3.5) and talk to `Crime.instance`.

`Schedule` (`NPC/Schedule.cs`): `Timetable` and `RandomActivities` lists of `ScheduledAction {name, disabled, interruptPrevious, chance, starts, duration, activity}`. `starts`/`duration` are `TimeAndDay` with `weekDay` 0-6, `hours`, `minutes`. A week is 10080 minutes; an action whose end passes 10080 wraps. `Schedule.CurrentTime(TimeAndDay)` (line 44) is subscribed to `TimeOfDayAzure.CurrentTimeAndDay`; it returns early while the current activity cannot be interrupted or `TotalMinutes < nextCheck && !isIdle`; picks the first timetable entry covering "now" and tries `TryStartActivity` (random `chance` roll, then `NPC.StartActivity(act, interruptPrevious)`); if nothing matched it rolls a random entry from `RandomActivities`. For NPCs not in the active scene `Schedule.UpdateScheduleWithoutController` just updates `currentScheduledAction` so the director knows which scene they should be heading to.

BehaviorDesigner: the full runtime and task library are in the assembly (`BehaviorDesigner.Runtime*`, including `Tasks.DialugeSystem.*` and `Tasks.Movement.AstarPathfindingProject.*`). No C# in the NPC code references `BehaviorTree`. If any trees are used they are assigned in prefabs/scenes and are not the main NPC brain, which is the Activity system above. `BehaviorDesigner.Runtime.Tasks.DialugeSystem.RunLua` / `StartConversation` / `Bark` are available to such trees. The `GetQuestState`/`SetQuestState` tasks use PixelCrushers `QuestLog`, which the game's own task system does not use (section 4).

Other per-NPC components worth knowing: `NPC_FOV` (sight/witness checks, used by police and `NPCRandomTalk.OnPlayerSpottedFOV`), `NPCRandomTalk` (barks on spotted/witnessed/guitar/soiled pants), `CharacterBarkStarter.TryToComment(string conversation)`, `CharacterHeadController` (look-at), `OpenSewerNPCMovementAnimation` (animator speeds), `NPCStats`.

### 2.6 Character assets and bodies

Read in game 0.5.13 (Open Sewer Bazaar loaded):

- `CharacterDatabase.Characters` holds 294 `Character` assets (index 0 is the player). Only 79 have a prefab (`prefabWithAi ?? prefab`), all of them with an `NPCInfo`. Of those, 2 have a slider body (`Appearance`, the character editor's rig): 126 Lizzy Mopper and 146 Empty Test Map Police. The other 77 are baked: a `CharacterAppearance` that builds the body from `Character.appearance.appearanceMeshes` (mesh name, materials, blend shape names and values per child renderer). 215 characters have no prefab; 40 of those carry worn items (a `Head` wearable) for a slider body.
- Baked bodies are made of child skinned meshes such as `average_body`, `male_head`, clothing meshes (`karl_trenchcoat_pants`); each has 83 to 85 blend shapes, mostly facial (`_01UpperLidLower`, `_02UpperLidRaise`...). `CharacterAppearance.UpdateAppearance()` shows only the children listed in the mesh list and applies their materials and shapes.
- `Instantiate` on a `Character` deep-copies the nested `Appearance` lists, so a copy can be edited without touching the asset.
- `NPCController.Start` takes `NPCDirector.GetNPCDataByID(ID) ?? new NPCData()`, so an NPC with an id the schedule database does not know (a mod NPC) gets an empty `NPCData` with no scheduler and stands still. `NPCData.character` looks the id up in `CharacterDatabase.Characters`, which is why a copy must be in that list before its body wakes up. `NPCInfo.Start` sets the body's `Appearance.createdCharacter` to `NPCInfo.character`.
- `SaveController.SerializeData` skips any savable script whose `GUID` field is empty, so clearing the GUIDs of a spawned body (`SaveController.SetObjectGUID(part, "")`) keeps it out of the save.
- `NPCInfo.ChangeState(Dead)` is also how a loaded dead NPC is put back (`NPCInfo.Start` calls it when the saved state is not `Default`), so "was alive before the call" is the test for a real death.
- Animals: `AnimalStats` saves `animalData` (tame, health, hunger, age) per save id, but nothing copies `animalData.tame` back to `Animal.tame` for an animal of the scene after a load; only `SetAnimalStatsMeta` (an animal spawned from an item) does.

---

## 3. Dialogue

### 3.1 Architecture

The game embeds the PixelCrushers Dialogue System (decompiled in `firstpass/PixelCrushers.DialogueSystem/`). One persistent `DialogueSystemController` (`dontDestroyOnLoad = true`) holds a `DialogueDatabase` ScriptableObject with actors, conversations, variables. The database asset is inside Unity data files and is not readable from disk; dump it at runtime from `DialogueManager.MasterDatabase` (`.conversations`, `.actors`, `.variables`).

The game wraps it with three classes of its own:

- `DialogueController` (`DialogueController.cs`): UI/controls glue. Not a conversation API.
- `InteractableTalk` (`InteractableTalk.cs`): the NPC "Talk" interactable. Requires a PixelCrushers `ConversationTrigger` on the same object, which holds the NPC's default conversation title.
- `DialogueCommonMethods` (`DialogueCommonMethods.cs`): registers common Lua helpers and owns named timers.

`DeleteOtherDialogueManagers` (on some prefabs) destroys every `DialogueSystemController` in the scene that is not its own object (`Awake` and `Start`). If a scene prefab with that component ever replaces the persistent controller, a database you added with `DialogueManager.AddDatabase` goes with it. Re-add on scene load to be safe (untested).

Name clash: the game has its own global `ConversationStarter : MonoBehaviour` (`ConversationStarter.cs`, `conversationName` + `startConversation()` calling `DialogueManager.StartConversation`) and PixelCrushers has `PixelCrushers.DialogueSystem.ConversationStarter` (base of `ConversationTrigger`). Qualify the type in mod code.

### 3.2 `DialogueController` (game UI glue)

- `DialogueController.instance`, `bool DialogueActive`, `DialogueSystemController dialogueSystem`, `Action onDialogueClosed`, `InteractableTalk currentInteractableTalk`.
- `GameController.Update` (`GameController.cs:38`) calls `CheckDialogueStatus()` every frame. It compares `dialogueSystem.IsConversationActive` with `DialogueActive` and calls `OnDialogueOpen()` (hides game menu, `PlayerStats.instance.Active = false`, `GameController.instance.ControlsDisabled(gameObject, ShowCursor: true)`, shows subtitles) or `OnDialogueClosed()` (re-enables controls, then **`onDialogueClosed()` is invoked without a null check**, then `DialogueActive` drops 0.01 s later).
- So any conversation started anywhere, including a raw `DialogueManager.StartConversation`, still locks and unlocks player controls automatically. What it does not do is the NPC side (look-at, `NPCController.OnTalk`, relay registration). That only happens through `InteractableTalk.OnDialogueStart`.
- Continue/response navigation (`Update`, `MoveUp/MoveDown`) is keyboard/controller glue on top of the PixelCrushers UI; `SetContinueDisabled(bool)`, `SetCharacterSound(RandomAudio)`, `SetSalsaAudio`, `SetSalsaTextSync` feed the typewriter/lip-sync.

### 3.3 Starting a conversation from code

Three levels, from lowest to highest:

```csharp
// 1. Raw. Player locks via DialogueController; NPC does not react.
DialogueManager.StartConversation("Some Conversation", playerTransform, npcTransform);

// 2. Through the NPC's InteractableTalk (preferred; mirrors what pressing Use does).
var talk = InteractableTalk.interactableTalks.Find(t => t.title == "Pentti Penttilä");
talk.ForseStartDialogue("Conversation Title");   // force: true, ignores CanTalk() and open dialogue
talk.CheckStartDialogueInterrupt(title, force: false, ignoreMenuState: true); // respects CanTalk()

// 3. Via the NPC controller (ignoreMenuState: false: refuses if any menu is open)
npcInfo.npcController.StartDialogue("Conversation Title");
```

Details of `InteractableTalk.OnDialogueStart(string dialogue, bool tryStartConversation = true)` (line 236): registers the object's `RelayDialogue[]` and `DialogueCondition[]` Lua functions, `CharacterItemManager.RegisterDialogue`, fires `onStartTalk`, calls `NPCController.OnTalk()` (stops the NPC, `lookAtPlayer`), sets `DialogueController.instance.onDialogueClosed = OnDialogueEnded`, then:

- if `dialogue` is non-empty: `DialogueManager.StopAllConversations(); DialogueManager.StartConversation(dialogue, OpenSewerCharacterController.instance.transform)` (actor = player, no conversant transform);
- if empty: `conversationTrigger.TryStartConversation(player)` which uses the NPC prefab's default conversation.

It then starts a 1-second `DelayCheck` that closes the UI if no conversation became active (so a bad conversation title fails quietly after a second). `CanTalk()` blocks on `talkDisabled`, `ContestController.ContestActive`, the wasteland/SMV state, player not grounded, `WaitingController` waiting, or an NPC using a level-change door. `OnDialogueEnded` (the `onDialogueClosed` target) undoes the registrations.

Gotcha: `DialogueController.onDialogueClosed` is never cleared. After a normal NPC talk it keeps pointing at that NPC's `OnDialogueEnded`, and a later raw conversation will call it again when it closes (re-fires `onEndTalk` relays and `onEndTalkCallback`). Set `DialogueController.instance.onDialogueClosed = () => {}` before a raw `StartConversation`, or restore it after.

Phone calls and `PhoneNumber` reuse this: `Phone.StartCall` sets `conversationTrigger.selectedDatabase = number.database; conversationTrigger.conversation = number.conversation; interactableTalk.Interact();` (`Phone.cs:231`). That is also the only place the game attaches an extra `DialogueDatabase` to a conversation start.

### 3.4 Lua variables

All through PixelCrushers statics in `PixelCrushers.DialogueSystem` (`firstpass/PixelCrushers.DialogueSystem/DialogueLua.cs`):

```csharp
DialogueLua.GetVariable("Day").AsInt            // Lua.Result: .AsInt .AsFloat .AsBool .AsString .isNumber ...
DialogueLua.SetVariable("PlayerFirstName", "Ada");
DialogueLua.GetActorField("Player", "Money").AsInt
DialogueLua.SetActorField("Player", "RealMoney", 12.5f);
Lua.Run("Variable[\"Day\"] = 5");                // arbitrary Lua
Lua.IsTrue("Variable[\"Day\"] > 3");
```

Variables the game itself reads or writes (grep'd):

| Name | Kind | Writer |
|---|---|---|
| `Day` | variable | `TimeOfDayAzure` (line 541), also `SURVIVE_*` achievements |
| `PlayerFirstName`, `PlayerLastName`, `PlayerAge`, `PlayerGender` | variable | `PlayerIdentity.cs:72` |
| `Player.{FirstName, LastName, Age, Gender, Pronoun, PronounCapitalized, AgeName, AgeNameFormal, AgeNameFormalShort}` | actor field | `PlayerIdentity.cs:76-98` |
| `Player.Money` (OC), `Player.RealMoney` (RM) | actor field | `Money.AddMoney/RemoveMoney` only |
| `Price`, `Currency` | variable | `MarketShop.cs:568` (set when the cashier quotes a total) |
| `<actorName>.Price/PayOc/PayRm` | actor field | `GasStation.cs` |
| `WakeUpReason`, `PoliceIdle`, `DREAM_1` | variable | waiting/police/shroom code |
| `Resident/Crazy Neighbor.LastVisitDay` | actor field | read by `NPCDirector.cs:73` |
| `Generic/Card Games.Tutorial*Done` | actor field | card game tutorials |
| `<actor>.KnowsTheName` | actor field | read by `InteractableTalk.HideName` when `hasToKnowTheName` |
| `Generic_Prison_guard.HasBagItems ` | actor field | `Prison.cs:339` (note the trailing space in the key, a typo you must reproduce to match) |

Persistence: the whole Lua table is written to `Globals.dialogue` by `PersistentDataManager.GetSaveData()` on save and re-applied with `ApplySaveData` at the very end of `SaveController.LoadSaveGameDifferentScene` (`SaveController.cs:814`). Any variable you set during a load will be overwritten by that apply. Variables you create from a mod are saved for free (they are just Lua globals) but vanish from saves if the mod is removed and re-created differently.

Typed helper wrappers in game code: `DialogueVariable` (actor/variable + compare/set spec used by scene logic, `DialogueVariable.cs`) and `ChangeDialogueVariableValue` (Unity-event friendly setters).

### 3.5 Lua functions the game registers

All via `Lua.RegisterFunction(string name, object target, MethodInfo method)` (`firstpass/.../Lua.cs:293`). Conventions: numbers arrive as `double`, so integer parameters are declared `double` and cast; strings and bools map directly. **Registering an existing name is silently ignored** (the first registration wins, a warning only if `Lua.warnRegisteringExistingFunction`). `UnregisterFunction` sets the name to nil. Most of these register in `Awake`/`Start` of a scene object and unregister in `OnDestroy`, so they exist only while that object does; a few are registered per interaction (below).

| Group | Functions | Source |
|---|---|---|
| Common | `GenerateRandom(n)`, `GetGeneratedRandom()`, `GetRandom(n)`, `SkillCheck(skill, failChance)`, `GetSkillLevel(skill)`, `CreateTimer(name, minutes)`, `RemoveTimer(name)`, `CheckTimer(name)`, `SetWaveEffect(bool)`, `SetWaveEffectIntensity(n)` | `DialogueCommonMethods.cs:79` |
| NPC | `ChangeInteractableTalk(id)`, `LookAtNamedTarget(id, name)`, `LookAtNPC(id, targetId)`, `LookAtPlayer(id)` | `NPCManager.cs:187` |
| Animation | `AnimatorTrigger`, `AnimatorTriggerDelay`, `AnimatorSetBoolToTrue`, `AnimatorSetBoolToFalse` | `SetAnimatorValue.cs:214` |
| Tasks | `TaskActive(id)`, `TaskAnyStatus`, `TaskFinished`, `TaskFailed`, `TaskAbandoned`, `ObjectiveAnyStatus(task,obj)`, `ObjectiveActive`, `ObjectiveAbandoned`, `ObjectiveFailed`, `ObjectiveCompleted`, `SetObjectiveFinished(task,obj)` | `TaskController.cs:467` |
| Key items | `PlayerHasTaskItemId(id, amount)` | `TaskItemsManager.cs:313` |
| Money | `AddMoneyDialogue(amount, rm)`, `RemoveMoneyDialogue(amount, rm)`, `CheckMoneyDialogue(amount, rm)` | `Money.cs:237` |
| Inventory | `CheckForItemDialogue*`, `RemoveItemDialogue*`, `AddItemDialogue*`, `SellItemDialogue(id, rm)`, `SellItemDialogueCategory(cat, rm)`, `CountItemsDialogue`, `InventoryIsNotFull`, ... (24 total) | `Inventory.cs:194` |
| Bank | `CheckIfPlayerHasAccount`, `CreateAccount`, `EnterBank`, `GetLoanDetailsDialogue`, `GetOwedAmountDialogue`, `CheckIfLoanAmountIsZero`, `CheckIfPlayerOwesNothing` | `Bank.cs:637` (`Bank.RegisterDialogue()`/`UnRegisterDialogue()`, called from scene logic around the conversation, not from C#) |
| Trading | `StartTrade()` | `Trade.cs:314` (registered by that trader's scene logic around the conversation) |
| Crime/prison | `GetSentenceLength*`, `GetCrimeAmount`, `GetCrimeFineAmount`, `CountStolenGoodsValue`, `GoToPrison`, `TeleportToPrison`, `PayFine`, `CanPayFine`, `CanBribe`, `TryToBribe`, `CalculateBribe`, `HasStolenGoods`, `InPrison`, `AskForSentenceTime` | `Crime.cs:127`, `Prison.cs` |
| Police/robbers | `QuestioningPlayer`, `ArrestingPlayer`, `StartChase`, `ForceNewState`, `Aggro`, `RobPlayer`, `TryNegotiate` | `NPC/Act_Police.cs:433`, `NPC/Act_Robber.cs:325` |
| World | `CanRentApartment`, `ResidentRented`, `ResidentRentedAmount`, `ResidentApartmentLevel`, `GiveTenementTask5`, ... (10), `TriggerJanitorAction`, `HasJanitorAction`, `CheckAvailability`/`EnterReservation`/`CheckPlayerRoomStatus`/`GetPlayerRoomName` (hotel), `StartBetting`/`CollectWinnings`/`CanPlaceBet`/`CanCollectWinnings` (rat arena), `GiveRecipe`, `SubscriptionIdActive`, `RelationshipIdActive`, legal-services functions, `GetPlayerStatDialogue`, `GetPriceForOSPhone`, `HasSuitableDevicesForOSPhone`, `TellTimeResponse`/`GetHour`/`GetDay`, `startDream`, `HasSelectedStart` | various |
| Per-instance, dynamic names | `Relay_<relayName>` (`RelayDialogue`), `Condition_<name>` (`DialogueCondition`), `<dialogueMethodStart>_PlayerHasItems`, `_GiveItems`, `_TakeItemsFromPlayerInventory` (`CharacterItemManager`) | registered by `InteractableTalk.OnDialogueStart` for the NPC being talked to |

`Market.cs` (`MarketBuy`, `MarketReturnStuff`) is dead code: the bodies throw `NotImplementedException`. The working supermarket is `MarketShop` (section 7).

`DialogueEventSystem` (ScriptableObject, `DialogueEventSystem.cs`) is a bag of methods the dialogue DB can call through Unity events: `AddMoneyOC/RM`, `RemoveMoneyOC/RM`, `GiveItem`, `GiveRecipe`, `Commit*Crime`, `GoToPrison`, `TriggerAchievement(string)`, `SellReceipts*`, `GiveStolenItemsAndReturnHalfOfOC`.

### 3.6 Adding your own Lua function

```csharp
public class ModLua { public double Add(double a, double b) => a + b; public void Hello(string s) { ... } }
var h = new ModLua();
Lua.RegisterFunction("ModAdd", h, typeof(ModLua).GetMethod("Add"));
// from Lua: ModAdd(1, 2)
```

Guidelines: use `double`/`string`/`bool` parameters and `double`/`string`/`bool`/`void` returns; the name must not already exist or your registration is dropped; the PixelCrushers `Lua` environment is static and survives scene loads, but anything that calls `UnregisterFunction` with your name (nothing in the game does for custom names) would clear it. Register after `DialogueManager.hasInstance` (the persistent manager exists after the first scene), then call `Lua.Run("return ModAdd(1,2)")` once to prove it works. Function names registered by scene objects (table above) can be overridden for the lifetime of that scene by calling `Lua.UnregisterFunction(name)` then `RegisterFunction` yourself, though the scene object will then fail to re-register until you free it.

### 3.7 Adding a new conversation (untested sketch)

Build a `DialogueDatabase` in code and add it:

```csharp
var db = ScriptableObject.CreateInstance<DialogueDatabase>();
db.baseID = 90000;                          // keep IDs away from the shipped DB
db.actors.Add(new Actor { id = 90001, fields = ... });   // fields: "Name", "Is Player" ...
var conv = new Conversation { id = 90002, fields = ... }; // "Title", "Actor", "Conversant"
conv.dialogueEntries.Add(new DialogueEntry { id = 0, isRoot = true, ... });
db.conversations.Add(conv);
DialogueManager.AddDatabase(db);            // merges into MasterDatabase
DialogueManager.StartConversation("My Conversation", player, npc);
```

Open points: the shipped DB's `Template` (`Template.FromDefault()`, `firstpass/.../Template.cs`) defines the field titles the UI expects (`Title`, `Actor`, `Conversant`, `Dialogue Text`, `Menu Text`, `Sequence`, ...); entries need `fields` with `Dialogue Text` and `outgoingLinks` pointing at the next entry; variables you reference in conditions need to exist in `db.variables` or be created by `DialogueLua.SetVariable` first. The alternative with less risk is to load a Chat Mapper / JSON export via the library's importers, or to skip the database entirely and use `DialogueManager.Bark`/`ShowAlert` and your own UI. A database added at runtime is not part of saves; recreate it every launch. `ConversationTrigger.selectedDatabase` (used by `Phone`) lets one conversation start pull a conversation from an extra DB without merging it.

For phone numbers specifically a mod can append to the static list `PhoneNumberDatabase.database` (filled from `Resources "Dialogue/Phone"` in `Awake`, line ~20): create a `PhoneNumber` ScriptableObject (`number: int[]`, `network`, `answerChance`, `conversation`, `database`, `characterSounds`). `PhoneNumberDatabase.CallNumber` then resolves it.

Dialogue timers (`CreateTimer(name, minutes)`) are `DialogueCommonMethods.timers`, saved with the game; `CheckTimer` returns the `completed` flag only. Minutes are rounded to an int before converting to seconds.

---

## 4. Quests and tasks

The game does not use PixelCrushers quests. It has its own system built from ScriptableObject assets (loaded through Addressables) and one state holder, `TaskController`.

### 4.1 Types

| Type | File | Meaning |
|---|---|---|
| `TaskInfo` : `SavableScriptableObject` | `TaskInfo.cs` | quest definition: `id`, `title`, `description`, `finishTaskWhenTheresNoActiveTasks`, `requiredActivateTasks`, `firstObjectives` |
| `TaskObjective` : `SavableScriptableObject` | `TaskObjective.cs` | step definition: `id`, `title`, `counterAmount`, `requiredActivateObjectives`, `requiredCompleteObjectives`, `onFinished/onAbandoned/onFailedObjectives`, back-pointer `task`; virtuals `OtherActivateRequirements()`, `OtherCompleteRequirements()`, `CompleteEffects()` |
| `TaskStatusInfo` | `TaskStatusInfo.cs` | `{TaskInfo task; CurrentTaskStatus status; List<ObjectiveStatusInfo> objectives; bool renovation}` |
| `ObjectiveStatusInfo` | `ObjectiveStatusInfo.cs` | `{TaskObjective objective; ObjectiveStatus status; bool optional; int currentAmount, counterAmount}` |
| `CurrentTaskStatus` | `CurrentTaskStatus.cs` | `Active, Failed, Finished, Abandoned, AnyStatus` |
| `ObjectiveStatus` | `ObjectiveStatus.cs` | `Active, Failed, Finished, Abandoned, Hidden, AnyStatus, NotActive` |
| `EditTask` (MonoBehaviour) | `EditTask.cs` | scene trigger that applies a status change; used by level logic and relays |
| `TaskItem`, `TaskItemsManager` | `TaskItem.cs`, `TaskItemsManager.cs` | key items (letters, blueprints, keys); separate from `Item` inventory items; `AddTaskItem(TaskItem, int amount = 0, ...)` at line 136 |
| `TaskPanel`/`TaskUIPanel` | | the in-game task list UI |

Ids: `TaskInfo.Id` / `TaskObjective.Id` return `id`, else `addressableAssetPath`, else `title`. Lua and the console address tasks by this string. Objective keys in the string lists are `taskId + "_" + objectiveId`.

### 4.2 State storage

`TaskController` (`TaskController.cs`) holds `List<TaskStatusInfo> allTasks` plus ten parallel `List<string>` index lists (`anyStatusTasksIds`, `activeTasksIds`, `failedTasksIds`, `abandonedTasksIds`, `finishedTasksIds`, and the five `...Objectives` lists) that the Lua functions read. It saves as a global object (`GUID "TaskController"`, `SerializeData(this, global: true)`). Task and objective ScriptableObjects are not serialised by value: `SavedAssets` (`SavedAssets.cs`, `assetTypes` = `TaskInfo, TaskObjective, NoteInfo, BookInfo, Furniture, TenementUpgrade, TenementUpgradeGroup, TenementContractor, Landmark`) writes their `addressableAssetPath` and resolves them with `Addressables.LoadAssetAsync` on load; `TaskStatusInfo.LoadFromPath()` does the same fixup after deserialising. Consequences:

- A mod can drive existing tasks by id. Creating brand-new `TaskInfo`/`TaskObjective` at runtime (`ScriptableObject.CreateInstance`) has no addressable path, so `LoadFromPath` logs "Path is empty!" and the saved reference falls back to the object as serialised. Whether a runtime-only task survives a save/load cycle is untested; budget time to test it, and consider keeping mod quest state in your own save file instead.
- `OnLoadingGame` also re-adds every task to `TaskPanel` and runs `FinishIfNoActiveObjectives`.

### 4.3 Starting, advancing and finishing

Everything funnels through two methods:

```csharp
// TaskController.cs:150
bool EditTask(TaskInfo task, TaskObjective objective, ObjectiveStatus newObjectiveStatus,
              bool optional, bool createNewTask, CurrentTaskStatus newTaskStatus,
              bool objectiveHasToExist = false, bool taskHasToBeActive = true, bool renovation = false)
// TaskController.cs:266
void EditTaskStatus(TaskStatusInfo task, CurrentTaskStatus status)   // + overload taking TaskInfo (235)
```

`EditTask` behaviour: fails (returns false) if `task.CheckActivateRequirements(allTasks)` is not met; with `createNewTask` and no existing status it creates the `TaskStatusInfo` ("New Task" notification, `TaskPanel.AddActiveTask`); requires the task to be Active when `taskHasToBeActive`; checks `objective.CheckActivateRequirements`; creates the objective entry unless `objectiveHasToExist`; refuses to re-activate an already-existing objective; then `EditTaskObjective`. When the objective becomes `Finished` and `CheckCompleteRequirements` passes it runs `CompleteEffects()`, shows "Objective Complete", and activates the `onFinishedObjectives` chain. If `newTaskStatus == Active` and the task has `finishTaskWhenTheresNoActiveTasks` with no active objectives left, the task becomes `Finished` automatically (`ShouldFinishTask`). Moving a task to a non-Active status abandons all still-active objectives and creates the matching notification.

Convenience wrappers (all end in `EditTask`):

- `TaskObjective.TaskObjectiveToActive(bool optional)`, `...ToFinished`, `...ToFailed`, `...ToAbandoned` and `...NoNewTask` / `...ObjectiveHasToExist` variants (`TaskObjective.cs:146-191`), `EditObjective(optional, status, createNewTask, objectiveHasToExist, taskStatus, taskInfo)` (211), `IncreaseAmount(int)`, `SetAmount(int)`.
- `TaskInfo.GiveFirstObjectives()`, `TaskToFinished()`, `TaskToFailed()`, `TaskToAbandoned()` (`TaskInfo.cs:116-136`).
- Counters: `TaskController.EditTaskCounter(task, objective, amount, setamount = false)` (355) clamps to `counterAmount`, auto-finishes the objective when it hits the max unless `dontSelfCompleteWithCounter`, otherwise shows "Objective Updated".
- Lua `SetObjectiveFinished(taskId, objectiveId)` (only works if the objective is currently in `activeObjectives`).
- Mail: `MailController.TaskMail` and `TaskItemMail.objective` can change a task when a mail is taken (see 5.1).

Every state change also pushes a UI notification through `Notifications.instance.CreateNotification(type, text)` and raises the static events `TaskController.TaskUpdated` and `ObjectiveUpdated`; these are the best observation hooks for a mod that reacts to quest progress (no parameters, so re-read `allTasks`).

Querying: `TaskController.TaskActive(string id)` and the other four task and five objective predicates, `GetTaskStatus(TaskInfo)`, `TaskIsStatus(TaskInfo, CurrentTaskStatus)`, `ObjectiveIsStatus(taskId, objectiveId, ObjectiveStatus)`, `GetTaskAndObjective(...)`.

To find task ids at runtime use the developer console (`list_tracked_tasks`, `list_task_objectives <taskId>`, section 9) or enumerate `TaskController.instance.allTasks`. Unstarted tasks are only reachable through Addressables keys; there is no global registry.

---

## 5. Mail, notes, phone

### 5.1 `MailController` (`MailController.cs`)

State: `receivedMail`, `receivedPackages` (parcel locker), `sentMail` (items the player sent to an NPC), `unprocessedMail : Dictionary<Mail, TimeOfDayAzure.Timer>` (in transit), `delay : TimeAndDay` (default transit time for player-bound mail), `parcelPrefabs`. Saved as global `GUID "MailSystem"`.

Mail objects (`MailController.Mail` subclasses): `ItemMail {ItemReference item; int amount; object[] meta}`, `TaskItemMail {TaskItem taskItem; TaskObjective objective; ObjectiveStatus objectiveStatus}`, `NoteMail`, `RecipeMail`, `MoneyMail {Money.money}`, `TaskMail {task, objective, taskStatus, objectiveStatus, optional, createNewTask, objectiveHasToExist, addToCounter, taskId, objectiveId}`, `FurnitureMail`, `ParcelMail`, and `BundleMail` (`BundleMail.cs`). Every mail has `mailName` and `NPCReference receiver`. `Target` is `Player` (inbox), `World` (sent to an NPC, `receiver` used), `PlayerParcelLocker`.

Sending (all `public`, lines 238-298):

```csharp
SendMail(string name, ItemReference item, int amount, object[] meta, Target target, NPCReference npc = null, TimeAndDay delay = null)
SendMail(string name, TaskItem item, Target target, NPCReference npc = null)
SendMail(string name, NoteInfo note, Target target, NPCReference npc = null)
SendMail(string name, RecipeReference recipe, Target target, NPCReference npc = null)
SendMail(string name, Money.money money, Target target, NPCReference npc = null)
SendMail(string name, Furniture furniture, Target target, NPCReference npc = null)
SendMail(Mail mail, Target target, TimeAndDay _delay)     // the funnel
```

The funnel delivers immediately when `delay == null || _delay == null || delay.TotalMinutesWithEverything == 0` (note the null check is after the property access, so a null `delay` field would throw; it is set in the prefab), otherwise it creates a `TimeOfDayAzure.Timer` in `unprocessedMail`. Parcel-locker deliveries also generate a "parcel card" via `SendParcelCardToPlayer`. Events: `MailController.MailSent(name, receiver, mail)`, `MailReceived(name)`, `PackageSent`, `PackageReceived`.

Opening: `TakeMailitem(Mail mail, bool silent = false)` (line 516) is what the Mailbox calls. It dispatches by mail type: items go to `ItemOperations.AddItemsAndDropRemaining`, task items to `TaskItemsManager.AddTaskItem` and optionally `objective.EditObjective(...)`, notes to `NoteController.AddNote` + `ReadNote`, etc.

Scene logic components: `logic_mail_sender` (`SendMail()` per `MailType`), `logic_mail_bundle_sender`, `logic_mail_listener` (reacts to `MailSent`/`MailReceived`), `Mailbox : Interactable` (`TakeMail`, `InsertMail`, relays `onMailTaken/onMailInserted/onMailReceived`), `PlayerMailSubscription`. `Achievements.Reward.SendReward` also uses `SendMail` for furniture and item rewards.

### 5.2 Notes (`NoteController.cs`, `NoteInfo.cs`)

`NoteInfo : SavableScriptableObject` (`id`, `title`, `content`, `category`, optional `book`). `NoteController.instance.AddNote(NoteInfo, bool showNotification = false)` adds a note, or a page to a book; `ReadNote(NoteInfo)` opens it; `ReadCustomNote(ItemWritableData)` shows a player-written note. `notes : List<CollectedNoteInfo>` is the saved state. `NoteInfo.GiveNote()` is the shortcut. Email is a subtype: `Email : NoteInfo` (`from`, `fromEmail`, `date`, `topImage`) shown by `ComputerEmails`. Console: `list_notes`, `add_note <title>`, `read_note <title>`.

### 5.3 Phone (`Phone.cs`, `PhoneNumber.cs`, `PhoneNumberDatabase.cs`, `PhoneUI.cs`, `PhonecallRelay.cs`)

Phones are wall/desk interactables, not a handheld. `Phone : Interactable` has `network` (`[Flags] PhoneNumber.Network {None=0, Obenseuer=1, Outside=2, Wasteland=4}`), an owner `NPCReference`, and relays. `Interact()` opens the dialing UI (`PhoneUI.instance.SetPhone`) unless it is owned and not ringing. `PhoneNumberDatabase.CallNumber(List<int> number, Network, List<PhoneNumber> additional)` returns the matching `PhoneNumber`, or the shared `notReachable`, `outsideNetwork` or `notAnswering` entries (random roll against `answerChance`). A call is just a conversation: `Phone.StartCall(PhoneNumber)` (see 3.3). Incoming calls: `Phone.ReceiveCall(PhoneNumber, int hangupTime, Action onAnswered = null, Action onNotAnswered = null)` rings and sets a `TimeOfDayAzure.Timer` that expires the call; `PhonecallRelay.MakeCall()` is the scene-logic wrapper. The phone plan is a subscription (`PlayerPhonePlanSubscription`, Lua `GetPriceForOSPhone`).

---

## 6. Money and the bank

### 6.1 Wallet: `Money` (`Money.cs`)

Two currencies: `Money.Currency { OC, RM }`. OC is an integer; RM is "real money", stored internally as an integer count of tenths.

| Member | Notes |
|---|---|
| `int OCMoneyAmount` (public field, saved) | |
| `float RMoneyAmount` (property, line 197) | getter `Round(rMoneyAmount / 10, 1)`, setter stores `RoundToInt(value * 10)`. The backing `int rMoneyAmount` is private |
| `float OCtoRM = 0.1f` (`[fsIgnore]`, public), `RMtoOC => 1 / OCtoRM` | global conversion used by `ToTotalInRM`, `OcToRmConvert`, `RmToOcConvert`, shop prices. Not saved |
| `struct money { float RM; int OC; }` | with `+ - * /` operators (rounding: OC `RoundToInt`, RM 1 decimal), `GetPricestring()`, `IsMoreThanZero()`, `ToTotalInRM()`, `ToTotalInOC()` |
| `bool CheckMoney(float amount, Currency c, bool showNoMoney = false)` (313) | also overloads `(float, bool RealMoney, bool)` and `(money)` |
| `bool Buy(int amount, Currency c, bool showNoMoney = false)` (369); `bool Buy(money m)` (396) | check then remove; returns false without touching anything if short. Overload `Buy(int, bool RealMoney, bool)` exists |
| `void AddMoney(float amount, Currency c)` (447) | the real implementation; `(float, bool RM)` and `(money, bool notify)` forward to it |
| `void RemoveMoney(float amount, Currency c)` (498) | clamps at zero, never fails; callers are expected to check first |
| `AddMoneyDialogue(double, bool rm)`, `RemoveMoneyDialogue`, `CheckMoneyDialogue` | Lua entry points, show a "Money added!" notification |
| `AddRandomMoney(min, max, RMChance, ratio)`, `OCToOtherCurrency`, `OcToRmConvert(float)`, `RmToOcConvert(float)` | helpers |
| `List<BankAccountData> bankAccounts`, `FindBankAccount(name)`, `FindOrCreateBankAccount(name)` | bank accounts live in the wallet object, keyed by bank name |

Side effects of `AddMoney`/`RemoveMoney`: mirrors the new total into Lua (`Player.Money` for OC, `Player.RealMoney` for RM), updates every `MoneyPanel`, calls `InteractObjects.instance.UpdateInteractableMoney()`, and plays the money sound. A mod that sets `OCMoneyAmount` directly bypasses the Lua mirror, UI and sound (call `Money.instance.RefreshAmount()` for the UI, and set the actor field yourself). Preferred mod API: `AddMoney`/`RemoveMoney`/`Buy`/`CheckMoney`.

Details that bite: `AddMoney`/`RemoveMoney` cast OC amounts with `(int)`, truncating fractions. `Money.Account.CanAfford(money f)` has inverted logic (`f >= funds`) and appears unused. `CurrentTime` (line 632) compounds bank interest once per day change using `Pow(1 + rate, days / 7)` with integer division, so intervals shorter than 7 days apply no interest, and issues monthly debt-downpayment invoices (`InvoiceController.CreateInvoice`).

### 6.2 Bank (`Bank.cs`, `BankPanel.cs`, `BankUI.cs`)

One `Bank` component per bank location (`BankName`, `GUID`), each with its own rates and cash pool. Player accounts are `Money.BankAccountData` stored in `Money.instance.bankAccounts` (`Bankname, OC, RM, Frozen, Interest, DebtInterest, CurrentDebt, MonthlyDownpayment`). `BankPanel.instance.activeBank` is whichever bank the UI is currently using; `Bank.EnterBank()` (line 626) opens it and is the Lua `EnterBank`.

Key members:

- Account: `PlayerHasAnAccount()`, `CreateAccount()`, `CreateAccountWithMoney(int OC, int RM)`, `CheckBalance() : Money.money`, `AccountFrozen()`, `SetAccountFrozen(bool)`, `GetPlayerAccount()`.
- `bool DepositWithdraw(int ocAmount, float rmAmount)` (480): positive deposits, negative withdraws; skimmed by `Corruption` (random 0..Corruption fraction of deposits is lost to the bank); clamped to the bank's `TotalOCSupply/TotalRMSupply` on withdrawal and fires `onOutOfFundsOC/RM`.
- Exchange: `float GetExchangeValues(float amount, bool toRM = false, bool round = true, bool ignorePlayerMoney = false)` (381) quotes; `bool Exchange(int amount, bool toRM = false)` (421) executes via `Money.RemoveMoney` + `Money.AddMoney`. Rates are per-bank fields `OS_RM_Ratio` (default 0.06, OC to RM) and `RM_OS_Ratio` (default 9.8, RM to OC), not the wallet's 0.1 constant; both are scaled down when the bank's currency supply is below its `DailyAverage...Spending`. The result is truncated with `(int)` on the received side, so RM received by exchange is whole units only.
- Loans: `CalculateMaximumLoan()`, `TakeLoan(Money.money)`, `TakeRMLoan(int)`, `TakeOCLoan(int)`, `PayLoan(int)`, with `Max_RM_Loan`, `Max_OC_Loan`, `PaymentTimemonths`, `DebtInterest`, `LoanCurrency`, `TenementFinancingRatio`.
- Daily: `CurrentTime` randomises each bank's supplies around `DailyAverageOc/RmIncome/Spending`.

The ATM/teller UI is `BankUI` + `BankPanel` + `BankMenuNavigationHandler`/`BankPanelState` (controller navigation). The money slider (`MoneySliderPanel`) is reused for loan and deposit amounts.

Branches in 0.5.13 (read from the scene files; the field layout was checked against the live Bazaar branch): Stalburg Bank (Open Sewer Bazaar, Open Sewer Tenement, Interior Bazaar Painting Shop; `WithdrawOnly`, Real Money), "Money exchange" (Interior Tenement Kolhola), "Penni-Pena" (Interior Bazaar Mentti B) and "Casino" (Interior Bazaar Casino, better rates 0.08 / 10.3) are exchange desks, and three developer test branches sit in Interior Player Tenement and Interior Bazaar Clock Tower. No branch reached in normal play takes deposits. Quirks: `DepositWithdraw` reads `BankPanel.instance.activeBank.mode`, so it throws without an open bank window; `GetExchangeValues` checks the Real Money supply in the Real Money to coin direction where `Exchange` checks the coin supply; and in that direction `TotalOCSupply / DailyAverageOcSpending` is an integer division, so a bank whose coin supply is below its daily spending pays 0 coins for Real Money (and `GetExchangeValues` divides by zero when the spending is 0). `CalculateMaximumLoan` opens an account when `TakeLoanDirectly` is set, and needs `TenementController.instance` when `TenementFinancingRatio > 0`. The framework's `Banks`/`BankAccounts` work around all of this (see `docs/modding/framework/economy.md`).

### 6.3 Invoices and rent

`InvoiceController.instance.CreateInvoice(string debtor, Money.money money, int days = 7, TimeAndDay delay = null, bool isfake = false, Action<ItemInvoiceData> onPaid = null, Action<ItemInvoiceData> onExpired = null)` (`InvoiceController.cs:47`) creates a paper invoice item; `PayInvoice(int ID)`, `CanPayInvoice(int)`, `CancelInvoice(int)`, `SendReminder`. Paying fake invoices unlocks the `PAY_FAKE_INVOICE` achievement. Rent and debt downpayments use this. Rent amounts and tenant preferences come from `CharactersRent.json` plus `TenementController`; difficulty scaling is `DifficultyController.instance.currentDifficultyInfo` (`GetRentPayment`, `GetNumberOfPayments`, `GetTraderWealth(float)`), `DifficultyController.InfiniteTraderWealth` (static).

---

## 7. Shops, vending machines, slot machines

There are five separate purchase paths. They do not share a base class beyond `Interactable`.

| Path | Class | What is sold | Pricing |
|---|---|---|---|
| Item traders (the main NPC shop) | `Trade` (`Trade.cs`), `TradePanel`, `TradeSpecial` | inventory `Item`s through a `Storage` | `baseSellPrice` 0.9 (player sells), `basePurchasePrice` 1.2 (player buys), `Item.GetItemSalePrice(meta)` |
| Supermarket checkout | `MarketShop` (`MarketShop.cs`) | player puts goods on a conveyor, cashier scans, player pays | `Item.GetItemSalePriceUnrounded * priceFactor (1.1)` converted to the shop's `currency` |
| Furniture and train shops | `BaseShop` -> `FurnitureShop`, `TrainShop`; UI `ShopBaseUI` -> `FurnitureShopUI`, `TrainShopUI`; `ShopController` | `Furniture`/train cars | `buyPercent` 110, `sellPercent` 90 of `furniture.PriceOC/PriceRM` |
| Vending machines | `VendingMachine` | fixed stocked list | `item.GetItemSalePriceUnrounded * basePurchasePrice` or `absolutePrice` |
| Player-run shop | `PlayerShopManager` | tenement shop the player runs | work in progress (see below) |

Base item prices live in `Items.json` / the `Item` asset `Value` field, in OC. RM prices are derived (`OC * 0.1`).

### 7.1 `Trade`

`StartTrade()` (line 322) is the Lua function a trader exposes to its dialogue (public `RegisterFunction()`/`UnRegisterFunction()` at lines 312/317 do the registering; nothing in C# calls them, so scene relays do, as with `Bank`); it opens `TradePanel` and the trader's `Storage`. Moves between slots call `MakeTrade(ItemData, Slot, int)` (406), which routes to `Buy(ItemData, int)` (434, virtual: player buys) or `Sell(ItemData, int)` (549, virtual: player sells). Naming is from the player's side. Key points:

- Price: `static float GetItemPrice(Item item, int owner, object[] meta, Money.Currency currency, float basePrice = 1f, float stolenPriceFactor = 1f, bool ignoreContainers = false)` (594). Stolen goods (`owner > 0`) multiply by `stolenPriceFactor` (0.75); minimum 1; RM is `Money.OcToRmConvert`.
- Currency per direction: `sellPriceMode` / `purchasePriceMode` (`PriceMode.OnlyOC | OnlyRM`).
- Trader funds: `CurrentMoney` (property, backed by `currentMoney` or a linked furniture's `currentOcMoney/currentRmMoney`) is a finite float that refills in `MoneyRestock()` (`traderMoneyAmountMin/Max` through `DifficultyController.GetTraderWealth`). `CanSell` checks it unless `InfiniteTraderWealth`.
- Filters: `allowedCategories`, `forbiddenCategories`, `buysStolen`, `destroyPurchasedItems`, opening hours (`openingTimes`, `autoOpenClose`).
- Mod hook points: `Trade.GetItemPrice` (static, one choke point for every trade price), `Buy`, `Sell`, `CanBuy`, `CanSell`.

### 7.2 `MarketShop`

State machine `State {Standby, Scanning, Pay}` with `ConveyorItem`s. `GetItemSalePrice(Item, object[] meta = null, bool round = true)` (988) is the per-item price; `GetCurrentPrice()` (975) is the running total (`price` field summed while scanning). `Pay()` (588): `Money.instance.Buy(max(1, total), currency, showNoMoney: true)`; on success it removes ownership flags from the goods (`owner = new NPCReference(0)`) so the player can carry them out. `ReturnItems()` (809) undoes it. Writes Lua `Price` and `Currency` for the cashier dialogue (line 568). The shop is owned by an `NPCReference owner`; `retainOwner` keeps ownership on items worth more than `retainIfSpawnsItemValueGreaterThan`.

### 7.3 `BaseShop` / `ShopBaseUI` (furniture and trains)

`BaseShop : Interactable` (`BaseShop.cs`): shop cash `currentOcMoney/currentRmMoney` (refilled in `MoneyRestock()` between `shopMoney*Min/Max`, scaled by difficulty), restock timers on `TimeOfDayAzure.MinutePassed` (`minRestockingTime`/`maxRestockingTime` in game seconds, or `restockEveryDay`), and virtual `GetAvailableItems()`, `UpdateShopItems()`, `AddItem(...)`, `RemoveItem(...)`, `CanSell(...)`. Price helpers: `GetOcBuyPrice/GetRmBuyPrice(FurnitureInfo)` (player pays `Price * buyPercent / 100`) and `GetOcSellPrice/GetRmSellPrice` (player receives `Price * sellPercent / 100`). `Interact()` -> `ShopController.instance.OpenMenu(this)`.

`ShopBaseUI` (`ShopBaseUI.cs`, shared with the crafting UI) does the money movement: `Buy()` (line 681) does `Money.RemoveMoney(GetOcBuyPrice, OC)` + `RemoveMoney(GetRmBuyPrice, RM)`, `BaseShop.AddBuyMoney`, `RemoveItem`, `AddItem()`; `Sell()` (652) does the reverse with sell prices and `BaseShop.RemoveSellMoney`. `PlayerHasMoney()` (1000) is the affordability check. The multi-currency price is deliberate: a furniture can cost OC and RM at once.

### 7.4 `VendingMachine`

`VendingMachineItem[] sellableItems` (`item`, `stock`, `currentStock`, `basePurchasePrice`, `absolutePrice`, `currency`, `owned`, `stolen`). `Buy(VendingMachineItem)` (line 268): builds a `Money.money` in the item's currency, may `Crime.instance.CommitCrime` for stolen stock when witnessed, then `Money.instance.Buy(money)`, `CreateItems`, `currentStock--`, fires `onDispense`. Restock on `MinutePassed` using `SpawnSettings`; `RefillStock()` is public. Output goes into the machine's `Storage` (8 slots), not directly into the inventory.

### 7.5 Player shop

`PlayerShopManager` (`PlayerShopManager.cs`): the data model for a tenement shop the player can open (`PlayerShopData`: `isOpen`, `residentsOnly`, `Currency`, `GeneralPriceFactor`, `SetPrices`, `itemsOnSale`, `itemsSold`, `uncollectedEarnings`, `NPCCanShop(NPCReference)`). `GetItemPrice` and `CollectEarnings` work; `MakeSale` and `FindItemAndMakeSale` are empty, and `PlayerShopController.BrowseAndMakePurchase` returns false. Treat it as unfinished: in 0.5.13 no scene and no loaded furniture prefab has a `PlayerShopManager` or an `InteractableCashRegister`, so nothing ever creates one. The framework's `PlayerShops` is its own model instead.

### 7.6 Slot machines (high level)

`SlotMachineGameplay : Interactable` (`SlotMachineGameplay.cs`) is a self-contained mini game: the player feeds coins in (`AddMoneyButton(int)`, which deposits into `_moneyInMachine`, funded from the player's OC wallet by the `InteractableMoney` coin slot), picks a stake (`RaiseStakes`, `stakes[]`), a winning line (`WinningLine*`), and spins; reels are weighted by `SlotMachineHelper.PickWeightedRandomValue`, with lock buttons, a heads/tails doubling gamble (`DoubleButton`), and `CashOutButton()` which pays out with `Money.instance.AddMoney(_moneyInMachine, RM: false)` (line 899). OC only. The RNG is `UnityEngine.Random`. Win/loss feeds `PlayerStatsGambling` (`OCWon`, `OCLost`, addiction and difficulty factors). Other gambling: the pajatso (`PajatsoGameplay`, coins through gates drop from a gutter onto a win tray), scratch cards (`Scratchcard.Use`, prize on a receipt cashed in through dialogue), `RatFightArena` (Lua `StartBetting`/`CollectWinnings`), card games (`CardGameTable` with `ChinaHand`/`Ristiseiska`), the drinking and sauna contests (`ContestController`), no shared payout API. `CardGameTable.OnGameFinished` is `async void`: its state machine (`<OnGameFinished>d__66` in 0.5.13) computes and pays the payout in its first, synchronous run, so a prefix/postfix on the method brackets the payment. Locations (scene files): 10 slot machines, the pajatso and a card table in Interior Bazaar Casino, single slot machines in O Market, Speakeasy, Open Sewer Metro, Mines Barrack Hotel, Mentti B, J Kiosk, the Bar, Open Sewer Mines and Mallment, card tables also in Interior Bazaar Books and Test Map Card Games, the drinking contest in Interior Bazaar Bar, the sauna contest in Interior Bazaar Sauna, the rat fights in Interior Bazaar Ratfighting Arena. The only `GasStation` (two `FuelPump`s, for minitrains) is in Open Sewer Mines. A mod that wants to change odds should patch `SlotMachineHelper.PickWeightedRandomValue`; one that wants to log payouts can postfix `Money.AddMoney(float, Currency)` and look at the stack, or patch `CashOutButton`.

---

## 8. Achievements

`Achievements` (`Achievements.cs`) is a global `SavableScript` (`GUID "Achievements"`) wrapping Steamworks.NET. Each `Achievement` has `API_ID`, `STAT_API_ID`, `progress`, `maxprogress`, `enabled`, `rewards`, `_triggered`, `_committedToAPI`.

Unlock path: game code calls `Achievements.instance.TriggerAchievement(string name)`, or `(string, float|int progress, bool set = false)` to add or set progress (lines 432, 448, 471). These return immediately unless `initialized && !globalDisable`. Progress goes to Steam as a stat (`SteamUserStats.SetStat` + `StoreStats`); when `progress >= maxprogress && enabled` the `triggered` setter calls `SteamUserStats.SetAchievement(API_ID)` and `StoreStats()`, then `SendRewards()` mails any furniture/item reward (`Reward.Type.Money` and `Task` are no-ops). All Steam calls are wrapped in try/catch, so a missing Steam client (for example under Proton without Steam running) just logs and the local `_triggered` flag still flips only if the setter did not throw (on throw it resets `_triggered = false`).

How to avoid or respect them in mods:

- `Achievements.instance.globalDisable = true` is the supported off switch (public field). The in-game console sets it true when opened and **false again when closed** (`DeveloperConsole.cs:401/422`), and `enable_achievements` also clears it. So use of the console does not actually protect a save; a mod that wants to guarantee no unlocks while it is active must re-assert `globalDisable = true` itself (for example from a per-frame check or a postfix on `Achievements.TriggerAchievement`).
- Per achievement: `EnableDisableAchievementTrigger(string name, bool enabled)` (416).
- Alternatively Harmony-prefix all three `TriggerAchievement` overloads and return false.
- Ids seen in code: `ARRESTED`, `BOUNTY_LOW/MED/HIGH`, `WIN_DRINK_COMPETITION`, `BURN_RM`, `ENTER_OTHER_SIDE`, `RADIATION_DEATH`, `RAT_RACE`, `PAY_FAKE_INVOICE`, `BETTER_CALL_SAMUEL`, `MIN_EXPECTATIONS`, `MAX_EXPECTATIONS`, `SURVIVE_7/30/365`, `CRAFT_*` (`PlayerStats.cs:442`). Item and dialogue driven ones go through `ItemAchievementList` and `DialogueEventSystem.TriggerAchievement`.
- Money-granting or progression-skipping mods will not trip anything unless they call the above paths; `SURVIVE_*` is driven by the day counter, so time-skip mods can unlock them.
- `PauseMenu.cs:447` calls `ResetValues()` when starting a new game.

---

## 9. Developer console

There is a built-in console (`DeveloperConsole.cs`), toggled by F1 (key up) or controller View+Menu. It is a useful reference implementation and a quick way to inspect state while developing a mod.

- Dispatch is the private `HandleInput(string input)` (line ~462): splits on spaces (quotes group), `switch (array[0])`. Output via `DeveloperConsole.Instance.Print(string)` (public). To add commands, Harmony-prefix `HandleInput`, handle your verbs, and return false for them.
- Relevant commands: `add_money <OC|RM> <amount>`, `remove_money`, `list_tracked_tasks`, `list_task_objectives <taskId>`, `set_task_status <taskId> Active|Failed|Finished|Abandoned|AnyStatus`, `set_objective_status <taskId> <objectiveId> ...`, `set_objective`, `add_task_item`, `add_note`, `read_note`, `add_item <id> <amount>`, `find_item`, `change_scene`, `list_scenes`, `change_time`, `enable_achievements`, `add_crime`/`reset_crime`, `difficulty`, `player_stats ...`. `help` prints the full list.
- `DeveloperConsoleRoutines.SetTaskStatus` shows the minimal correct way to flip a task: look it up in `TaskController.instance.allTasks` by `Id`, then `EditTaskStatus(taskInfo, status)`.

---

## 10. Harmony hook table

Signatures are as decompiled. All are on the type named; patch by name, since these are the real method names (including the game's typos).

| Goal | Target | Notes |
|---|---|---|
| Observe/replace NPC database load | `NPC.NPCDirector.LoadDatabase()` | postfix to add/alter `globalDatabase` entries (needs a `Character` asset with a matching ID, else `item.character == null` makes the director skip it) |
| After saved NPC state applied | `NPC.NPCDirector.OnLoadingGameSpecial()` -> `bool` | postfix |
| NPC simulation tick | `NPC.NPCDirector.DeltaSeconds(int)` (private) | runs once per game minute per all NPCs |
| NPC scene spawn pass | `NPCManager.StartDelay()` (private iterator) | patch via `NPCData.Spawn()` or `Character.Spawn(Vector3, Quaternion)` instead |
| Intercept spawn | `Character.Spawn(Vector3 position, Quaternion rotation)` | prefix to block/replace; postfix has no return value, read `NPCManager.instance.FindNpcById(ID)` |
| NPC registers/unregisters | `NPCManager.AddNpcToList(NPCInfo)`, `RemoveNpcFromList(NPCInfo)` | best "NPC appeared/gone" events |
| NPC per-frame | `NPCController.Update()` (private) | hot path, keep patches cheap |
| Activity start | `NPCController.StartActivity(NPC.Activity activity, bool interrupt = false)` -> `bool` | prefix returning false to veto; also `StartNPCActivity(Activity)`, `ForceStartActivity(Activity)` |
| Activity logic | `NPC.Activity.OnActivity()` virtual, or `Act_*.OnActivity()` | patch the subclass, the base is empty |
| Schedule choice | `NPC.Schedule.CurrentTime(TimeAndDay)`, `TryStartActivity(ScheduledAction, TimeAndDay)` -> `bool` | |
| Talk begins | `InteractableTalk.OnDialogueStart(string dialogue = "", bool tryStartConversation = true)` | prefix can rewrite `dialogue` to redirect the conversation |
| Talk gate | `InteractableTalk.CheckStartDialogueInterrupt(string dialogue = "", bool force = false, bool ignoreMenuState = true)` -> `bool` | |
| Talk ends | `InteractableTalk.OnDialogueEnded()` | |
| UI open/close | `DialogueController.OnDialogueOpen()`, `OnDialogueClosed()` | |
| Any conversation | `PixelCrushers.DialogueSystem.DialogueManager.StartConversation(string, Transform, Transform, int)` and shorter overloads (`firstpass/.../DialogueManager.cs:586-626`) | patch the 4-arg or 5-arg one; shorter overloads forward |
| Lua function table | `PixelCrushers.DialogueSystem.Lua.RegisterFunction(string, object, MethodInfo)` | prefix to rename/wrap/observe every registration |
| Task state change | `TaskController.EditTask(TaskInfo, TaskObjective, ObjectiveStatus, bool optional, bool createNewTask, CurrentTaskStatus, bool objectiveHasToExist = false, bool taskHasToBeActive = true, bool renovation = false)` -> `bool` | postfix on `true` is "something changed" |
| Task status | `TaskController.EditTaskStatus(TaskStatusInfo, CurrentTaskStatus)` | |
| Counter | `TaskController.EditTaskCounter(TaskInfo, TaskObjective, int amount, bool setamount = false)` | |
| Task events (no patch) | `TaskController.TaskUpdated`, `TaskController.ObjectiveUpdated` | static events |
| Mail funnel | `MailController.SendMail(MailController.Mail mail, MailController.Target target, TimeAndDay _delay)` | every typed overload ends here; postfix to log, prefix to redirect |
| Mail taken | `MailController.TakeMailitem(MailController.Mail mail, bool silent = false)` | |
| Money add | `Money.AddMoney(float amount, Money.Currency currency)` | the funnel; `AddMoney(money, bool)` and `(float, bool)` forward here |
| Money remove | `Money.RemoveMoney(float amount, Money.Currency type)` | also funnel |
| Money check | `Money.CheckMoney(float, Money.Currency, bool)`, `CheckMoney(Money.money)`, `Buy(int, Money.Currency, bool)`, `Buy(Money.money)` | patching these changes affordability but not the wallet math |
| Trade price | `Trade.GetItemPrice(Item, int, object[], Money.Currency, float basePrice, float stolenPriceFactor, bool ignoreContainers)` (static) | single choke point |
| Trade execute | `Trade.Buy(ItemData, int)`, `Trade.Sell(ItemData, int)`, `CanBuy`, `CanSell` | |
| Checkout | `MarketShop.GetItemSalePrice(Item, object[], bool)`, `Pay()` | |
| Furniture shop | `BaseShop.GetOcBuyPrice/GetRmBuyPrice/GetOcSellPrice/GetRmSellPrice(BuildingSystem.FurnitureInfo)`, `ShopBaseUI.Buy()`/`Sell()` (private) | |
| Vending | `VendingMachine.Buy(VendingMachine.VendingMachineItem)`, `VendingMachineItem.GetPrice()` | |
| Bank exchange | `Bank.GetExchangeValues(float, bool toRM, bool round, bool ignorePlayerMoney)`, `Bank.Exchange(int, bool toRM)` | |
| Bank deposit | `Bank.DepositWithdraw(int, float)` | |
| Achievements | `Achievements.TriggerAchievement(string)`, `(string, float, bool)`, `(string, int, bool)` | prefix + return false to suppress |
| Console | `DeveloperConsole.HandleInput(string)` (private) | add commands |

For anything that needs `[HarmonyPatch]` on a private method use `AccessTools.Method(typeof(T), "Name")`.

---

## 11. Gotchas

1. **Lua number types.** Lua numbers are `double`. Registered methods taking `int`/`float` will not bind; use `double` and cast, like `NPCManager.ChangeInteractableTalk(double characterId)`.
2. **Register-once semantics.** `Lua.RegisterFunction` ignores a name that already exists. The game's own scene objects register in `Awake`/`Start` and unregister in `OnDestroy`, so the set of live functions changes by scene and by which NPC you are talking to. Check with `Lua.environment` before assuming a function exists, and do not rely on registering over a game name.
3. **Raw conversations skip NPC setup.** `DialogueManager.StartConversation` locks the player (via `GameController.Update` -> `CheckDialogueStatus`) but the NPC keeps walking and its relay/condition Lua functions are not registered. Go through `InteractableTalk` for NPC dialogue. And watch the stale `DialogueController.onDialogueClosed` (3.3).
4. **`Globals.dialogue` is applied last on load.** Writes to Lua variables during `OnLoadingGame` will be clobbered. Do mod Lua setup after `SaveController.LoadingDone`.
5. **Money has side effects.** Only the `Money` methods keep Lua `Player.Money/RealMoney`, UI and interactable money in sync. RM is tenths-of-a-unit internally, `RemoveMoney` clamps at zero silently, and `OCtoRM` (0.1) is public and unsaved: changing it rescales every OC/RM conversion game-wide, but not the bank's own `OS_RM_Ratio`/`RM_OS_Ratio`.
6. **Three different exchange rates.** Wallet conversion (0.1), bank exchange (0.06 / 9.8 by default, supply-scaled, truncated), and shop RM prices (`OC * 0.1`). A "fair" currency exchange feature has to pick one on purpose.
7. **Naming from the player's side.** `Trade.Buy` is the player buying; `BaseShop.GetOcBuyPrice` is what the player pays; `GetOcSellPrice` is what the player receives. `Money.Buy` is the player paying.
8. **Time-driven things use game seconds.** `TimeOfDayAzure.Timer`, restock times, `NPCData` transition progress all use in-game seconds; `NPCSceneUtilities.UpdateNPCTransition` converts via `TimeOfDayAzure.SecondsToRealTimeSeconds` / `actualSpeed`. Changing time scale (`set_ingame_time_scale`) affects NPC off-screen travel and shop restock cadence.
9. **Off-screen NPCs are bookkeeping.** An NPC outside the loaded scene is an `NPCData` with a scene name and a float progress. To "teleport" one, set `state.currentScene`/`currentEntrypoint` and clear the path (`ResetTransition()`); to force it to appear now use `NPCData.Spawn()` while the target scene is loaded.
10. **NPC state is saved, schedules are not.** Mutating `Schedule.Timetable` at runtime does not persist; re-apply it each load (or edit `NPCBehavior.json`, which every launch reads unless a save has set `dontLoadDatabase`, in which case only `state` is taken from the save and the schedule still comes from JSON).
11. **`Character.FullName.Contains("Crazy")` debug branches** are all over the NPC code (logging only). Naming a mod NPC with "Crazy" in it just makes it noisy.
12. **Task assets are Addressable.** Saves reference tasks by `addressableAssetPath`. Reuse shipped `TaskInfo`/`TaskObjective` assets where you can; runtime-created ones have no path and are untested across save/load. All task APIs fail quietly (return false/no-op) when requirements are unmet, so log in your postfix to debug.
13. **Dead and stub code.** `Market.cs` Lua functions throw `NotImplementedException`; `PlayerShopManager.MakeSale` is empty; `Money.Account.CanAfford` is inverted; `Bank.GetExchangeValues` OC branch scales by an integer division of supplies. Do not build on these.
14. **Typos you must match.** `BehaviorDesigner.Runtime.Tasks.DialugeSystem` namespace, `ForseStartDialogue`, `Generic_Prison_guard.HasBagItems ` (trailing space), `Activity.OnInterupt`, and lowercase `Act_sit`. Harmony patches and Lua strings need the exact spelling.
15. **Steam under Proton.** Achievements and `SteamUserStats` calls are try/caught; they will not crash if Steam is absent, they just do nothing.
