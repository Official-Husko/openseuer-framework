// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
using System;
using System.Collections.Generic;
using HarmonyLib;
using ObenFramework.Economy;
using ObenFramework.Saves;
using ObenLoader;

namespace ObenFramework.Building;

/// <summary>What the framework keeps per save for custom furniture.</summary>
public sealed class CustomFurnitureSave
{
    /// <summary>Per furniture shop (by its id): the custom pieces it put on sale since its last restock.</summary>
    public Dictionary<string, List<string>> Offered = new Dictionary<string, List<string>>();

    /// <summary>Custom ids this save has seen, so a missing mod can be named.</summary>
    public List<string> Seen = new List<string>();
}

/// <summary>
/// Puts custom furniture on sale at the furniture shops its definition names. A shop gets the pieces each time
/// it restocks; a shop loaded from a save that never offered a piece (the mod is new to the save) gets it once.
/// The record of what each shop offered since its last restock is kept per save, so a piece the player bought
/// out doesn't come back on every load.
/// </summary>
internal static class FurnitureShops
{
    private static readonly AccessTools.FieldRef<FurnitureShop, bool> CanSellItems = AccessTools.FieldRefAccess<FurnitureShop, bool>("canSellItems");
    private static ModSaveData<CustomFurnitureSave> _save;

    public static CustomFurnitureSave Data => _save?.Value;

    public static void Init(ObenMod framework) => _save = ModSaves.Register<CustomFurnitureSave>(framework, "customfurniture");

    public static void Reset() => _save = null;

    /// <summary>The shop restocked: it offers every piece listed for it, fresh.</summary>
    public static void Restocked(FurnitureShop shop)
    {
        var info = Shops.Of(shop);
        if (info == null || _save == null)
            return;
        var offered = Offered(info.Id);
        offered.Clear();
        Offer(shop, info, offered);
    }

    /// <summary>The shop came from a save: add what it never offered.</summary>
    public static void Loaded(FurnitureShop shop)
    {
        var info = Shops.Of(shop);
        if (info == null || _save == null)
            return;
        Offer(shop, info, Offered(info.Id));
    }

    /// <summary>The shop buys custom pieces it sells, and only those.</summary>
    public static bool Buys(FurnitureShop shop, global::Furniture furniture)
    {
        var entry = FurnitureRegistry.Find(furniture.id);
        return entry != null && CanSellItems(shop) && entry.SoldAt(Shops.Of(shop));
    }

    private static void Offer(FurnitureShop shop, ShopInfo info, List<string> offered)
    {
        foreach (var entry in FurnitureRegistry.All)
        {
            if (!entry.SoldAt(info) || offered.Contains(entry.Id))
                continue;
            var furniture = FurnitureBuild.Registered(entry);
            if (furniture == null)
                continue;
            offered.Add(entry.Id);
            if (Stocked(shop, entry.Id))
                continue;
            var meta = new BuildingSystem.FurnitureInfo.Meta();
            shop.availableFurnitures.Add(new BuildingSystem.FurnitureInfo(furniture, meta, global::Furniture.CreateTaskItem(furniture, meta), null, entry.Definition.ShopStock));
        }
    }

    private static List<string> Offered(string shopId)
    {
        var all = _save.Value.Offered;
        if (!all.TryGetValue(shopId, out var list) || list == null)
            all[shopId] = list = new List<string>();
        return list;
    }

    private static bool Stocked(FurnitureShop shop, string id)
    {
        foreach (var info in shop.availableFurnitures)
        {
            if (info.furniture != null && info.furniture.Id == id)
                return true;
        }
        return false;
    }

    [HarmonyPatch(typeof(FurnitureShop), "UpdateShopItems")]
    private static class RestockPatch
    {
        [HarmonyPostfix]
        private static void After(FurnitureShop __instance)
        {
            try { Restocked(__instance); }
            catch (Exception e) { FurnitureRegistry.Log.Error("Furniture shop restock hook", e); }
        }
    }

    [HarmonyPatch(typeof(FurnitureShop), nameof(FurnitureShop.OnLoadingGame))]
    private static class LoadPatch
    {
        [HarmonyPostfix]
        private static void After(FurnitureShop __instance, bool __result)
        {
            if (!__result)
                return;
            try { Loaded(__instance); }
            catch (Exception e) { FurnitureRegistry.Log.Error("Furniture shop load hook", e); }
        }
    }

    [HarmonyPatch(typeof(FurnitureShop), nameof(FurnitureShop.CanSell))]
    private static class SellPatch
    {
        [HarmonyPostfix]
        private static void After(FurnitureShop __instance, BuildingSystem.FurnitureInfo furnitureInfo, ref bool __result)
        {
            if (__result || furnitureInfo == null || furnitureInfo.furniture == null || !FurnitureIds.IsModId(furnitureInfo.furniture.id))
                return;
            try { __result = Buys(__instance, furnitureInfo.furniture); }
            catch (Exception e) { FurnitureRegistry.Log.Error("Furniture shop sell hook", e); }
        }
    }
}
