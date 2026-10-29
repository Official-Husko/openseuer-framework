using DevBridge.Server;
using Newtonsoft.Json.Linq;
using ObenFramework.Items;

namespace DevBridge.Commands;

/// <summary>Items mods register, and the stand-ins for mod items whose mod is gone.</summary>
internal static class RegistryCommands
{
    [BridgeCommand("registry.items", "Items registered by mods, with the mod that added each.")]
    private static object Items(JObject p)
    {
        var list = new JArray();
        foreach (var item in ItemRegistry.Items)
            list.Add(new JObject { ["id"] = item.ID, ["title"] = item.Title, ["mod"] = ItemRegistry.OwnerOf(item.ID) });
        return list;
    }

    [BridgeCommand("registry.placeholders", "Missing mod items: stand-ins handed out this session and the ones the loaded save lists without their mod.")]
    private static object Placeholders(JObject p) => new JObject
    {
        ["available"] = ItemPlaceholders.IsAvailable,
        ["active"] = Entries(ItemPlaceholders.Active()),
        ["missingInSave"] = Entries(ItemPlaceholders.MissingInSave()),
    };

    [BridgeCommand("registry.removemissing", "Delete stacks of missing mod items from the inventory and loaded containers (for good).",
        Params = new[] { "mod?: that mod's items, as the save lists them", "all?: true for every mod's (mod or all is required)" })]
    private static object RemoveMissing(JObject p)
    {
        string mod = p.Str("mod", null);
        if (mod == null && !p.Bool("all", false))
            throw new System.ArgumentException("Give mod=<id>, or all=true to delete every mod's missing items.");
        return new JObject { ["removed"] = mod != null ? ItemPlaceholders.RemoveMissing(mod) : ItemPlaceholders.RemoveAllMissing() };
    }

    [BridgeCommand("registry.missing", "Stacks of missing mod items in the inventory and loaded containers, with mod, amount and place. Changes nothing.")]
    private static object Missing(JObject p)
    {
        var list = new JArray();
        foreach (var stack in ItemPlaceholders.Scan())
            list.Add(new JObject { ["id"] = stack.ItemId, ["mod"] = stack.Mod, ["title"] = stack.Title, ["amount"] = stack.Amount, ["where"] = stack.Where });
        return new JObject { ["stacks"] = list };
    }

    private static JArray Entries(System.Collections.Generic.IEnumerable<ItemManifestEntry> entries)
    {
        var list = new JArray();
        foreach (var entry in entries)
            list.Add(new JObject { ["id"] = entry.Id, ["mod"] = entry.Mod, ["title"] = entry.Title });
        return list;
    }
}
