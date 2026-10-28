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
}
