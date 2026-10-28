// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
using HarmonyLib;
using NPC;
using UnityEngine;

namespace ObenFramework.Npcs;

/// <summary>
/// Chairs and beds for sit and sleep orders: the nearest free one to a point, wrapped in the game's own
/// <c>Act_sit</c> or <c>Act_Sleep</c> activity. Looked up once when an order starts, never per frame.
/// </summary>
internal static class NpcSeats
{
    private const float ChairRange = 1.5f;
    private const float BedRange = 2f;

    private static readonly AccessTools.FieldRef<InteractableChair, bool> HidesNpc = AccessTools.FieldRefAccess<InteractableChair, bool>("hideNpc");

    /// <summary>A sit activity on the nearest usable chair, or null when there is none in range.</summary>
    public static Activity SitNear(Vector3 point)
    {
        InteractableChair found = null;
        float best = ChairRange;
        foreach (var chair in Object.FindObjectsByType<InteractableChair>(FindObjectsSortMode.None))
        {
            if (chair == null || !chair.gameObject.activeInHierarchy || HidesNpc(chair))
                continue;
            float distance = Vector3.Distance(chair.transform.position, point);
            if (distance <= best)
            {
                found = chair;
                best = distance;
            }
        }
        if (found == null)
            return null;
        return new Act_sit
        {
            activityName = "mod sit",
            interactableChair = new InteractableChairReference { chair = found },
        };
    }

    /// <summary>A sleep activity in the nearest free bed, or null when there is none in range.</summary>
    public static Activity SleepNear(Vector3 point)
    {
        Sleep found = null;
        float best = BedRange;
        foreach (var bed in Object.FindObjectsByType<Sleep>(FindObjectsSortMode.None))
        {
            if (bed == null || bed.occupied || !bed.gameObject.activeInHierarchy)
                continue;
            float distance = Vector3.Distance(bed.transform.position, point);
            if (distance <= best)
            {
                found = bed;
                best = distance;
            }
        }
        if (found == null)
            return null;
        return new Act_Sleep
        {
            activityName = "mod sleep",
            bed = new SleepReference(found),
        };
    }
}
