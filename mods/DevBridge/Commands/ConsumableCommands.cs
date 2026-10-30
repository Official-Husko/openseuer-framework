using System;
using System.Collections.Generic;
using DevBridge.Server;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using ObenFramework.Items;

namespace DevBridge.Commands;

/// <summary>Reading and changing what consumable items do, for the session or kept in the save (owned by DevBridge).</summary>
internal static class ConsumableCommands
{
    private static readonly Dictionary<int, IDisposable> SessionValues = new Dictionary<int, IDisposable>();

    [BridgeCommand("items.consumable", "Read a consumable item's values (now, the game's own, DevBridge's per-save), or change them.",
        Params = new[] { "id", "set?: values to change, e.g. {\"Food\":40}; the rest stay as they are now",
            "save?: keep the change in the loaded save instead of the session, default false",
            "clear?: drop DevBridge's session and per-save values of the item, default false" })]
    private static object Consumable(JObject p)
    {
        int id = p.Int("id", -1);
        if (ItemConsumables.Get(id) == null)
            throw new ArgumentException($"Item {id} isn't consumable (or doesn't exist).");

        var result = new JObject { ["id"] = id };
        if (p.Bool("clear", false))
        {
            bool session = SessionValues.TryGetValue(id, out var held);
            held?.Dispose();
            SessionValues.Remove(id);
            result["cleared"] = new JObject { ["session"] = session, ["save"] = ItemConsumables.ClearForSave(DevBridgeMod.Instance, id) };
        }

        if (p["set"] is JObject changes)
        {
            var values = ItemConsumables.Get(id);
            JsonConvert.PopulateObject(changes.ToString(), values);
            if (p.Bool("save", false))
            {
                result["set"] = ItemConsumables.SetForSave(DevBridgeMod.Instance, id, values);
            }
            else
            {
                if (SessionValues.TryGetValue(id, out var old))
                    old.Dispose();
                var registration = ItemConsumables.Set(DevBridgeMod.Instance, id, values);
                SessionValues[id] = registration;
                result["set"] = registration != null;
            }
        }

        result["now"] = JObject.FromObject(ItemConsumables.Get(id));
        result["original"] = JObject.FromObject(ItemConsumables.Original(id));
        var saved = ItemConsumables.SavedValues(DevBridgeMod.Instance.Info.Id, id);
        result["saved"] = saved != null ? JObject.FromObject(saved) : null;
        return result;
    }
}
