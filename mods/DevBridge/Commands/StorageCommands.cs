using System;
using DevBridge.Server;
using Newtonsoft.Json.Linq;
using ObenFramework.Items;

namespace DevBridge.Commands;

/// <summary>Loot tables and containers in scenes that are not loaded.</summary>
internal static class StorageCommands
{
    [BridgeCommand("loot.pools", "Loot categories with their pool sizes, or one category's items with weights.",
        Params = new[] { "category?: name of one category" })]
    private static object Pools(JObject p)
    {
        string category = p.Str("category", null);
        if (category == null)
        {
            var list = new JArray();
            foreach (var c in LootPools.Categories())
                list.Add(new JObject { ["id"] = c.Id, ["name"] = c.Name, ["common"] = c.CommonEntries, ["rare"] = c.RareEntries, ["modded"] = c.Modded });
            return new JObject { ["categories"] = list };
        }

        var entries = new JArray();
        foreach (var e in LootPools.PoolOf(category))
            entries.Add(new JObject { ["itemId"] = e.ItemId, ["title"] = ItemJson.Title(e.ItemId), ["tier"] = e.Tier.ToString(), ["weight"] = e.Weight });
        return new JObject { ["category"] = category, ["entries"] = entries };
    }

    [BridgeCommand("storage.unloaded", "Containers of a scene that is not loaded, read from the loaded save: their ids, or one container's stacks. Also lists queued changes.",
        Params = new[] { "scene?: scene name, e.g. Interior Player Tenement", "id?: container GUID for its stacks" })]
    private static object Unloaded(JObject p)
    {
        var result = new JObject();
        string scene = p.Str("scene", null);
        string id = p.Str("id", null);
        if (scene != null && id == null)
        {
            var ids = UnloadedStorage.ContainerIds(scene, out string refusal);
            result["containers"] = new JArray(ids.ToArray());
            result["refusal"] = refusal;
        }
        else if (scene != null)
        {
            var container = UnloadedStorage.Read(scene, id, out string refusal);
            result["refusal"] = refusal;
            if (container != null)
            {
                var stacks = new JArray();
                foreach (var s in container.Stacks)
                    stacks.Add(new JObject { ["slot"] = s.Slot, ["itemId"] = s.ItemId, ["title"] = ItemJson.Title(s.ItemId), ["amount"] = s.Amount, ["owner"] = s.OwnerId });
                result["stacks"] = stacks;
            }
        }
        result["pending"] = Pending();
        return result;
    }

    [BridgeCommand("storage.queue", "Queue items into or out of a container in a scene that is not loaded; applied when the player enters it.",
        Params = new[] { "scene: scene name", "id: container GUID", "item: item id", "amount?: default 1", "take?: take out instead of put in, default false" })]
    private static object Queue(JObject p)
    {
        var owner = DevBridgeMod.Instance ?? throw new InvalidOperationException("The bridge isn't loaded.");
        string scene = p.Str("scene"), id = p.Str("id");
        int item = p.Int("item", -1), amount = p.Int("amount", 1);
        bool take = p.Bool("take", false);
        bool queued = take ? UnloadedStorage.QueueTake(owner, scene, id, item, amount) : UnloadedStorage.QueueGive(owner, scene, id, item, amount);
        if (!queued)
            throw new InvalidOperationException("Refused; the framework log says why.");
        return new JObject { ["queued"] = true, ["pending"] = Pending() };
    }

    private static JArray Pending()
    {
        var list = new JArray();
        foreach (var c in UnloadedStorage.Pending())
        {
            list.Add(new JObject
            {
                ["mod"] = c.ModId, ["scene"] = c.Scene, ["container"] = c.Guid, ["itemId"] = c.ItemId, ["title"] = ItemJson.Title(c.ItemId),
                ["amount"] = c.Amount, ["give"] = c.Give,
            });
        }
        return list;
    }
}
