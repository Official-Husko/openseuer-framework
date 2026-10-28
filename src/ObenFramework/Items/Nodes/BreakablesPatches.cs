// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
using System;
using HarmonyLib;
using ObenFramework.World;
using UnityEngine;

namespace ObenFramework.Items;

/// <summary>
/// Reports breaking, repairing, lock breaking and smashing by comparing an object's strength or health before
/// and after the game's own call. Each pair does nothing while nobody listens.
/// </summary>
[HarmonyPatch]
internal static class BreakablesPatches
{
    [HarmonyPrefix]
    [HarmonyPatch(typeof(Breakable), nameof(Breakable.Break))]
    private static void Breaking(Breakable __instance, out bool __state) =>
        __state = Breakables.Broken.HasSubscribers && __instance.strength > 0;

    [HarmonyPostfix]
    [HarmonyPatch(typeof(Breakable), nameof(Breakable.Break))]
    private static void Broke(Breakable __instance, bool onLoadingGame, bool __state)
    {
        if (!__state || onLoadingGame || __instance.strength > 0 || !WorldChanges.Active)
            return;
        try { Breakables.Broken.Raise(ObjectIds.IdOf(__instance)); }
        catch (Exception e) { ItemsLog.Failed("Breakable broken hook", e); }
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(Breakable), nameof(Breakable.Repair))]
    private static void Repairing(Breakable __instance, out bool __state) =>
        __state = Breakables.Repaired.HasSubscribers && __instance.strength <= 0;

    [HarmonyPostfix]
    [HarmonyPatch(typeof(Breakable), nameof(Breakable.Repair))]
    private static void RepairDone(Breakable __instance, bool __state)
    {
        if (!__state || __instance.strength <= 0 || !WorldChanges.Active)
            return;
        try { Breakables.Repaired.Raise(ObjectIds.IdOf(__instance)); }
        catch (Exception e) { ItemsLog.Failed("Breakable repaired hook", e); }
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(BreakableLock), nameof(BreakableLock.Break))]
    private static void LockBreaking(BreakableLock __instance, out bool __state) =>
        __state = Breakables.LockBroken.HasSubscribers && __instance.strength > 0;

    [HarmonyPostfix]
    [HarmonyPatch(typeof(BreakableLock), nameof(BreakableLock.Break))]
    private static void LockBroke(BreakableLock __instance, bool __state)
    {
        if (!__state || __instance.strength > 0 || !WorldChanges.Active)
            return;
        try { Breakables.LockBroken.Raise(ObjectIds.IdOf(__instance)); }
        catch (Exception e) { ItemsLog.Failed("Lock broken hook", e); }
    }

    // The object may destroy itself in Damage, so what to report is read before.
    [HarmonyPrefix]
    [HarmonyPatch(typeof(SpawnItemOnBreak), nameof(SpawnItemOnBreak.Damage),
        new[] { typeof(int), typeof(Vector3), typeof(Vector3), typeof(DamageReceiver.DamageType) })]
    private static void Smashing(SpawnItemOnBreak __instance, int damage, DamageReceiver.DamageType type, out SmashedObject? __state)
    {
        __state = null;
        if (!Breakables.Smashed.HasSubscribers || !WorldChanges.Active)
            return;
        try
        {
            if (!__instance.CheckDamageType(type) || __instance.health - damage > 0)
                return;
            var spawn = __instance.itemToSpawn;
            int itemId = spawn != null && spawn.Item != null && spawn.Item.ID != 0 ? spawn.Item.ID : -1;
            __state = new SmashedObject(ObjectIds.IdOf(__instance), __instance.transform.position, __instance.objectType == SpawnItemOnBreak.Type.Animal, itemId);
        }
        catch (Exception e)
        {
            ItemsLog.Failed("Smash hook", e);
        }
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(SpawnItemOnBreak), nameof(SpawnItemOnBreak.Damage),
        new[] { typeof(int), typeof(Vector3), typeof(Vector3), typeof(DamageReceiver.DamageType) })]
    private static void Smashed(SpawnItemOnBreak __instance, SmashedObject? __state)
    {
        if (__state == null || __instance.health > 0)
            return;
        try { Breakables.Smashed.Raise(__state.Value); }
        catch (Exception e) { ItemsLog.Failed("Smash hook", e); }
    }
}
