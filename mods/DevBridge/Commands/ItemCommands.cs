using System;
using System.Collections.Generic;
using System.Linq;
using DevBridge.Server;
using Newtonsoft.Json.Linq;
using ObenFramework.Items;
using UnityEngine;

namespace DevBridge.Commands;

/// <summary>Find, give, count and spawn items.</summary>
internal static class ItemCommands
{
    [BridgeCommand("items.find", "Find items by id or by part of the title.",
        Params = new[] { "query: id or title text", "max?: default 20" })]
    private static object Find(JObject p)
    {
        string query = p.Str("query").Trim();
        int max = Math.Max(1, p.Int("max", 20));

        List<Item> found;
        if (int.TryParse(query, out int id))
        {
            var item = ItemLookup.ById(id);
            found = item != null ? new List<Item> { item } : new List<Item>();
        }
        else
        {
            found = ItemLookup.Search(query, max);
        }
        return new JArray(found.Select(Describe).ToArray());
    }

    [BridgeCommand("items.give", "Give the player items. Overflow drops at their feet; ids 0 and 1 add money.",
        Params = new[] { "id", "amount?: default 1" })]
    private static object Give(JObject p)
    {
        var item = Require(p.Int("id", -1));
        int amount = p.Int("amount", 1);
        bool allFit = PlayerItems.Give(item, amount);
        return new JObject
        {
            ["item"] = item.Title,
            ["amount"] = amount,
            ["allFit"] = allFit,
            ["count"] = PlayerItems.Count(item.ID),
        };
    }

    [BridgeCommand("items.count", "How many of an item the player has.", Params = new[] { "id" })]
    private static object Count(JObject p)
    {
        var item = Require(p.Int("id", -1));
        return new JObject { ["item"] = item.Title, ["count"] = PlayerItems.Count(item.ID) };
    }

    [BridgeCommand("items.spawn", "Spawn a pickup in the world, at pos or in front of the player.",
        Params = new[] { "id", "amount?: default 1", "pos?: [x,y,z] or x/y/z" })]
    private static object Spawn(JObject p)
    {
        var item = Require(p.Int("id", -1));
        int amount = p.Int("amount", 1);
        Vector3? pos = p.Position();
        var spawned = pos.HasValue ? WorldItems.Spawn(item, amount, pos.Value) : WorldItems.SpawnAtPlayer(item, amount);
        if (spawned == null)
            throw new InvalidOperationException($"Couldn't spawn {item.Title}. Check the log for the reason.");
        return new JObject
        {
            ["item"] = item.Title,
            ["amount"] = amount,
            ["object"] = "#" + spawned.GetInstanceID(),
            ["pos"] = new JArray(spawned.transform.position.x, spawned.transform.position.y, spawned.transform.position.z),
        };
    }

    private static Item Require(int id) =>
        ItemLookup.ById(id) ?? throw new ArgumentException($"No item with id {id}. Try items.find.");

    private static JObject Describe(Item item) => new JObject
    {
        ["id"] = item.ID,
        ["title"] = item.Title,
        ["categories"] = new JArray(item.Categories ?? Array.Empty<string>()),
        ["value"] = item.Value,
        ["stackable"] = item.Stackable,
        ["modded"] = ItemRegistry.IsModded(item.ID),
    };
}
