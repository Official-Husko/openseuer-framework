// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
using System;
using System.Collections.Generic;
using HarmonyLib;
using ObenFramework.Items;
using ObenFramework.Saves;
using ObenFramework.World;
using UnityEngine;

namespace ObenFramework.Housing;

/// <summary>A pile of trash furniture leaves behind or tenants make, with what it is made of.</summary>
public sealed class TrashPileInfo
{
    internal TrashPileInfo(string id, Vector3 position, List<(int itemId, int amount)> items, List<ItemStackInfo> stacks)
    {
        Id = id;
        Position = position;
        Items = items;
        Stacks = stacks;
    }

    public string Id { get; }

    public Vector3 Position { get; }

    /// <summary>The items the pile turns into when cleaned up.</summary>
    public List<(int itemId, int amount)> Items { get; }

    /// <summary>The same items as full stacks (quality, liquid, owner and the rest).</summary>
    public List<ItemStackInfo> Stacks { get; }
}

/// <summary>An apartment's cleaning job started or finished, as raised by <see cref="Cleaning.Changed"/>.</summary>
public readonly struct CleaningChange
{
    public CleaningChange(int apartment, CleaningState state, int completeDay, ChangeSource source)
    {
        Apartment = apartment;
        State = state;
        CompleteDay = completeDay;
        Source = source;
    }

    public int Apartment { get; }

    /// <summary><see cref="CleaningState.Running"/> when it started, <see cref="CleaningState.Done"/> when it finished.</summary>
    public CleaningState State { get; }

    /// <summary>The game day it finishes, while it runs; 0 otherwise.</summary>
    public int CompleteDay { get; }

    public ChangeSource Source { get; }
}

/// <summary>
/// Cleaning in the tenement: an apartment's cleaning job (the story starts one for the shop, apartment 2, from
/// a conversation), and the trash piles of the loaded scenes.
/// </summary>
public static class Cleaning
{
    /// <summary>A cleaning job started or finished. Not raised while a save loads.</summary>
    public static readonly WorldEvent<CleaningChange> Changed = new WorldEvent<CleaningChange>(nameof(Changed));

    /// <summary>Where an apartment's cleaning job is, and the game day it finishes while it runs (0 otherwise).</summary>
    public static CleaningState StateOf(int apartment, out int completeDay)
    {
        completeDay = 0;
        var found = Tenement.Find(apartment);
        if (found == null)
            return CleaningState.NoSuchApartment;
        var state = HousingRules.CleaningOf(found.cleaningTimerCompleteDay);
        if (state == CleaningState.Running)
            completeDay = found.cleaningTimerCompleteDay;
        return state;
    }

    public static CleaningState StateOf(int apartment) => StateOf(apartment, out _);

    /// <summary>
    /// Starts an apartment's cleaning job: it is done when the game day turns <paramref name="days"/> days from
    /// now. Like the game, only once per apartment: false when one ran or runs, for an unknown apartment, or
    /// while the game saves.
    /// </summary>
    public static bool Start(int apartment, int days)
    {
        if (days < 1)
            throw new ArgumentOutOfRangeException(nameof(days), "At least one day.");
        if (SaveGate.IsBusy || StateOf(apartment) != CleaningState.NotStarted || TimeOfDayAzure.instance == null)
            return false;
        using (WorldChanges.EnterMod())
            TenementController.instance.StartApartmentCleaningTimer(apartment, days);
        return StateOf(apartment) == CleaningState.Running;
    }

    /// <summary>Finishes a running cleaning job now. False when none runs, or while the game saves.</summary>
    public static bool Finish(int apartment)
    {
        var found = Tenement.Find(apartment);
        if (SaveGate.IsBusy || found == null || HousingRules.CleaningOf(found.cleaningTimerCompleteDay) != CleaningState.Running)
            return false;
        found.cleaningTimerCompleteDay = 0;
        found.prefabs?.UpdateCleaningTimerState();
        using (WorldChanges.EnterMod())
            CleaningPatches.Raise(found.apartmentNumber, CleaningState.Done, 0);
        return true;
    }

    /// <summary>Every trash pile in the loaded scenes. A scan.</summary>
    public static List<TrashPileInfo> TrashPiles()
    {
        var list = new List<TrashPileInfo>();
        foreach (var trash in ObjectIds.Active<FurnitureTrash>())
        {
            var items = new List<(int, int)>();
            var stacks = new List<ItemStackInfo>();
            if (trash.items != null)
            {
                foreach (var stack in trash.items)
                {
                    if (stack == null || stack.itemId == -1 || stack.itemAmount <= 0)
                        continue;
                    items.Add((stack.itemId, stack.itemAmount));
                    stacks.Add(ItemStackInfo.From(stack));
                }
            }
            list.Add(new TrashPileInfo(string.IsNullOrEmpty(trash.GUID) ? ObjectIds.IdOf(trash) : trash.GUID, trash.transform.position, items, stacks));
        }
        return list;
    }
}

/// <summary>Raises <see cref="Cleaning.Changed"/> from the game's start (a dialogue's Lua) and its day check.</summary>
[HarmonyPatch]
internal static class CleaningPatches
{
    [HarmonyPrefix]
    [HarmonyPatch(typeof(TenementController), nameof(TenementController.StartApartmentCleaningTimer))]
    private static void BeforeStart(double apartmentNumber, out int __state) => __state = Cleaning.StateOf((int)apartmentNumber) == CleaningState.NotStarted ? 1 : 0;

    [HarmonyPostfix]
    [HarmonyPatch(typeof(TenementController), nameof(TenementController.StartApartmentCleaningTimer))]
    private static void AfterStart(double apartmentNumber, int __state)
    {
        int number = (int)apartmentNumber;
        if (__state == 1 && Cleaning.StateOf(number, out int day) == CleaningState.Running)
            Raise(number, CleaningState.Running, day);
    }

    // OnCleaningTimerDayChanged runs on every day change; only apartments whose job was running are of interest.
    [HarmonyPrefix]
    [HarmonyPatch(typeof(TenementController), "OnCleaningTimerDayChanged")]
    private static void BeforeDay(TenementController __instance, out List<int> __state)
    {
        __state = null;
        if (!Cleaning.Changed.HasSubscribers || __instance.apartments == null)
            return;
        foreach (var apartment in __instance.apartments)
        {
            if (apartment != null && apartment.cleaningTimerCompleteDay > 0)
                (__state ??= new List<int>()).Add(apartment.apartmentNumber);
        }
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(TenementController), "OnCleaningTimerDayChanged")]
    private static void AfterDay(List<int> __state)
    {
        if (__state == null)
            return;
        foreach (int number in __state)
        {
            if (Cleaning.StateOf(number) == CleaningState.Done)
                Raise(number, CleaningState.Done, 0);
        }
    }

    internal static void Raise(int apartment, CleaningState state, int day)
    {
        if (!Cleaning.Changed.HasSubscribers || !WorldChanges.Active)
            return;
        try
        {
            Cleaning.Changed.Raise(new CleaningChange(apartment, state, day, WorldChanges.Current));
        }
        catch (Exception e)
        {
            WorldLog.Failed("Cleaning hook", e);
        }
    }
}
