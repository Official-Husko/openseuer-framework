// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
using ObenFramework.Items;
using OS.Items;

namespace ObenFramework.Building;

/// <summary>
/// Flatpacks: the game's "Furniture Flatpack" item (19410) carries one piece of furniture in its data and
/// unpacks into the building inventory when used. Any piece fits, custom or the game's own.
/// </summary>
public static class FurnitureFlatpacks
{
    /// <summary>The game's flatpack item.</summary>
    public const int ItemId = 19410;

    public const int MaxAmount = 100;

    /// <summary>Puts flatpacks of a piece into the player's inventory. False when there is no room.</summary>
    public static bool Give(global::Furniture furniture, int amount = 1)
    {
        if (furniture == null || amount < 1 || amount > MaxAmount)
            return false;
        var inventory = Inventory.instance;
        var item = ItemLookup.ById(ItemId);
        if (inventory == null || item == null || Item.GetMetaOfType<ItemFurnitureData>(item.CloneMeta()) == null)
            return false;
        if (!inventory.InventoryIsNotFull(item, -1, amount, 1, item.CloneMeta(), inventory.Slots))
            return false;
        for (int i = 0; i < amount; i++)
            ItemOperations.AddItem(item, -1, null, Fill(item.CloneMeta(), furniture), notify: true);
        return true;
    }

    /// <summary>The piece a flatpack's item data holds, or null for an empty one.</summary>
    public static global::Furniture ContentOf(object[] meta)
    {
        var data = Item.GetMetaOfType<ItemFurnitureData>(meta);
        return data != null && data.furnitureInfo != null ? data.furnitureInfo.furniture : null;
    }

    /// <summary>True for flatpack item data that holds nothing yet (fresh from a loot table).</summary>
    public static bool IsEmpty(object[] meta)
    {
        var data = Item.GetMetaOfType<ItemFurnitureData>(meta);
        return data != null && data.furnitureInfo == null;
    }

    /// <summary>Fills flatpack item data with a piece; returns the same array.</summary>
    public static object[] Fill(object[] meta, global::Furniture furniture)
    {
        if (meta == null)
            return null;
        for (int i = 0; i < meta.Length; i++)
        {
            if (meta[i] is ItemFurnitureData)
            {
                var furnitureMeta = new BuildingSystem.FurnitureInfo.Meta();
                meta[i] = new ItemFurnitureData
                {
                    furnitureInfo = new BuildingSystem.FurnitureInfo(furniture, furnitureMeta, global::Furniture.CreateTaskItem(furniture, furnitureMeta), null),
                    ocValue = furniture.PriceOC + furniture.PriceRM * 10,
                };
            }
        }
        return meta;
    }
}
