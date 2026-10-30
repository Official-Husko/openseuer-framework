// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
using System;
using System.Collections.Generic;
using HarmonyLib;
using ObenFramework.Saves;
using ObenFramework.World;
using UnityEngine;

namespace ObenFramework.Items;

/// <summary>A snapshot of an item giver: an interactable that hands out items (a tap, a box of flyers, a tree).</summary>
public readonly struct ItemGiverInfo
{
    public ItemGiverInfo(string id, string title, Vector3 position, bool canGive, bool unlimited, int amount)
    {
        Id = id;
        Title = title;
        Position = position;
        CanGive = canGive;
        Unlimited = unlimited;
        Amount = amount;
    }

    public string Id { get; }

    public string Title { get; }

    public Vector3 Position { get; }

    /// <summary>It is switched on (a script can switch it off).</summary>
    public bool CanGive { get; }

    /// <summary>It never runs out.</summary>
    public bool Unlimited { get; }

    /// <summary>How many more times it gives before it is empty, or -1 when unlimited.</summary>
    public int Amount { get; }

    public bool Empty => !Unlimited && Amount < 1;
}

/// <summary>An item giver handed something out, raised by <see cref="ItemGivers.Gave"/>.</summary>
public readonly struct ItemGiven
{
    public ItemGiven(string giverId, int itemId, int amount)
    {
        GiverId = giverId;
        ItemId = itemId;
        Amount = amount;
    }

    public string GiverId { get; }

    public int ItemId { get; }

    public int Amount { get; }
}

/// <summary>Item givers in the loaded scenes (the game's <c>InteractableItemGiver</c>): listing, emptying, refilling and hearing them give.</summary>
public static class ItemGivers
{
    /// <summary>
    /// The framework's item hooks are in place. False when they failed to apply (a game update changed what they
    /// patch); the events then stay quiet and changes the game should pick up may not take.
    /// </summary>
    public static bool IsAvailable => ItemsModule.HooksInPlace;

    private static readonly AccessTools.FieldRef<InteractableItemGiver, int> CurrentAmount =
        AccessTools.FieldRefAccess<InteractableItemGiver, int>("currentAmount");

    private static readonly AccessTools.FieldRef<InteractableItemGiver, int> MinAmount =
        AccessTools.FieldRefAccess<InteractableItemGiver, int>("minItemAmount");

    private static readonly AccessTools.FieldRef<InteractableItemGiver, int> MaxAmount =
        AccessTools.FieldRefAccess<InteractableItemGiver, int>("maxItemAmount");

    private static readonly Action<InteractableItemGiver> RefillGiver =
        AccessTools.MethodDelegate<Action<InteractableItemGiver>>(AccessTools.Method(typeof(InteractableItemGiver), "RefillItemGiver"));

    private static readonly Action<InteractableItemGiver> UpdateGiverState =
        AccessTools.MethodDelegate<Action<InteractableItemGiver>>(AccessTools.Method(typeof(InteractableItemGiver), "UpdateState"));

    /// <summary>A giver handed the player an item.</summary>
    public static readonly WorldEvent<ItemGiven> Gave = new WorldEvent<ItemGiven>(nameof(Gave));

    public static List<ItemGiverInfo> All()
    {
        var list = new List<ItemGiverInfo>();
        foreach (var giver in ObjectIds.Active<InteractableItemGiver>())
            list.Add(Describe(giver));
        return list;
    }

    public static InteractableItemGiver Find(string id) => ObjectIds.Find<InteractableItemGiver>(id);

    /// <summary>Empties a giver or gives it one more use. Unlimited givers are refused.</summary>
    public static bool SetEmpty(string id, bool empty)
    {
        var giver = Find(id);
        if (giver == null || SaveGate.IsBusy || Unlimited(giver))
            return false;
        int amount = CurrentAmount(giver);
        if (empty ? amount > 0 : amount < 1)
        {
            CurrentAmount(giver) = empty ? 0 : 1;
            UpdateGiverState(giver);
        }
        return (CurrentAmount(giver) < 1) == empty;
    }

    /// <summary>Refills a giver as its own timer does. Unlimited givers are refused.</summary>
    public static bool Refill(string id)
    {
        var giver = Find(id);
        if (giver == null || SaveGate.IsBusy || Unlimited(giver))
            return false;
        RefillGiver(giver);
        return true;
    }

    public static ItemGiverInfo Describe(InteractableItemGiver giver)
    {
        bool unlimited = Unlimited(giver);
        return new ItemGiverInfo(ObjectIds.IdOf(giver), giver.title, giver.transform.position, giver.canGive, unlimited, unlimited ? -1 : CurrentAmount(giver));
    }

    // The game's own rule (HasInfiniteAmount): no amount range means no limit.
    private static bool Unlimited(InteractableItemGiver giver) => MinAmount(giver) == -1 || MaxAmount(giver) == -1;
}

[HarmonyPatch]
internal static class ItemGiversPatches
{
    [HarmonyPostfix]
    [HarmonyPatch(typeof(InteractableItemGiver), "GiveItem")]
    private static void Given(InteractableItemGiver __instance, ItemReference item, ref bool success, int amount)
    {
        if (!success || item == null || !ItemGivers.Gave.HasSubscribers || !WorldChanges.Active)
            return;
        try { ItemGivers.Gave.Raise(new ItemGiven(ObjectIds.IdOf(__instance), item.ID, amount)); }
        catch (Exception e) { ItemsLog.Failed("Item giver hook", e); }
    }
}
