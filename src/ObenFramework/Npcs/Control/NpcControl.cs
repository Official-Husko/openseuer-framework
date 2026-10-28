using System.Collections.Generic;
using ObenLoader;
using ObenLoader.Runtime;

namespace ObenFramework.Npcs;

/// <summary>
/// Takes control of NPCs for a while without two mods fighting over them. A mod claims an NPC with
/// <see cref="Claim"/>, gets an <see cref="NpcClaim"/> to move it, make it look at things and pause its
/// schedule, and gives it back with <c>Dispose</c>. Only one mod can hold an NPC at a time; a second
/// mod's claim is refused until the first lets go. Everything a claim changed is undone when it is
/// released, which also happens when the owning mod unloads, the NPC leaves the scene or the level changes.
/// </summary>
public static class NpcControl
{
    private static readonly List<NpcClaim> Claims = new List<NpcClaim>();

    // Owners that already have their release registered, so a mod claiming often does not pile up disposables.
    private static readonly HashSet<string> Tracked = new HashSet<string>();

    /// <summary>
    /// Claims an NPC in the scene you are in. Returns null when the NPC is not loaded, cannot be steered
    /// (it has no controller, or is dead), or another mod already holds it (see <see cref="ClaimedBy"/>).
    /// Claiming an NPC the same mod already holds returns the existing claim. The claim itself changes
    /// nothing; moving, looking and pausing do.
    /// </summary>
    public static NpcClaim Claim(ObenMod owner, int npcId)
    {
        if (owner == null)
            throw new System.ArgumentNullException(nameof(owner));

        string ownerId = owner.Info.Id;
        var existing = Find(npcId);
        if (existing != null)
        {
            if (existing.Owner == ownerId)
                return existing;
            NpcsLog.Info($"{ownerId} could not claim NPC {npcId}: {existing.Owner} holds it.");
            return null;
        }

        var live = Npcs.LiveObject(npcId);
        if (live == null || live.IsDead() || live.npcController == null || live.npcController.Data == null)
            return null;

        var claim = new NpcClaim(ownerId, npcId, live);
        Claims.Add(claim);
        if (Tracked.Add(ownerId))
            owner.Track(Disposable.Create(() => ReleaseOwner(ownerId)));
        return claim;
    }

    /// <summary>The id of the mod that holds this NPC, or null when nobody does.</summary>
    public static string ClaimedBy(int npcId) => Find(npcId)?.Owner;

    /// <summary>Gives back every NPC the mod holds. Returns how many there were.</summary>
    public static int ReleaseAll(ObenMod owner) => ReleaseClaims(owner.Info.Id);

    private static void ReleaseOwner(string ownerId)
    {
        Tracked.Remove(ownerId);
        ReleaseClaims(ownerId);
    }

    private static int ReleaseClaims(string ownerId)
    {
        int released = 0;
        for (int i = Claims.Count - 1; i >= 0; i--)
        {
            if (Claims[i].Owner != ownerId)
                continue;
            Claims[i].Dispose();
            released++;
        }
        return released;
    }

    internal static void Init(ObenMod framework) => framework.Track(Disposable.Create(Clear));

    /// <summary>Called every frame by the framework; does nothing while there are no claims.</summary>
    internal static void Tick()
    {
        for (int i = Claims.Count - 1; i >= 0; i--)
        {
            var claim = Claims[i];
            if (!claim.IsValid)
                claim.Dispose();
            else
                claim.Tick();
        }
    }

    /// <summary>The NPC object is going away; its claim ends without touching the object.</summary>
    internal static void Forget(UnityEngine.Component npc)
    {
        for (int i = Claims.Count - 1; i >= 0; i--)
        {
            if (Claims[i].Npc == npc)
                Claims[i].Abandon();
        }
    }

    internal static void Remove(NpcClaim claim) => Claims.Remove(claim);

    private static NpcClaim Find(int npcId)
    {
        for (int i = 0; i < Claims.Count; i++)
        {
            if (Claims[i].NpcId == npcId)
                return Claims[i];
        }
        return null;
    }

    private static void Clear()
    {
        for (int i = Claims.Count - 1; i >= 0; i--)
            Claims[i].Dispose();
        Claims.Clear();
        Tracked.Clear();
    }
}
