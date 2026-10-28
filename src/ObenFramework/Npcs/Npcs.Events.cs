using ObenFramework.World;

namespace ObenFramework.Npcs;

public static partial class Npcs
{
    /// <summary>
    /// An NPC object appeared in the scene you are in, at a level load, when the game spawns one that
    /// arrives from another scene, or when a story NPC is created. Subscribe with an owner so a hot reload
    /// removes the handler; each handler runs in its own try/catch.
    /// </summary>
    public static readonly WorldEvent<NpcRecord> Spawned = new WorldEvent<NpcRecord>(nameof(Spawned));

    /// <summary>
    /// An NPC object left the scene you are in: it walked out through a door, was removed, or the level is
    /// being torn down (then every NPC reports in turn). The record is read just before the object goes.
    /// </summary>
    public static readonly WorldEvent<NpcRecord> Despawned = new WorldEvent<NpcRecord>(nameof(Despawned));

    /// <summary>
    /// An NPC in the scene you are in died: the player killed it, a story event did, or a mod called
    /// <see cref="NpcActions.Kill"/>. Only the change from alive to dead counts, so an NPC that was already
    /// dead in the save does not report again when the level loads. The record is read after the change.
    /// </summary>
    public static readonly WorldEvent<NpcRecord> Died = new WorldEvent<NpcRecord>(nameof(Died));

    /// <summary>
    /// An NPC's current activity changed (it started standing, patrolling, sleeping, a police chase...), in
    /// this scene or in the game's off-scene simulation. Fires often while the world runs, so keep handlers
    /// short; the hook does nothing while nobody is subscribed.
    /// </summary>
    public static readonly WorldEvent<NpcActivityChange> ActivityChanged = new WorldEvent<NpcActivityChange>(nameof(ActivityChanged));
}
