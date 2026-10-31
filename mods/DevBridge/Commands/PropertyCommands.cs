using System;
using DevBridge.Server;
using Newtonsoft.Json.Linq;
using ObenFramework.Housing;

namespace DevBridge.Commands;

/// <summary>Hotels, renovations, mailboxes, parcel lockers and trash through ObenFramework.Housing.</summary>
internal static class PropertyCommands
{
    [BridgeCommand("hotels.list", "Hotel rooms of the loaded scenes with today's price per night.")]
    private static object HotelsList(JObject p)
    {
        var list = new JArray();
        foreach (var r in Hotels.Rooms())
            list.Add(new JObject
            {
                ["id"] = r.Id, ["hotel"] = r.HotelId, ["index"] = r.Index, ["name"] = r.Name, ["free"] = r.Free, ["bookable"] = r.Bookable,
                ["rentedByPlayer"] = r.RentedByPlayer, ["nightsLeft"] = r.NightsLeft, ["pricePerNight"] = HousingCommands.Amount(r.PricePerNight),
            });
        return new JObject { ["rooms"] = list };
    }

    [BridgeCommand("hotels.rent", "Rent a hotel room for the player, paid from the wallet.", Params = new[] { "id: room id", "nights: 1-7" })]
    private static object HotelsRent(JObject p)
    {
        string id = p.Str("id");
        int nights = p.Int("nights", 1);
        var price = Hotels.Price(id, nights);
        bool rented = Hotels.Rent(id, nights);
        return new JObject { ["rented"] = rented, ["price"] = HousingCommands.Amount(price) };
    }

    [BridgeCommand("renovations.list", "Tenement renovations with their state; apartment 0 is the building.",
        Params = new[] { "apartment?: only this apartment", "state?: Locked, Available, InProgress, AwaitingFinalPayment or Installed" })]
    private static object RenovationsList(JObject p)
    {
        int apartment = p.Int("apartment", -1);
        string state = p.Str("state", null);
        var list = new JArray();
        foreach (var r in Renovations.All())
        {
            if ((apartment >= 0 && r.Apartment != apartment) || (state != null && !string.Equals(r.State.ToString(), state, StringComparison.OrdinalIgnoreCase)))
                continue;
            list.Add(new JObject
            {
                ["id"] = r.Id, ["name"] = r.Name, ["apartment"] = r.Apartment, ["state"] = r.State.ToString(), ["startOc"] = r.StartCostOc,
                ["finalRm"] = r.FinalCostRm, ["hours"] = r.Hours,
            });
        }
        return new JObject { ["contractorOpen"] = Renovations.ContractorOpen, ["renovations"] = list };
    }

    [BridgeCommand("renovations.start", "Order a renovation from the contractor the player is talking to.", Params = new[] { "id: renovation id" })]
    private static object RenovationsStart(JObject p) => Renovation(p.Str("id"), Renovations.Start(p.Str("id")));

    [BridgeCommand("renovations.pay", "Pay a finished renovation's final bill at the open contractor.", Params = new[] { "id: renovation id" })]
    private static object RenovationsPay(JObject p) => Renovation(p.Str("id"), Renovations.PayFinal(p.Str("id")));

    private static JObject Renovation(string id, bool accepted)
    {
        var info = Renovations.Get(id) ?? throw new ArgumentException($"No renovation '{id}'.");
        return new JObject { ["accepted"] = accepted, ["state"] = info.State.ToString(), ["progress"] = Math.Round(Renovations.Progress(id), 3) };
    }

    [BridgeCommand("mailboxes.list", "Mailboxes and post boxes of the loaded scenes.")]
    private static object MailboxesList(JObject p)
    {
        var list = new JArray();
        foreach (var m in Mailboxes.All())
            list.Add(new JObject { ["id"] = m.Id, ["name"] = m.Name, ["mode"] = m.Mode.ToString(), ["count"] = m.Count });
        return new JObject { ["mailboxes"] = list };
    }

    [BridgeCommand("mailboxes.use", "Take the waiting letters from the player's box, or post the player's letters in a post box.",
        Params = new[] { "id: mailbox id", "quiet?: take without the game's toasts" })]
    private static object MailboxesUse(JObject p)
    {
        var box = Mailboxes.Find(p.Str("id")) ?? throw new ArgumentException($"No mailbox '{p.Str("id")}'.");
        int moved = box.mode == Mailbox.Mode.Send ? Mailboxes.Post(box) : Mailboxes.TakeMail(box, !p.Bool("quiet", false));
        return new JObject { ["moved"] = moved, ["left"] = Mailboxes.Describe(box).Count };
    }

    [BridgeCommand("lockers.use", "Claim the waiting parcels into a locker, or take one out.",
        Params = new[] { "id: locker id", "take?: compartment to take a parcel from", "quiet?: take without the game's toast" })]
    private static object LockersUse(JObject p)
    {
        var locker = ParcelLockers.Find(p.Str("id")) ?? throw new ArgumentException($"No parcel locker '{p.Str("id")}'.");
        int index = p.Int("take", -1);
        if (index >= 0)
            return new JObject { ["taken"] = ParcelLockers.TakeParcel(locker, index, !p.Bool("quiet", false)) };
        return new JObject { ["claimed"] = ParcelLockers.Open(locker) };
    }

    [BridgeCommand("lockers.list", "Parcel lockers and what is in their compartments.")]
    private static object LockersList(JObject p)
    {
        var list = new JArray();
        foreach (var locker in ParcelLockers.All())
        {
            var slots = new JArray();
            foreach (var s in locker.Slots)
                slots.Add(new JObject { ["index"] = s.Index, ["open"] = s.Open, ["parcel"] = s.Parcel?.Title });
            list.Add(new JObject { ["id"] = locker.Id, ["name"] = locker.Name, ["slots"] = slots });
        }
        return new JObject { ["lockers"] = list };
    }

    [BridgeCommand("trash.list", "Trash piles of the loaded scenes and what they are made of.")]
    private static object TrashList(JObject p)
    {
        var list = new JArray();
        foreach (var t in Cleaning.TrashPiles())
        {
            var items = new JArray();
            foreach (var (itemId, amount) in t.Items)
                items.Add(new JObject { ["item"] = itemId, ["amount"] = amount });
            list.Add(new JObject { ["id"] = t.Id, ["pos"] = new JArray(t.Position.x, t.Position.y, t.Position.z), ["items"] = items });
        }
        return new JObject { ["piles"] = list };
    }
}
