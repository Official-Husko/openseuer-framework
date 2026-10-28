// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
using System.Collections.Generic;
using ObenFramework.Items;
using UnityEngine;

namespace ObenFramework.Housing;

/// <summary>A pile of trash furniture leaves behind or tenants make, with what it is made of.</summary>
public sealed class TrashPileInfo
{
    internal TrashPileInfo(string id, Vector3 position, List<(int itemId, int amount)> items)
    {
        Id = id;
        Position = position;
        Items = items;
    }

    public string Id { get; }

    public Vector3 Position { get; }

    /// <summary>The items the pile turns into when cleaned up.</summary>
    public List<(int itemId, int amount)> Items { get; }
}

/// <summary>Cleaning in the tenement: an apartment's cleaning job, and the trash piles of the loaded scenes.</summary>
public static class Cleaning
{
    /// <summary>Where an apartment's cleaning job is, and the game day it finishes while it runs (0 otherwise).</summary>
    public static CleaningState StateOf(int apartment, out int completeDay)
    {
        completeDay = 0;
        var found = Tenement.Find(apartment);
        if (found == null)
            return CleaningState.NotStarted;
        var state = HousingRules.CleaningOf(found.cleaningTimerCompleteDay);
        if (state == CleaningState.Running)
            completeDay = found.cleaningTimerCompleteDay;
        return state;
    }

    public static CleaningState StateOf(int apartment) => StateOf(apartment, out _);

    /// <summary>Every trash pile in the loaded scenes. A scan.</summary>
    public static List<TrashPileInfo> TrashPiles()
    {
        var list = new List<TrashPileInfo>();
        foreach (var trash in ObjectIds.Active<FurnitureTrash>())
        {
            var items = new List<(int, int)>();
            if (trash.items != null)
            {
                foreach (var stack in trash.items)
                {
                    if (stack != null && stack.itemId != -1 && stack.itemAmount > 0)
                        items.Add((stack.itemId, stack.itemAmount));
                }
            }
            list.Add(new TrashPileInfo(string.IsNullOrEmpty(trash.GUID) ? ObjectIds.IdOf(trash) : trash.GUID, trash.transform.position, items));
        }
        return list;
    }
}
