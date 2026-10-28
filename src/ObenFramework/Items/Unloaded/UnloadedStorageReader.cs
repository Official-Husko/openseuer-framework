// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Newtonsoft.Json.Linq;

namespace ObenFramework.Items;

/// <summary>
/// Reads containers out of a level save file (<c>&lt;scene&gt;.tnmt</c>, plain JSON). A container is an entry
/// of the file's <c>Data</c> list whose data has a <c>storageSlots</c> list. No Unity types, so it is unit
/// tested on its own.
/// </summary>
internal static class UnloadedStorageReader
{
    public static UnloadedContainer Read(string path, string guid, out string refusal)
    {
        string json = Load(path, out refusal);
        return json == null ? null : Parse(json, guid, out refusal);
    }

    public static List<string> Guids(string path, out string refusal)
    {
        string json = Load(path, out refusal);
        return json == null ? new List<string>() : ParseGuids(json, out refusal);
    }

    public static UnloadedContainer Parse(string json, string guid, out string refusal)
    {
        refusal = null;
        JArray data;
        try
        {
            data = JObject.Parse(json)["Data"] as JArray;
        }
        catch (Exception e)
        {
            refusal = "the scene file could not be read: " + e.Message;
            return null;
        }
        if (data == null)
        {
            refusal = "the scene file has no Data list";
            return null;
        }

        JObject storage = null;
        int matches = 0;
        foreach (var token in data)
        {
            if (token is JObject entry && (string)entry["GUID"] == guid && IsStorage(entry))
            {
                storage = (JObject)entry["Data"];
                matches++;
            }
        }
        if (matches == 0)
        {
            refusal = "no container with this id is in the scene file";
            return null;
        }
        if (matches > 1)
        {
            refusal = "more than one container has this id";
            return null;
        }
        return Stacks(storage, guid, out refusal);
    }

    public static List<string> ParseGuids(string json, out string refusal)
    {
        refusal = null;
        var guids = new List<string>();
        var seen = new HashSet<string>();
        try
        {
            if (!(JObject.Parse(json)["Data"] is JArray data))
            {
                refusal = "the scene file has no Data list";
                return guids;
            }
            foreach (var token in data)
            {
                if (token is JObject entry && IsStorage(entry) && (string)entry["GUID"] is string guid && guid.Length > 0 && seen.Add(guid))
                    guids.Add(guid);
            }
        }
        catch (Exception e)
        {
            refusal = "the scene file could not be read: " + e.Message;
        }
        return guids;
    }

    private static string Load(string path, out string refusal)
    {
        refusal = null;
        var info = new FileInfo(path);
        if (!info.Exists)
        {
            refusal = "this save has no file for the scene yet (the player hasn't been there)";
            return null;
        }
        if (info.Length > UnloadedStorageRules.MaxFileBytes)
        {
            refusal = "the scene file is too large to read";
            return null;
        }
        return File.ReadAllText(path, Encoding.UTF8);
    }

    private static UnloadedContainer Stacks(JObject storage, string guid, out string refusal)
    {
        refusal = null;
        var slots = (JArray)storage["storageSlots"];
        var stacks = new List<UnloadedStack>();
        for (int i = 0; i < slots.Count; i++)
        {
            var token = slots[i];
            if (token.Type == JTokenType.Null)
                continue;
            if (!(token is JObject slot))
            {
                refusal = $"slot {i} is not an object";
                return null;
            }
            // A shared object written once and referenced elsewhere; following it isn't worth the risk.
            if (slot["$ref"] != null)
            {
                refusal = $"slot {i} is a reference to another object";
                return null;
            }
            int itemId = Integer(slot["itemId"], -1);
            int amount = Integer(slot["itemAmount"], 0);
            if (itemId != -1 && amount > 0)
                stacks.Add(new UnloadedStack(i, itemId, amount, Integer(slot["ownerId"], -1)));
        }
        return new UnloadedContainer(guid, stacks);
    }

    private static bool IsStorage(JObject entry) => entry["Data"] is JObject body && body["storageSlots"] is JArray;

    private static int Integer(JToken token, int fallback)
    {
        if (token == null)
            return fallback;
        if (token.Type == JTokenType.Integer)
            return (int)token;
        return token.Type == JTokenType.Float ? (int)Math.Round((double)token) : fallback;
    }
}
