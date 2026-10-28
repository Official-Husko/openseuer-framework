// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
using System;
using System.Collections.Generic;
using HarmonyLib;
using ObenFramework.Items;
using ObenFramework.World;

namespace ObenFramework.Env;

/// <summary>A snapshot of one blast site (a dynamite cone and the cones linked to it).</summary>
public sealed class BlastSiteInfo
{
    internal BlastSiteInfo(string id, string name, bool dynamitePlaced, bool detonated, int linked, int linkedPlaced)
    {
        Id = id;
        Name = name;
        DynamitePlaced = dynamitePlaced;
        Detonated = detonated;
        Linked = linked;
        LinkedPlaced = linkedPlaced;
    }

    public string Id { get; }

    public string Name { get; }

    public bool DynamitePlaced { get; }

    /// <summary>It blew up already (saved with the game).</summary>
    public bool Detonated { get; }

    /// <summary>Cones that blow up together, this one included.</summary>
    public int Linked { get; }

    /// <summary>How many of the linked cones have dynamite in them; all need it before any goes off.</summary>
    public int LinkedPlaced { get; }
}

/// <summary>
/// Blast sites in the mines: cones that take dynamite (item 35140) and blow up together once every linked
/// cone has some. Read them, set them off, and hear when one goes off.
/// </summary>
public static class BlastSites
{
    /// <summary>A cone blew up. Raised once per cone; linked cones raise their own.</summary>
    public static readonly WorldEvent<InteractableDynamiteCone> Detonated = new WorldEvent<InteractableDynamiteCone>(nameof(Detonated));

    public static List<BlastSiteInfo> All()
    {
        var list = new List<BlastSiteInfo>();
        foreach (var cone in ObjectIds.Active<InteractableDynamiteCone>())
            list.Add(Describe(cone));
        return list;
    }

    public static InteractableDynamiteCone Find(string id) => ObjectIds.Find<InteractableDynamiteCone>(id);

    public static BlastSiteInfo Describe(InteractableDynamiteCone cone) =>
        new BlastSiteInfo(ObjectIds.IdOf(cone), cone.name, cone.dynamitePlaced, cone.detonated, cone.CountLinkedCones(false), cone.CountLinkedCones(true));

    /// <summary>
    /// Sets the cone off, like its fuse: only when it has dynamite and so do all cones linked to it, which go
    /// off with it. Returns whether it blew up.
    /// </summary>
    public static bool Detonate(InteractableDynamiteCone cone)
    {
        if (cone == null)
            throw new ArgumentNullException(nameof(cone));
        if (!cone.dynamitePlaced)
            return false;
        using (WorldChanges.EnterMod())
            cone.Detonate();
        return cone.detonated && !cone.dynamitePlaced;
    }
}

[HarmonyPatch(typeof(InteractableDynamiteCone), nameof(InteractableDynamiteCone.Detonate))]
internal static class BlastSitePatch
{
    [HarmonyPrefix]
    private static void Before(InteractableDynamiteCone __instance, out bool __state) => __state = __instance.dynamitePlaced;

    [HarmonyPostfix]
    private static void After(InteractableDynamiteCone __instance, bool __state)
    {
        if (!__state || __instance.dynamitePlaced || !__instance.detonated || !BlastSites.Detonated.HasSubscribers || !WorldChanges.Active)
            return;
        try
        {
            BlastSites.Detonated.Raise(__instance);
        }
        catch (Exception e)
        {
            WorldLog.Failed("Blast site hook", e);
        }
    }
}
