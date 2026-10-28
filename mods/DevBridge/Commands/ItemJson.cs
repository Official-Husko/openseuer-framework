using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using ObenFramework.Crafting;
using ObenFramework.Items;
using UnityEngine;

namespace DevBridge.Commands;

/// <summary>JSON shapes shared by the item, crafting and storage commands.</summary>
internal static class ItemJson
{
    public static JArray Vec(Vector3 v) => new JArray(Math.Round(v.x, 2), Math.Round(v.y, 2), Math.Round(v.z, 2));

    public static string Title(int itemId)
    {
        var item = ItemLookup.ById(itemId);
        return item != null ? item.Title : "#" + itemId;
    }

    public static JObject Stack(ItemStackInfo stack)
    {
        var row = new JObject
        {
            ["slot"] = stack.Slot,
            ["itemId"] = stack.ItemId,
            ["title"] = Title(stack.ItemId),
            ["amount"] = stack.Amount,
        };
        if (stack.OwnerId > 0)
            row["owner"] = stack.OwnerId;
        if (stack.Quality >= 0f)
            row["quality"] = Math.Round(stack.Quality, 3);
        if (stack.IntegrityPercent >= 0)
            row["integrity"] = stack.IntegrityPercent;
        if (stack.LiquidItemId >= 0)
        {
            row["liquid"] = Title(stack.LiquidItemId);
            row["liquidAmount"] = Math.Round(stack.LiquidAmount, 3);
        }
        if (stack.IsStolen)
            row["stolen"] = true;
        if (stack.Charge >= 0f)
            row["charge"] = $"{stack.Charge}/{stack.MaxCharge}";
        if (stack.WalletCoins >= 0f)
            row["walletCoins"] = stack.WalletCoins;
        if (stack.IsWritable)
            row["written"] = stack.WrittenTitle;
        return row;
    }

    public static JArray Counts(List<ItemCount> counts)
    {
        var list = new JArray();
        foreach (var count in counts)
            list.Add(new JObject { ["itemId"] = count.ItemId, ["title"] = Title(count.ItemId), ["amount"] = Math.Round(count.Amount, 3) });
        return list;
    }

    public static JObject Station(StationInfo info) => new JObject
    {
        ["id"] = info.Id,
        ["kind"] = info.Kind,
        ["name"] = info.Name,
        ["scene"] = info.Scene,
        ["pos"] = Vec(info.Position),
        ["busy"] = info.Busy,
        ["powered"] = info.Powered,
        ["equipmentQuality"] = Math.Round(info.EquipmentQuality, 3),
        ["recipeTypes"] = new JArray(info.RecipeTypes),
        ["modifiers"] = new JArray(info.Modifiers),
    };

    /// <summary>The id of an owner object as the factor lists show it.</summary>
    public static string Owner(object owner) => owner is ObenLoader.ObenMod mod && mod.Info != null ? mod.Info.Id : owner?.GetType().Name;
}
