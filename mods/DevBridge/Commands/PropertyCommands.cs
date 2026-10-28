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
                ["id"] = r.Id, ["hotel"] = r.HotelId, ["index"] = r.Index, ["name"] = r.Name, ["free"] = r.Free, ["rentedByPlayer"] = r.RentedByPlayer,
                ["nightsLeft"] = r.NightsLeft, ["pricePerNight"] = HousingCommands.Amount(r.PricePerNight),
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

    [BridgeCommand("mailboxes.list", "Mailboxes and post boxes of the loaded scenes.")]
    private static object MailboxesList(JObject p)
    {
        var list = new JArray();
        foreach (var m in Mailboxes.All())
            list.Add(new JObject { ["id"] = m.Id, ["name"] = m.Name, ["mode"] = m.Mode.ToString(), ["count"] = m.Count });
        return new JObject { ["mailboxes"] = list };
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
