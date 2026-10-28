// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
using System;
using System.Collections.Generic;
using HarmonyLib;
using ObenFramework.Events;
using ObenFramework.Items;
using ObenFramework.World;
using ObenLoader;
using ObenLoader.Runtime;

namespace ObenFramework.Transport;

/// <summary>A ticket machine and what it charges.</summary>
public readonly struct TicketMachineInfo
{
    public TicketMachineInfo(InteractableTicketMachine machine, string id, int price, int basePrice, Money.Currency currency, int ticketItemId)
    {
        Machine = machine;
        Id = id;
        Price = price;
        BasePrice = basePrice;
        Currency = currency;
        TicketItemId = ticketItemId;
    }

    public InteractableTicketMachine Machine { get; }
    public string Id { get; }

    /// <summary>What it charges now, mods' rules included.</summary>
    public int Price { get; }

    /// <summary>What the level set it to charge.</summary>
    public int BasePrice { get; }

    public Money.Currency Currency { get; }

    /// <summary>The item it hands out.</summary>
    public int TicketItemId { get; }
}

/// <summary>A ticket was bought or taken out of a machine.</summary>
public readonly struct TicketEvent
{
    public TicketEvent(InteractableTicketMachine machine, bool bought, int price, Money.Currency currency, int ticketItemId)
    {
        Machine = machine;
        Bought = bought;
        Price = price;
        Currency = currency;
        TicketItemId = ticketItemId;
    }

    public InteractableTicketMachine Machine { get; }

    /// <summary>True when paid for, false when the ticket was taken from the tray.</summary>
    public bool Bought { get; }

    public int Price { get; }
    public Money.Currency Currency { get; }
    public int TicketItemId { get; }
    public string Id => TrainTickets.IdOf(Machine);
}

/// <summary>
/// The metro's ticket machines: what they charge, changing it, and hearing tickets bought and taken. Price
/// rules from several mods combine (see <see cref="TicketPriceRules"/>) and are taken back when their mod unloads.
/// </summary>
public static class TrainTickets
{
    internal static readonly AccessTools.FieldRef<InteractableTicketMachine, int> PriceField = AccessTools.FieldRefAccess<InteractableTicketMachine, int>("ticketPrice");
    internal static readonly AccessTools.FieldRef<InteractableTicketMachine, Money.Currency> CurrencyField = AccessTools.FieldRefAccess<InteractableTicketMachine, Money.Currency>("currency");
    internal static readonly AccessTools.FieldRef<InteractableTicketMachine, ItemReference> TicketField = AccessTools.FieldRefAccess<InteractableTicketMachine, ItemReference>("ticket");

    private static readonly List<TicketPriceRule> Rules = new List<TicketPriceRule>();
    private static readonly Dictionary<InteractableTicketMachine, int> BasePrices = new Dictionary<InteractableTicketMachine, int>();

    /// <summary>A ticket was paid for, or taken from the tray.</summary>
    public static readonly WorldEvent<TicketEvent> Changed = new WorldEvent<TicketEvent>(nameof(Changed));

    public static List<InteractableTicketMachine> All() => ObjectIds.Active<InteractableTicketMachine>();

    public static InteractableTicketMachine Find(string id) => ObjectIds.Find<InteractableTicketMachine>(id);

    public static string IdOf(InteractableTicketMachine machine) => ObjectIds.IdOf(machine);

    public static TicketMachineInfo Describe(InteractableTicketMachine machine)
    {
        if (machine == null)
            throw new ArgumentNullException(nameof(machine));
        var ticket = TicketField(machine);
        return new TicketMachineInfo(machine, IdOf(machine), PriceField(machine), BaseOf(machine), CurrencyField(machine), ticket != null ? ticket.ID : 0);
    }

    /// <summary>
    /// Adds a price rule until <paramref name="owner"/> unloads or the result is disposed: a fixed price and/or a
    /// factor, for one machine (by id) or all of them (<c>Machine = null</c>).
    /// </summary>
    public static IDisposable AddRule(ITracker owner, TicketPriceRule rule)
    {
        if (owner == null)
            throw new ArgumentNullException(nameof(owner));
        string problem = TicketPriceRules.Check(rule);
        if (problem != null)
            throw new ArgumentException(problem, nameof(rule));
        var copy = new TicketPriceRule { Machine = rule.Machine, Price = rule.Price, Factor = rule.Factor };
        Rules.Add(copy);
        ApplyAll();
        var registration = Disposable.Create(() =>
        {
            if (Rules.Remove(copy))
                ApplyAll();
        });
        owner.Track(registration);
        return registration;
    }

    /// <summary>Shorthand for a fixed price on every machine (or one).</summary>
    public static IDisposable SetPrice(ITracker owner, int price, string machine = null) =>
        AddRule(owner, new TicketPriceRule { Machine = machine, Price = price });

    internal static void Init(ObenMod framework)
    {
        GameEvents.WorldReady.Subscribe(framework, _ => ApplyAll());
        framework.Track(Disposable.Create(() =>
        {
            Rules.Clear();
            ApplyAll();
            BasePrices.Clear();
        }));
    }

    internal static void ForgetScene() => BasePrices.Clear();

    private static int BaseOf(InteractableTicketMachine machine) =>
        BasePrices.TryGetValue(machine, out int price) ? price : PriceField(machine);

    // Puts every machine at the price the rules give; machines nobody has a rule for keep their own.
    private static void ApplyAll()
    {
        if (Rules.Count == 0 && BasePrices.Count == 0)
            return;
        foreach (var machine in All())
        {
            if (!BasePrices.ContainsKey(machine))
                BasePrices[machine] = PriceField(machine);
            PriceField(machine) = TicketPriceRules.Apply(BasePrices[machine], Rules, IdOf(machine));
        }
    }

    [HarmonyPatch(typeof(InteractableTicketMachine))]
    private static class MachinePatches
    {
        [HarmonyPrefix]
        [HarmonyPatch(nameof(InteractableTicketMachine.BuyTicket))]
        private static void BeforeBuy(bool ___hasTicket, out bool __state) => __state = ___hasTicket;

        [HarmonyPostfix]
        [HarmonyPatch(nameof(InteractableTicketMachine.BuyTicket))]
        private static void AfterBuy(InteractableTicketMachine __instance, bool ___hasTicket, bool __state, int ___ticketPrice, Money.Currency ___currency, ItemReference ___ticket)
        {
            if (!__state && ___hasTicket)
                Raise(new TicketEvent(__instance, true, ___ticketPrice, ___currency, ___ticket != null ? ___ticket.ID : 0));
        }

        [HarmonyPostfix]
        [HarmonyPatch(nameof(InteractableTicketMachine.TakeTicket))]
        private static void AfterTake(InteractableTicketMachine __instance, int ___ticketPrice, Money.Currency ___currency, ItemReference ___ticket) =>
            Raise(new TicketEvent(__instance, false, ___ticketPrice, ___currency, ___ticket != null ? ___ticket.ID : 0));

        private static void Raise(TicketEvent e)
        {
            try
            {
                if (Changed.HasSubscribers && WorldChanges.Active)
                    Changed.Raise(e);
            }
            catch (Exception ex) { TransportModule.Log.Error("Ticket machine hook", ex); }
        }
    }
}
