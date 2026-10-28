using System;
using DevBridge.Server;
using Newtonsoft.Json.Linq;
using ObenFramework.Items;

namespace DevBridge.Commands;

/// <summary>The player's inventory in detail: stacks, held items, equipping and the inventory events.</summary>
internal static class InventoryCommands
{
    private static readonly EventRecorder Events = new EventRecorder(recorder =>
    {
        var owner = DevBridgeMod.Instance;
        InventoryEvents.ItemAdded.Subscribe(owner, c => recorder.Add("ItemAdded", Change(c)));
        InventoryEvents.ItemRemoved.Subscribe(owner, c => recorder.Add("ItemRemoved", Change(c)));
        InventoryEvents.HeldChanged.Subscribe(owner, c => recorder.Add("HeldChanged", new JObject
        {
            ["hand"] = c.LeftHand ? "secondary" : "primary",
            ["itemId"] = c.ItemId,
            ["title"] = c.ItemId >= 0 ? ItemJson.Title(c.ItemId) : null,
            ["previousItemId"] = c.PreviousItemId,
        }));
        TaskItems.Changed.Subscribe(owner, c => recorder.Add("TaskItemChanged", new JObject
        {
            ["id"] = c.Id,
            ["name"] = c.Name,
            ["type"] = c.Type,
            ["old"] = c.OldAmount,
            ["new"] = c.NewAmount,
            ["held"] = c.Held,
        }));
    });

    [BridgeCommand("inventory.stacks", "Every stack the player carries with quality, liquid, owner and other details, plus held items and free slots.")]
    private static object Stacks(JObject p)
    {
        var stacks = new JArray();
        foreach (var stack in PlayerItems.Stacks())
            stacks.Add(ItemJson.Stack(stack));
        return new JObject
        {
            ["ready"] = PlayerItems.IsReady,
            ["freeSlots"] = PlayerItems.FreeSlots,
            ["capacity"] = PlayerItems.Capacity,
            ["primary"] = Held(HeldItems.Primary),
            ["secondary"] = Held(HeldItems.Secondary),
            ["stacks"] = stacks,
        };
    }

    [BridgeCommand("inventory.events", "Inventory events (ItemAdded, ItemRemoved, HeldChanged, TaskItemChanged) since recording started on the first call.",
        Params = new[] { "clear?: forget what was returned, default true" })]
    private static object EventsList(JObject p) => Events.Read(p.Bool("clear", true));

    [BridgeCommand("inventory.equip", "Equip an item from the inventory grid into its hand slot, or put a hand's item away.",
        Params = new[] { "id?: item to equip", "unequip?: primary or secondary" })]
    private static object Equip(JObject p)
    {
        string hand = p.Str("unequip", null);
        bool done;
        if (hand != null)
        {
            if (hand != "primary" && hand != "secondary")
                throw new ArgumentException("unequip must be primary or secondary.");
            done = HeldItems.Unequip(hand == "secondary");
        }
        else
        {
            done = HeldItems.Equip(p.Int("id", -1));
        }
        return new JObject { ["done"] = done, ["primary"] = Held(HeldItems.Primary), ["secondary"] = Held(HeldItems.Secondary) };
    }

    [BridgeCommand("inventory.liquid", "How much of a liquid the player carries in containers, and optionally take some (all or nothing).",
        Params = new[] { "id: liquid item id", "take?: amount to take" })]
    private static object Liquid(JObject p)
    {
        int id = p.Int("id", -1);
        float take = p.Float("take", 0f);
        bool taken = take > 0f && StackActions.TakeLiquid(id, take);
        return new JObject { ["liquid"] = ItemJson.Title(id), ["amount"] = Math.Round(StackActions.LiquidAmount(id), 3), ["taken"] = taken };
    }

    private static JToken Held(int itemId) =>
        itemId < 0 ? (JToken)JValue.CreateNull() : new JObject { ["itemId"] = itemId, ["title"] = ItemJson.Title(itemId) };

    private static JObject Change(InventoryChange c) => new JObject
    {
        ["itemId"] = c.ItemId,
        ["title"] = ItemJson.Title(c.ItemId),
        ["amount"] = c.Amount,
        ["total"] = c.Total,
        ["source"] = c.Source.ToString(),
    };
}
