using System;
using ObenFramework.Game;
using ObenFramework.Saves;
using ObenFramework.World;

namespace ObenFramework.Npcs;

public static partial class ModNpcs
{
    /// <summary>
    /// A mod NPC left the scene: despawned, its mod unloaded, the level changed, or the game destroyed it
    /// (that last case is also logged). Raised just before its handle stops working, so <c>FullName</c> and
    /// <c>Position</c> can still be read. A saved NPC that left this way comes back when its scene next loads.
    /// Each handler runs in its own try/catch.
    /// </summary>
    public static readonly WorldEvent<ModNpc> Despawned = new WorldEvent<ModNpc>(nameof(Despawned));

    /// <summary>Called every frame by the framework: notices NPCs whose objects the game destroyed.</summary>
    internal static void Tick()
    {
        for (int i = Live.Count - 1; i >= 0; i--)
        {
            if (i < Live.Count && !Live[i].Loaded && !Live[i].Tearing)
                Lost(Live[i]);
        }
    }

    // Removes the mod's NPCs from the scene without forgetting saved ones (an unload, not a despawn).
    private static void RemoveAll(string owner)
    {
        foreach (var npc in List(owner))
        {
            if (npc.Saved)
                ModNpcStore.Keep(npc);
            Release(npc);
        }
    }

    private static void Release(ModNpc npc)
    {
        if (!Live.Remove(npc))
            return;
        try
        {
            if (Despawned.HasSubscribers)
                Despawned.Raise(npc);
            NpcOrders.End(npc, false);
            NpcRoutines.Forget(npc);
            ModNpcBuilder.Tear(npc);
        }
        catch (Exception e)
        {
            NpcsLog.Failed("Removing a mod NPC", e);
        }
    }

    // Something in the game destroyed the NPC's object (story wiring on the template's body, a script). Its
    // character is taken out; a saved one stays in the save and comes back at the next world ready.
    private static void Lost(ModNpc npc)
    {
        // During a level change or a save load objects go with the scene; that is not news.
        if (WorldState.IsReady && !SaveGate.IsBusy)
        {
            NpcsLog.WarnOnce("lost " + npc.Owner + "/" + npc.Name,
                $"Mod NPC {npc.Owner}/{npc.Name} ({npc.FullName}, body {npc.BodyDescription}) was destroyed by the game, not by its mod" +
                (npc.Saved ? "; it stays in the save and comes back when the scene next loads." : "."));
        }
        Release(npc);
    }

    // NPCs whose objects went with the old level: their characters still need taking out.
    private static void ForgetGone()
    {
        for (int i = Live.Count - 1; i >= 0; i--)
            Release(Live[i]);
    }

    private static void Prune()
    {
        for (int i = Live.Count - 1; i >= 0; i--)
        {
            if (i < Live.Count && !Live[i].Loaded)
                Lost(Live[i]);
        }
    }
}
