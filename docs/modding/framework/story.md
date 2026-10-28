# Story timing and the law

Namespace `ObenFramework.Story`. This page covers when story changes are safe (`StoryPump`) and the player's crime record (`Law`). Quests are in [quests.md](quests.md) and [quest-authoring.md](quest-authoring.md), dialogue in [dialogue.md](dialogue.md) and [dialogue-authoring.md](dialogue-authoring.md), sessions in [new-game.md](new-game.md).

## StoryPump

### Ready: once per session

```csharp
StoryPump.WhenReady(this, kind =>
{
    // make this mod's quests, default variables, branches that depend on the save
});
```

`StoryPump.Ready` is raised once per play session (a save load, a new game's first level, a level loaded straight from the menu), a few frames after `Sessions.Started`, as soon as the task panel and notifications exist, no save is in progress and the Lua state can be written. A level change (doors, fast travel) continues the session and does not raise it again; loading the same save again does. The payload is the `SessionKind`. `WhenReady` also runs the action at once when the running session is already ready (a mod loaded or hot reloaded mid-game). `IsReady` says whether it was raised for the running session.

Behind it is a retry gate: it tries once per frame for up to 600 frames and logs one line if the quest system never appears.

### Deferring work out of the game's calls

```csharp
[HarmonyPostfix, HarmonyPatch(typeof(SomeGameClass), "SomeMethod")]
private static void After() => StoryPump.Defer(MyMod.Instance, () => QuestLog.SetTaskStatus("MyMod.intro", CurrentTaskStatus.Finished));
```

`Defer` runs the action at the end of the frame (LateUpdate), outside the game's own call, and holds it back while a save is written or loaded. Actions run in the order they were queued; one that queues more runs those on the next frame. The queue survives a level change and is dropped at the main menu or when another save loads (it belonged to the world that went away). An action is dropped when its owner unloads, and exceptions are logged. `Pending` and `PendingNames()` show what waits. The framework's own law events and quest follow-ups go through the same queue.

## Law

```csharp
float onRecord = Law.TotalOnRecord;
int fine = Law.Fine;

Law.Intercept(this, crime =>
{
    if (crime.Kind == CrimeKind.Trespassing)
        crime.Severity /= 2;
});

Law.Committed.Subscribe(this, c => Log.Info($"{c.Kind} +{c.Severity} against {c.VictimId}"));
Law.Commit(CrimeKind.Vandalism, 25);
```

The record is the game's own (`Crime`, saved with the game). Everything needs a gameplay scene: `Law.IsAvailable`.

| Member | What it is |
|---|---|
| `Total` | the record plus a tenth of the value of stolen goods carried (what arrest chances use) |
| `TotalOnRecord` | the record alone (what fines and sentences use) |
| `Fine` | what paying at the police desk costs now, in real money (record times 1.5) |
| `SentenceHours`, `TimesInJail`, `IsArrestInProgress`, `InPrison` | the prison side |
| `HasStolenGoods`, `StolenGoodsValue` | stolen items the player carries |
| `Generic(kind)`, `Against(npcId)`, `Records()` | the record per kind, per victim, line by line |
| `Commit(kind, severity, victimNpcId, silent)` | a crime the way the game commits one (notification, interceptors, `Committed`) |
| `Clear()`, `Clear(npcId)`, `Clear(kind)`, `Clear(npcId, kind)` | clears lines of the record |
| `PayFine()` | pays from real money like the desk (stolen goods are taken), raises `Fined` |
| `Arrest(factor)` | the police arrest: the record is multiplied by `factor` (up to 100), the player passes out and wakes in prison, raises `Arrested` |
| `Intercept(owner, handler)` | sees each crime before it goes on the record: change `Kind`, `Severity`, `Silent`, or `Cancel` it |
| `Committed`, `Fined`, `Arrested`, `Released` | events, raised through the story queue at the end of the frame |

Edits are refused during a save, while an arrest or level change runs and while the loading screen is up. `Arrest` is also refused during a conversation and in prison. `CrimeKind.None` is the game's "every kind" for clearing and is never a crime.

Related: a witness or camera seeing the player is `Npcs.Surveillance.PlayerSpotted`, police chases are `Npcs.Pursuit` ([npcs.md](npcs.md)).

## Dev bridge

```sh
oben call story.queue
oben call law.crimes
oben call law.commit kind=Vandalism severity=25
oben call law.clear kind=Vandalism
oben call law.fine
oben call law.fine pay=true
oben call law.arrest factor=1
```
