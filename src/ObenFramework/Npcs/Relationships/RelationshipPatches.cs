// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
using System;
using HarmonyLib;

namespace ObenFramework.Npcs;

/// <summary>
/// Raises <see cref="Relationships.Changed"/> from the three places the game changes a relationship: the
/// daily drift (<c>Relationship.CheckDateAndTrigger</c>), and adding and removing one on the controller.
/// Every hook returns at once while nobody listens.
/// </summary>
[HarmonyPatch]
internal static class RelationshipPatches
{
    [HarmonyPrefix]
    [HarmonyPatch(typeof(Relationship), nameof(Relationship.CheckDateAndTrigger))]
    private static void BeforeDrift(RelationshipController.RelationshipInfo info, out float __state) =>
        __state = info != null ? info.disposition : 0f;

    [HarmonyPostfix]
    [HarmonyPatch(typeof(Relationship), nameof(Relationship.CheckDateAndTrigger))]
    private static void AfterDrift(Relationship __instance, RelationshipController.RelationshipInfo info, float __state)
    {
        if (info == null || info.disposition == __state)
            return;
        Guard("Relationship drift hook", () => Relationships.Raise(RelationshipChangeKind.Disposition, __instance, __state, info.disposition));
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(RelationshipController), nameof(RelationshipController.AddRelationship))]
    private static void AfterAdd(RelationshipController __instance, Relationship relationship, bool __result)
    {
        if (!__result || relationship == null || !Relationships.Changed.HasSubscribers)
            return;
        Guard("Relationship added hook", () =>
        {
            float value = Disposition(__instance, relationship);
            Relationships.Raise(RelationshipChangeKind.Started, relationship, value, value);
        });
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(RelationshipController), nameof(RelationshipController.RemoveRelationship))]
    private static void BeforeRemove(RelationshipController __instance, Relationship relationship, out bool __state)
    {
        __state = relationship != null && Relationships.Changed.HasSubscribers && __instance.CheckIfRelationshipIsActive(relationship);
        if (!__state)
            return;
        Guard("Relationship removed hook", () =>
        {
            float value = Disposition(__instance, relationship);
            Relationships.Raise(RelationshipChangeKind.Ended, relationship, value, value);
        });
    }

    private static float Disposition(RelationshipController controller, Relationship relationship)
    {
        foreach (var info in controller.relationships)
        {
            if (info != null && info.relationship != null && info.relationship.Id == relationship.Id)
                return info.disposition;
        }
        return relationship.startDisposition;
    }

    private static void Guard(string where, Action action)
    {
        try { action(); }
        catch (Exception e) { NpcsLog.Failed(where, e); }
    }
}
