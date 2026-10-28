using System;
using HarmonyLib;
using ObenFramework.Patching;
using ObenFramework.World;

namespace ObenFramework.Economy;

/// <summary>
/// The moments stock has to be (re)applied. Traders: when their storage finishes loading a save. Vending
/// machines: when they start, and around the game's own save and load of the machine, which must see the
/// vanilla entry list only (see <see cref="ShopStockVending"/>).
/// </summary>
[HarmonyPatch]
[PatchArea("Shops")]
internal static class ShopStockPatches
{
    [HarmonyPatch(typeof(Storage), nameof(Storage.OnLoadingGame))]
    [HarmonyPostfix]
    private static void StorageLoaded(Storage __instance)
    {
        try { ShopStockTraders.Loaded(__instance); }
        catch (Exception e) { WorldLog.Failed("Trader stock hook (load)", e); }
    }

    [HarmonyPatch(typeof(VendingMachine), "Start")]
    [HarmonyPostfix]
    private static void VendingStarted(VendingMachine __instance)
    {
        try { ShopStockVending.Attach(__instance, restoreStock: true); }
        catch (Exception e) { WorldLog.Failed("Vending stock hook (start)", e); }
    }

    [HarmonyPatch(typeof(VendingMachine), nameof(VendingMachine.OnSavingGame))]
    [HarmonyPrefix]
    private static void VendingSaving(VendingMachine __instance, out bool __state)
    {
        __state = false;
        try { __state = ShopStockVending.Detach(__instance, rememberStock: true); }
        catch (Exception e) { WorldLog.Failed("Vending stock hook (save)", e); }
    }

    [HarmonyPatch(typeof(VendingMachine), nameof(VendingMachine.OnSavingGame))]
    [HarmonyFinalizer]
    private static Exception VendingSaved(VendingMachine __instance, bool __state, Exception __exception)
    {
        if (__state)
        {
            try { ShopStockVending.Attach(__instance, restoreStock: false); }
            catch (Exception e) { WorldLog.Failed("Vending stock hook (saved)", e); }
        }
        return __exception;
    }

    [HarmonyPatch(typeof(VendingMachine), nameof(VendingMachine.OnLoadingGame))]
    [HarmonyPrefix]
    private static void VendingLoading(VendingMachine __instance)
    {
        try { ShopStockVending.Detach(__instance, rememberStock: false); }
        catch (Exception e) { WorldLog.Failed("Vending stock hook (load)", e); }
    }

    [HarmonyPatch(typeof(VendingMachine), nameof(VendingMachine.OnLoadingGame))]
    [HarmonyFinalizer]
    private static Exception VendingLoaded(VendingMachine __instance, Exception __exception)
    {
        try { ShopStockVending.Attach(__instance, restoreStock: true); }
        catch (Exception e) { WorldLog.Failed("Vending stock hook (loaded)", e); }
        return __exception;
    }
}
