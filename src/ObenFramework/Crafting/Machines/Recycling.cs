// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
using System;
using System.Collections.Generic;
using HarmonyLib;
using ObenFramework.Items;
using ObenFramework.Saves;
using ObenFramework.World;
using UnityEngine;

namespace ObenFramework.Crafting;

/// <summary>A bottle recycling machine with the deposit it owes the player.</summary>
public readonly struct RecyclerInfo
{
    public RecyclerInfo(string id, Vector3 position, int owed)
    {
        Id = id;
        Position = position;
        Owed = owed;
    }

    public string Id { get; }

    public Vector3 Position { get; }

    /// <summary>Deposit collected so far and not yet paid out.</summary>
    public int Owed { get; }
}

/// <summary>Bottles went into a recycling machine, raised by <see cref="Recycling.Recycled"/>.</summary>
public readonly struct RecycledInfo
{
    public RecycledInfo(string machineId, int amount, int money)
    {
        MachineId = machineId;
        Amount = amount;
        Money = money;
    }

    public string MachineId { get; }

    /// <summary>How many items went in.</summary>
    public int Amount { get; }

    /// <summary>How much the machine's owed deposit grew.</summary>
    public int Money { get; }
}

/// <summary>Bottle recycling machines: listing them, reading and setting what they owe, and hearing bottles go in.</summary>
public static class Recycling
{
    /// <summary>Items went into a recycling machine.</summary>
    public static readonly WorldEvent<RecycledInfo> Recycled = new WorldEvent<RecycledInfo>(nameof(Recycled));

    public static List<RecyclerInfo> All()
    {
        var list = new List<RecyclerInfo>();
        foreach (var machine in ObjectIds.Active<BottleRecycling>())
            list.Add(new RecyclerInfo(ObjectIds.IdOf(machine), machine.transform.position, machine.bottleRecyclingMoney));
        return list;
    }

    public static BottleRecycling Find(string id) => ObjectIds.Find<BottleRecycling>(id);

    /// <summary>The deposit a machine owes, or -1 when there is no such machine.</summary>
    public static int Owed(string id)
    {
        var machine = Find(id);
        return machine != null ? machine.bottleRecyclingMoney : -1;
    }

    /// <summary>Sets what a machine owes. Refused while a save loads.</summary>
    public static bool SetOwed(string id, int owed)
    {
        var machine = Find(id);
        if (machine == null || owed < 0 || SaveGate.IsBusy)
            return false;
        machine.bottleRecyclingMoney = owed;
        return true;
    }
}

[HarmonyPatch]
internal static class RecyclingPatches
{
    [HarmonyPrefix]
    [HarmonyPatch(typeof(BottleRecyclingPanel), "BottleAdd")]
    private static void Adding(BottleRecyclingPanel __instance, out int __state) =>
        __state = Recycling.Recycled.HasSubscribers && __instance.currentBottleRecycling != null ? __instance.currentBottleRecycling.bottleRecyclingMoney : int.MinValue;

    [HarmonyPostfix]
    [HarmonyPatch(typeof(BottleRecyclingPanel), "BottleAdd")]
    private static void Added(BottleRecyclingPanel __instance, int amount, int __state)
    {
        var machine = __instance.currentBottleRecycling;
        if (__state == int.MinValue || machine == null || !WorldChanges.Active)
            return;
        try { Recycling.Recycled.Raise(new RecycledInfo(ObjectIds.IdOf(machine), amount, machine.bottleRecyclingMoney - __state)); }
        catch (Exception e) { ItemsLog.Failed("Recycling hook", e); }
    }
}
