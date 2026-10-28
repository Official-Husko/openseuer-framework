using System;
using DevBridge.Server;
using Newtonsoft.Json.Linq;
using ObenFramework.Housing;

namespace DevBridge.Commands;

/// <summary>The tenement's apartments and tenants through ObenFramework.Housing.</summary>
internal static class HousingCommands
{
    [BridgeCommand("housing.apartments", "Every tenement apartment with its tenant, rent and notices, and the building's income.")]
    private static object Apartments(JObject p)
    {
        RequireTenement();
        var list = new JArray();
        foreach (var a in Tenement.Apartments())
            list.Add(Apartment(a));
        return new JObject { ["income"] = Amount(Tenement.TotalIncome), ["rawIncome"] = Amount(Tenement.RawIncome), ["apartments"] = list };
    }

    [BridgeCommand("housing.evict", "Serve or withdraw an eviction notice.",
        Params = new[] { "apartment: number", "cancel?: true withdraws the notice", "hours?: game hours until they leave, default 18" })]
    private static object Evict(JObject p)
    {
        RequireTenement();
        int number = p.Int("apartment", -1);
        bool done = p.Bool("cancel", false) ? Tenement.CancelEviction(number) : Tenement.StartEviction(number, p.Float("hours", 18f));
        var result = Apartment(Tenement.Apartment(number) ?? throw new ArgumentException($"No apartment {number}."));
        result["accepted"] = done;
        return result;
    }

    [BridgeCommand("tenants.list", "Current tenants with their apartments.")]
    private static object TenantsList(JObject p)
    {
        RequireTenement();
        var list = new JArray();
        foreach (var a in Tenement.Apartments())
        {
            if (a.Tenant != null)
                list.Add(new JObject
                {
                    ["npc"] = a.Tenant.NpcId, ["name"] = a.Tenant.Name, ["apartment"] = a.Number, ["movingIn"] = a.MovingIn,
                    ["evictionPending"] = a.EvictionPending, ["rent"] = Amount(a.Rent),
                });
        }
        return new JObject { ["tenants"] = list };
    }

    [BridgeCommand("tenants.check", "Whether an NPC could rent an apartment now, and why not.", Params = new[] { "npc: NPC id", "apartment: number" })]
    private static object TenantsCheck(JObject p)
    {
        RequireTenement();
        int npc = p.Int("npc", -1);
        var profile = Tenants.Profile(npc);
        return new JObject
        {
            ["npc"] = npc, ["name"] = profile?.Name, ["canRent"] = Tenants.CanRent(npc),
            ["refusal"] = Tenants.CheckRent(npc, p.Int("apartment", -1)).ToString(),
        };
    }

    [BridgeCommand("tenants.rent", "Rent an apartment to an NPC without the rent screen.",
        Params = new[] { "npc: NPC id", "apartment: number", "hours?: game hours to move in, default 18", "announce?: play the game's rent scene and take the deposit, default false" })]
    private static object TenantsRent(JObject p)
    {
        RequireTenement();
        int number = p.Int("apartment", -1);
        var refusal = Tenants.Rent(p.Int("npc", -1), number, p.Float("hours", 18f), p.Bool("announce", false));
        var result = new JObject { ["refusal"] = refusal.ToString() };
        var apartment = Tenement.Apartment(number);
        if (apartment != null)
            result["apartment"] = Apartment(apartment);
        return result;
    }

    [BridgeCommand("tenants.event", "Start one of a tenant's events.",
        Params = new[] { "npc: NPC id", "kind: SpawnTrash, PlayMusic, MakeNoise, BreakApartment, Die, SpawnCrazyTeddies or Complain" })]
    private static object TenantsEvent(JObject p)
    {
        RequireTenement();
        var kind = (TenantEventKind)Enum.Parse(typeof(TenantEventKind), p.Str("kind"), true);
        return new JObject { ["started"] = Tenants.TriggerEvent(p.Int("npc", -1), kind) };
    }

    private static JObject Apartment(ApartmentInfo a)
    {
        var o = new JObject
        {
            ["number"] = a.Number, ["floor"] = a.Floor, ["size"] = a.Size.ToString(), ["rent"] = Amount(a.Rent), ["locked"] = a.Locked,
            ["cleaning"] = Cleaning.StateOf(a.Number).ToString(),
        };
        if (a.Tenant != null)
            o["tenant"] = new JObject { ["npc"] = a.Tenant.NpcId, ["name"] = a.Tenant.Name };
        if (a.MovingIn)
            o["moveInHoursLeft"] = Math.Round(a.MoveInHoursLeft, 2);
        if (a.EvictionPending)
            o["evictionHoursLeft"] = Math.Round(a.EvictionHoursLeft, 2);
        if (a.FurnitureOnly)
            o["furnitureOnly"] = true;
        if (a.Deposit.Oc != 0 || a.Deposit.Rm != 0f)
            o["deposit"] = Amount(a.Deposit);
        return o;
    }

    internal static JObject Amount(RentAmount amount) => new JObject { ["oc"] = amount.Oc, ["rm"] = amount.Rm };

    private static void RequireTenement()
    {
        if (!Tenement.IsAvailable)
            throw new InvalidOperationException("There is no tenement here: not in a gameplay scene.");
    }
}
