// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
using System;
using System.Collections.Generic;
using ObenFramework.Economy;
using ObenFramework.Events;
using ObenFramework.Saves;
using Currency = Money.Currency;

namespace ObenFramework.Player;

/// <summary>A treatment a doctor in the loaded level sells, from <see cref="Medical.Treatments"/>.</summary>
public sealed class TreatmentInfo
{
    internal TreatmentInfo(MedicalTreatment kind, DoctorPanelSelection.Treatment treatment, bool offered)
    {
        Kind = kind;
        Title = treatment.title;
        Price = treatment.price;
        Currency = treatment.currency;
        Offered = offered;
    }

    public MedicalTreatment Kind { get; }
    public string Title { get; }
    public int Price { get; }
    public Currency Currency { get; }

    /// <summary>The doctor would sell it to the player right now (the player's state calls for it).</summary>
    public bool Offered { get; }
}

/// <summary>A treatment the player bought, from <see cref="Medical.Treated"/>.</summary>
public readonly struct Treated
{
    public Treated(MedicalTreatment kind, int price, Currency currency)
    {
        Kind = kind;
        Price = price;
        Currency = currency;
    }

    public MedicalTreatment Kind { get; }
    public int Price { get; }
    public Currency Currency { get; }
}

/// <summary>
/// Doctors: what they sell, buying a treatment while the doctor's menu is open, and an event for every
/// treatment bought. Buying goes through the game's own purchase, so it costs the same and does the same.
/// </summary>
public static class Medical
{
    /// <summary>The player bought a treatment (through the menu or <see cref="Treat"/>).</summary>
    public static readonly GameEvent<Treated> Treated = new GameEvent<Treated>(nameof(Treated));

    /// <summary>The doctor's menu is open.</summary>
    public static bool MenuOpen
    {
        get
        {
            var ui = GameUIController.instance;
            var controller = DoctorController.instance;
            var panel = DoctorPanel.instance;
            return ui != null && ui.IsMenuVisible && ui.currentPanel == MenuPanels.Doctor &&
                   controller != null && controller.currentDoctor != null &&
                   panel != null && panel.isActiveAndEnabled && PlayerStats.instance != null;
        }
    }

    /// <summary>
    /// The treatments the doctors of the loaded level sell, each once. With the menu open, only the
    /// current doctor's. Scans the level, so call it on demand.
    /// </summary>
    public static List<TreatmentInfo> Treatments()
    {
        var result = new List<TreatmentInfo>();
        var doctors = Doctors();
        foreach (MedicalTreatment kind in Enum.GetValues(typeof(MedicalTreatment)))
        {
            if (MedicalSlots.Find(kind, doctors, out var page, out var treatment))
                result.Add(new TreatmentInfo(kind, treatment, Offered(page, treatment)));
        }
        return result;
    }

    /// <summary>The menu is open, the doctor offers this treatment now and the player can pay for it.</summary>
    public static bool CanTreat(MedicalTreatment kind) => Ready(kind, out _, out _);

    /// <summary>Buys a treatment from the doctor whose menu is open. False when <see cref="CanTreat"/> is false.</summary>
    public static bool Treat(MedicalTreatment kind)
    {
        if (SaveGate.IsBusy || !Ready(kind, out var page, out var treatment))
            return false;
        return page.BuyTreatment(treatment);
    }

    internal static void Bought(DoctorPanelSelection page, DoctorPanelSelection.Treatment treatment)
    {
        if (MedicalSlots.KindOf(page, treatment, out var kind))
            Treated.Raise(new Treated(kind, treatment.price, treatment.currency), GameEvents.Log);
    }

    private static bool Ready(MedicalTreatment kind, out DoctorPanelSelection page, out DoctorPanelSelection.Treatment treatment)
    {
        page = null;
        treatment = null;
        if (!MenuOpen || !Enum.IsDefined(typeof(MedicalTreatment), kind))
            return false;
        var doctor = new[] { DoctorController.instance.currentDoctor };
        return MedicalSlots.Find(kind, doctor, out page, out treatment) && Offered(page, treatment) &&
               Wallet.CanAfford(treatment.currency, treatment.price);
    }

    private static IEnumerable<Doctor> Doctors()
    {
        if (MenuOpen)
            return new[] { DoctorController.instance.currentDoctor };
        return MedicalSlots.Doctors();
    }

    private static bool Offered(DoctorPanelSelection page, DoctorPanelSelection.Treatment treatment)
    {
        var available = page.GetAvailableTreatments();
        return available != null && available.Contains(treatment);
    }
}
