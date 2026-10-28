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
        Params = new[] { "mod?: only that mod's items, as the save lists them" })]
    private static object RemoveMissing(JObject p) =>
        new JObject { ["removed"] = ItemPlaceholders.RemoveMissing(p.Str("mod", null)) };

    private static JArray Entries(System.Collections.Generic.IEnumerable<ItemManifestEntry> entries)
    {
        var list = new JArray();
        foreach (var entry in entries)
            list.Add(new JObject { ["id"] = entry.Id, ["mod"] = entry.Mod, ["title"] = entry.Title });
        return list;
    }
}
