using System;
using System.Linq;
using DevBridge.Server;
using Newtonsoft.Json.Linq;
using ObenFramework.Player;
using UnityEngine;
using static DevBridge.Commands.PlayerAbilityCommands;

namespace DevBridge.Commands;

/// <summary>Sleeping and waiting, seats, the wardrobe and the player's notes.</summary>
internal static class PlayerRestCommands
{
    [BridgeCommand("player.sleep", "Sleep and wait state, the planned sleep event and how safe the spot is; start a sleep or wait, or wake up.",
        Params = new[] { "action?: sleep, wait or wake", "seconds?: game seconds (sleep: alarm, omit for none; wait: length)", "kind?: wait kind, default Waiting" })]
    private static object Sleep(JObject p)
    {
        string action = p.Str("action", "").ToLowerInvariant();
        bool ok = action switch
        {
            "" => true,
            "sleep" => PlayerSleep.Sleep(p.Int("seconds", -1)),
            "wait" => PlayerSleep.Wait(p.Int("seconds", 3600), ParseEnum<WaitKind>(p.Str("kind", "Waiting"))),
            "wake" => PlayerSleep.WakeUp(),
            _ => throw new ArgumentException($"Unknown action '{action}'. Use sleep, wait or wake."),
        };
        PlayerSleep.CanRest(out string reason);

        return new JObject
        {
            ["done"] = ok,
            ["current"] = PlayerSleep.Current.ToString(),
            ["secondsRested"] = PlayerSleep.SecondsRested,
            ["modWait"] = PlayerSleep.ModWaitRunning,
            ["canRest"] = reason == null,
            ["reason"] = reason,
            ["plannedEvent"] = SleepEvents.Planned.ToString(),
            ["secondsUntilEvent"] = SleepEvents.SecondsUntilPlanned,
            ["safety"] = Round(SleepEvents.SafetyFactor),
            ["safeArea"] = SleepEvents.PlayerInSafeArea,
            ["timePickerOpen"] = TimePicker.IsOpen,
        };
    }

    [BridgeCommand("player.seats", "Seats of the level, nearest first; sit on one or stand up.",
        Params = new[] { "sit?: seat address, GUID or path", "leave?: true stands up", "limit?: how many to list, default 20" })]
    private static object Seats(JObject p)
    {
        if (p["sit"] != null)
        {
            var chair = ObenFramework.Player.Seats.Find(p.Str("sit"));
            if (chair == null)
                throw new ArgumentException($"No seat at '{p.Str("sit")}'.");
            if (!ObenFramework.Player.Seats.Sit(chair, float.PositiveInfinity))
                throw new InvalidOperationException("The player can't sit there now.");
        }
        if (p.Bool("leave", false) && !ObenFramework.Player.Seats.Leave())
            throw new InvalidOperationException("The player isn't seated or can't stand up now.");

        var from = PlayerRef.Position ?? Vector3.zero;
        var seats = ObenFramework.Player.Seats.All()
            .OrderBy(s => Vector3.Distance(from, s.Position))
            .Take(p.Int("limit", 20))
            .Select(s => new JObject
            {
                ["title"] = s.Title,
                ["address"] = s.Address.ToString(),
                ["distance"] = Round(Vector3.Distance(from, s.Position)),
                ["occupied"] = s.Occupied,
                ["byPlayer"] = s.ByPlayer,
                ["toilet"] = s.IsToilet,
            });
        var current = ObenFramework.Player.Seats.Current;
        return new JObject { ["seated"] = current != null ? current.name : null, ["seats"] = new JArray(seats.ToArray()) };
    }

    [BridgeCommand("player.wardrobe", "What the player wears per slot; put an item on from the bag or take one off.",
        Params = new[] { "equip?: item id from the bag", "unequip?: Head, Face, Watch or Backpack" })]
    private static object WardrobeCommand(JObject p)
    {
        if (p["equip"] != null && !Wardrobe.Equip(p.Int("equip", -1)))
            throw new InvalidOperationException("Couldn't put that on (not in the bag, not wearable, or not the player's).");
        if (p["unequip"] != null && !Wardrobe.Unequip(p.Str("unequip")))
            throw new InvalidOperationException("Nothing to take off there, or it isn't the player's.");

        var worn = new JObject();
        foreach (string slot in Wardrobe.Slots)
            worn[slot] = Wardrobe.WornOn(slot);
        return worn;
    }

    [BridgeCommand("player.notes", "The notes and books the player holds; give one by id or asset path.",
        Params = new[] { "give?: note id or asset path (Assets/Content/Notes/...asset)" })]
    private static object Notes(JObject p)
    {
        if (p["give"] != null && !PlayerNotes.Give(p.Str("give")))
            throw new InvalidOperationException("No such note, or the player has it already.");

        var held = PlayerNotes.Held().Select(n => new JObject
        {
            ["id"] = n.Id,
            ["title"] = n.Title,
            ["book"] = n.IsBook,
            ["pages"] = n.IsBook ? $"{n.PagesCollected}/{n.PageCount}" : null,
            ["read"] = n.Read,
            ["category"] = n.Category.ToString(),
        });
        return new JObject { ["held"] = new JArray(held.ToArray()), ["loaded"] = PlayerNotes.Loaded().Count };
    }
}
