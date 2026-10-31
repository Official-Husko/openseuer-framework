using DevBridge.Server;
using Newtonsoft.Json.Linq;
using ObenFramework.Housing;

namespace DevBridge.Commands;

/// <summary>Events of ObenFramework.Housing, recorded for checks in the game.</summary>
internal static class HousingEventCommands
{
    private static readonly EventRecorder Events = new EventRecorder(recorder =>
    {
        var owner = DevBridgeMod.Instance;
        Tenement.TenantChanged.Subscribe(owner, c =>
        {
            var apartment = Tenement.Apartment(c.Apartment);
            recorder.Add("Tenant", new JObject
            {
                ["apartment"] = c.Apartment, ["npc"] = c.Tenant?.NpcId, ["name"] = c.Tenant?.Name, ["kind"] = c.Kind.ToString(),
                ["deposit"] = apartment != null ? HousingCommands.Amount(apartment.Deposit) : null, ["source"] = c.Source.ToString(),
            });
        });
        Tenement.RentPaid.Subscribe(owner, total => recorder.Add("RentPaid", HousingCommands.Amount(total)));
        Tenement.EventStarted.Subscribe(owner, e => recorder.Add("TenantEvent", new JObject
        {
            ["id"] = e.Id, ["kind"] = e.Kind.ToString(), ["npc"] = e.NpcId, ["source"] = e.Source.ToString(),
        }));
        Hotels.RoomChanged.Subscribe(owner, c => recorder.Add("HotelRoom", new JObject
        {
            ["id"] = c.Id, ["rented"] = c.Rented, ["nights"] = c.Nights, ["source"] = c.Source.ToString(),
        }));
        Renovations.Changed.Subscribe(owner, c => recorder.Add("Renovation", new JObject
        {
            ["id"] = c.Info.Id, ["name"] = c.Info.Name, ["kind"] = c.Kind.ToString(), ["source"] = c.Source.ToString(),
        }));
        Renovations.Finished.Subscribe(owner, info => recorder.Add("RenovationFinished", new JObject { ["id"] = info.Id }));
        Cleaning.Changed.Subscribe(owner, c => recorder.Add("Cleaning", new JObject
        {
            ["apartment"] = c.Apartment, ["state"] = c.State.ToString(), ["day"] = c.CompleteDay, ["source"] = c.Source.ToString(),
        }));
        Mailboxes.Used.Subscribe(owner, u => recorder.Add("Mailbox", new JObject
        {
            ["id"] = u.Id, ["mode"] = u.Mode.ToString(), ["count"] = u.Count, ["source"] = u.Source.ToString(),
        }));
        ParcelLockers.Used.Subscribe(owner, u => recorder.Add("Locker", new JObject
        {
            ["id"] = u.Id, ["kind"] = u.Kind.ToString(), ["count"] = u.Count, ["index"] = u.Index, ["parcel"] = u.Parcel?.Title,
            ["source"] = u.Source.ToString(),
        }));
    });

    [BridgeCommand("housing.events", "Tenant, rent, tenant event, hotel, renovation, cleaning, mailbox and parcel locker events since recording started.",
        Params = new[] { "clear?: forget what was returned, default true" })]
    private static object HousingEvents(JObject p) => Events.Read(p.Bool("clear", true));
}
