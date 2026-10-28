using System;
using HarmonyLib;
using UnityEngine;

namespace ObenFramework.World;

/// <summary>
/// Turns the game's lock methods into <see cref="Locks"/> events. A lock is a plain object, so the patches on
/// <c>Lock</c> find its component through <see cref="LockHolders"/>; a door sets its lock flag itself, so its
/// own methods are watched too. Each change is reported once: the door methods don't go through the lock's.
/// </summary>
internal static class LockPatches
{
    private static void Raise(Component owner, bool locked)
    {
        if (Locks.Changed.HasSubscribers && WorldChanges.Active)
            Locks.Changed.Raise(new LockChange(owner, locked, WorldChanges.Current));
    }

    [HarmonyPatch(typeof(Lock))]
    private static class LockStatePatch
    {
        [HarmonyTargetMethods]
        private static System.Collections.Generic.IEnumerable<System.Reflection.MethodBase> Targets()
        {
            yield return AccessTools.Method(typeof(Lock), nameof(Lock._Lock));
            yield return AccessTools.Method(typeof(Lock), nameof(Lock._Unlock));
        }

        [HarmonyPrefix]
        private static void Before(Lock __instance, out bool __state) => __state = __instance.Locked;

        [HarmonyPostfix]
        private static void After(Lock __instance, bool __state)
        {
            try
            {
                if (__state != __instance.Locked && Locks.Changed.HasSubscribers && WorldChanges.Active)
                    Raise(LockHolders.OwnerOf(__instance), __instance.Locked);
            }
            catch (Exception e) { WorldLog.Failed("Lock hook", e); }
        }
    }

    [HarmonyPatch(typeof(Door))]
    private static class DoorLockPatch
    {
        [HarmonyTargetMethods]
        private static System.Collections.Generic.IEnumerable<System.Reflection.MethodBase> Targets()
        {
            yield return AccessTools.Method(typeof(Door), nameof(Door.Lock));
            yield return AccessTools.Method(typeof(Door), nameof(Door.Unlock));
            yield return AccessTools.Method(typeof(Door), nameof(Door.ToggleLock));
        }

        [HarmonyPrefix]
        private static void Before(Door __instance, out bool __state) => __state = __instance.Locked;

        [HarmonyPostfix]
        private static void After(Door __instance, bool __state)
        {
            try
            {
                if (__state != __instance.Locked)
                    Raise(__instance, __instance.Locked);
            }
            catch (Exception e) { WorldLog.Failed("Door lock hook", e); }
        }
    }

    // The game tells the lock who it belongs to when it is used; keep that, it saves a scan later.
    [HarmonyPatch(typeof(Lock), nameof(Lock.Use))]
    private static class UsePatch
    {
        [HarmonyPrefix]
        private static void Before(Lock __instance, GameObject parent)
        {
            try { LockHolders.Remember(__instance, LockHolders.HolderOn(parent)); }
            catch (Exception e) { WorldLog.Failed("Lock use hook", e); }
        }
    }

    [HarmonyPatch(typeof(Lock), nameof(Lock.LockpickSuccessCallback))]
    private static class LockpickPatch
    {
        // The game clears these at the end; they are only set while its own lockpicking runs.
        [HarmonyPrefix]
        private static void Before(GameObject ____parent, Action ____CallbackUnlock, out GameObject __state) =>
            __state = ____CallbackUnlock != null ? ____parent : null;

        [HarmonyPostfix]
        private static void After(bool success, GameObject __state)
        {
            try
            {
                if (__state != null && Locks.Lockpicked.HasSubscribers && WorldChanges.Active)
                    Locks.Lockpicked.Raise(new LockpickResult(LockHolders.HolderOn(__state), success));
            }
            catch (Exception e) { WorldLog.Failed("Lockpick hook", e); }
        }
    }

    [HarmonyPatch(typeof(Lock), nameof(Lock.AbortLockpicking))]
    private static class AbortPatch
    {
        [HarmonyPostfix]
        private static void After(Lock __instance)
        {
            try { LockPicking.Aborted(__instance); }
            catch (Exception e) { WorldLog.Failed("Lockpick abort hook", e); }
        }
    }
}
