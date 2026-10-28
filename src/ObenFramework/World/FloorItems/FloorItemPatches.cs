using System;
using HarmonyLib;
using UnityEngine;

namespace ObenFramework.World;

/// <summary>
/// The game's pickup entry points. Every pickup the game or a mod puts on the ground goes through
/// <c>ItemOperations.SpawnItemToPosition</c>; drops (the inventory window, a full inventory, a storage
/// spilling over) enter through <c>ItemOperations.DropItem(ItemStack, ...)</c> first. Taking a pickup is
/// <c>CollectibleItem.PerformPickup</c> or, for coins, <c>CollectibleCoin.Interact</c>.
/// </summary>
internal static class FloorItemPatches
{
    private static int _dropDepth;
    private static bool _dropByPlayer;

    internal struct Taking
    {
        public bool Tracked;
        public string Id;
        public int ItemId;
        public string Title;
        public int Amount;
        public Vector3 Position;
        public GameObject GameObject;
        public string Scene;
    }

    public static void Reset()
    {
        _dropDepth = 0;
        _dropByPlayer = false;
    }

    private static Taking Capture(Collectible collectible)
    {
        var taking = new Taking();
        if (!FloorItems.PickedUp.HasSubscribers || !WorldChanges.Active || !FloorItems.TryDescribe(collectible, out var info))
            return taking;

        taking.Tracked = true;
        taking.Id = info.Id;
        taking.ItemId = info.ItemId;
        taking.Title = info.Title;
        taking.Amount = info.Amount;
        taking.Position = info.Position;
        taking.GameObject = info.GameObject;
        taking.Scene = info.Scene;
        return taking;
    }

    private static void RaisePickedUp(Taking taking, int taken)
    {
        if (!taking.Tracked || taken <= 0)
            return;
        var info = new FloorItemInfo(taking.GameObject, taking.Id, taking.ItemId, taking.Title, taken, taking.Position, taking.Scene);
        FloorItems.PickedUp.Raise(new FloorItemEvent(info, WorldChanges.Current));
    }

    [HarmonyPatch]
    private static class Spawning
    {
        [HarmonyPrefix]
        [HarmonyPatch(typeof(ItemOperations), nameof(ItemOperations.DropItem), new[] { typeof(ItemStack), typeof(GameObject), typeof(ItemOperations.DropItemValues) })]
        private static void Dropping(GameObject dropper)
        {
            _dropDepth++;
            _dropByPlayer = dropper == null || (PlayerLocator.instance != null && dropper == PlayerLocator.instance.gameObject);
        }

        [HarmonyFinalizer]
        [HarmonyPatch(typeof(ItemOperations), nameof(ItemOperations.DropItem), new[] { typeof(ItemStack), typeof(GameObject), typeof(ItemOperations.DropItemValues) })]
        private static Exception Dropped(Exception __exception)
        {
            if (_dropDepth > 0)
                _dropDepth--;
            return __exception;
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(ItemOperations), nameof(ItemOperations.SpawnItemToPosition))]
        private static void Spawned(GameObject __result)
        {
            if (__result == null || !FloorItems.AnySubscribers || !WorldChanges.Active)
                return;
            try
            {
                var collectible = __result.GetComponent<Collectible>();
                if (collectible == null || !FloorItems.TryDescribe(collectible, out var info))
                    return;

                var source = WorldChanges.Current;
                bool dropping = _dropDepth > 0;
                if (dropping && source == ChangeSource.Game && _dropByPlayer)
                    source = ChangeSource.LocalPlayer;

                var spawned = new FloorItemEvent(info, source);
                FloorItems.Spawned.Raise(spawned);
                if (dropping)
                    FloorItems.Dropped.Raise(spawned);
            }
            catch (Exception e)
            {
                WorldLog.Failed("Floor item spawn hook", e);
            }
        }
    }

    [HarmonyPatch]
    private static class Taken
    {
        [HarmonyPrefix]
        [HarmonyPatch(typeof(CollectibleItem), nameof(CollectibleItem.PerformPickup))]
        private static void PickingUp(CollectibleItem __instance, out Taking __state)
        {
            __state = default;
            try { __state = Capture(__instance); }
            catch (Exception e) { WorldLog.Failed("Floor item pickup hook (before)", e); }
        }

        // A partial pickup leaves the rest in the pickup's amount and the call returns false.
        [HarmonyPostfix]
        [HarmonyPatch(typeof(CollectibleItem), nameof(CollectibleItem.PerformPickup))]
        private static void PickedUp(CollectibleItem __instance, bool __result, Taking __state)
        {
            if (!__state.Tracked)
                return;
            try
            {
                int left = __instance != null && !__result ? __instance.amount : 0;
                RaisePickedUp(__state, __state.Amount - left);
            }
            catch (Exception e)
            {
                WorldLog.Failed("Floor item pickup hook (after)", e);
            }
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(CollectibleCoin), nameof(CollectibleCoin.Interact))]
        private static void TakingCoins(CollectibleCoin __instance, out Taking __state)
        {
            __state = default;
            try { __state = Capture(__instance); }
            catch (Exception e) { WorldLog.Failed("Floor item coin hook (before)", e); }
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(CollectibleCoin), nameof(CollectibleCoin.Interact))]
        private static void TookCoins(CollectibleCoin __instance, Taking __state)
        {
            if (!__state.Tracked)
                return;
            try
            {
                RaisePickedUp(__state, __state.Amount);
            }
            catch (Exception e)
            {
                WorldLog.Failed("Floor item coin hook (after)", e);
            }
        }
    }
}
