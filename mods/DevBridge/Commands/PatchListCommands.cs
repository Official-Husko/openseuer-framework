// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
using System;
using System.Collections.Generic;
using System.Linq;
using DevBridge.Server;
using HarmonyLib;
using Newtonsoft.Json.Linq;
using ObenLoader;

namespace DevBridge.Commands;

/// <summary>What is patched and by whom, like the patch dump the reference framework writes when its debug mode starts.</summary>
internal static class PatchListCommands
{
    [BridgeCommand("patches.list", "How many methods each mod (Harmony id) patches; with mod, each of its patched methods and handlers.",
        Params = new[] { "mod?: a mod id or other Harmony id", "method?: only methods whose name contains this" })]
    private static object List(JObject p)
    {
        string owner = p.Str("mod", null);
        string filter = p.Str("method", null);
        var counts = new Dictionary<string, int>();
        var methods = new JArray();
        int patched = 0;

        foreach (var original in Harmony.GetAllPatchedMethods())
        {
            var info = Harmony.GetPatchInfo(original);
            if (info == null || info.Owners.Count == 0)
                continue;
            patched++;
            foreach (string id in info.Owners)
                counts[id] = counts.TryGetValue(id, out int n) ? n + 1 : 1;
            if (owner == null || !info.Owners.Contains(owner))
                continue;
            string name = PatchEvents.Describe(original);
            if (filter != null && name.IndexOf(filter, StringComparison.OrdinalIgnoreCase) < 0)
                continue;
            var row = new JObject { ["method"] = name };
            Add(row, "prefixes", info.Prefixes, owner);
            Add(row, "postfixes", info.Postfixes, owner);
            Add(row, "transpilers", info.Transpilers, owner);
            Add(row, "finalizers", info.Finalizers, owner);
            var others = info.Owners.Where(id => id != owner).ToArray();
            if (others.Length > 0)
                row["alsoPatchedBy"] = new JArray(others);
            methods.Add(row);
        }

        var owners = new JObject();
        foreach (var pair in counts.OrderByDescending(pair => pair.Value))
            owners[pair.Key] = pair.Value;
        var result = new JObject { ["patched"] = patched, ["owners"] = owners };
        if (owner != null)
            result["methods"] = methods;
        return result;
    }

    private static void Add(JObject row, string kind, IEnumerable<Patch> patches, string owner)
    {
        var names = new JArray();
        foreach (var patch in patches)
        {
            if (patch.owner == owner && patch.PatchMethod != null)
                names.Add(patch.PatchMethod.DeclaringType?.Name + "." + patch.PatchMethod.Name);
        }
        if (names.Count > 0)
            row[kind] = names;
    }
}
