// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
using System;
using System.Collections.Generic;
using HarmonyLib;
using ObenFramework.World;

namespace ObenFramework.Housing;

public enum RenovationState
{
    /// <summary>Needs another upgrade first, or another upgrade of its line is under way.</summary>
    Locked,

    /// <summary>Can be ordered from a contractor.</summary>
    Available,

    InProgress,

    /// <summary>The work is done and waits for the final bill.</summary>
    AwaitingFinalPayment,

    Installed,
}

/// <summary>A snapshot of one renovation (an upgrade of the building or of one apartment).</summary>
public sealed class RenovationInfo
{
    internal RenovationInfo(string id, string name, int apartment, int startCostOc, int finalCostRm, int hours, RenovationState state)
    {
        Id = id;
        Name = name;
        Apartment = apartment;
        StartCostOc = startCostOc;
        FinalCostRm = finalCostRm;
        Hours = hours;
        State = state;
    }

    /// <summary><c>{apartment}/{upgrade id}</c>; apartment 0 is the building.</summary>
    public string Id { get; }

    public string Name { get; }

    /// <summary>The apartment it is for, 0 for the building.</summary>
    public int Apartment { get; }

    /// <summary>Open Sewer Coin paid when the work starts.</summary>
    public int StartCostOc { get; }

    /// <summary>Real Money paid on the final bill.</summary>
    public int FinalCostRm { get; }

    /// <summary>Game hours the work takes.</summary>
    public int Hours { get; }

    public RenovationState State { get; }
}

/// <summary>
/// Tenement renovations: every upgrade line of the building and of each apartment, where each upgrade
/// stands, and starting one. Starting goes through the contractor the player is talking to, like the
/// upgrade screen, so it pays the same and takes the same materials.
/// </summary>
public static class Renovations
{
    /// <summary>A renovation was finished (its final bill paid, or finished by a script). Silent finishes are not raised.</summary>
    public static readonly WorldEvent<RenovationInfo> Finished = new WorldEvent<RenovationInfo>(nameof(Finished));

    public static List<RenovationInfo> All()
    {
        var list = new List<RenovationInfo>();
        foreach (var entry in RenovationCatalog.Build())
            list.Add(entry.Info);
        return list;
    }

    public static RenovationInfo Get(string id) => RenovationCatalog.Find(id)?.Info;

    /// <summary>How far the work is, 0 to 1 (1 once done or installed, 0 when not started).</summary>
    public static float Progress(string id)
    {
        var entry = RenovationCatalog.Find(id);
        if (entry == null)
            return 0f;
        switch (entry.Info.State)
        {
            case RenovationState.Installed:
            case RenovationState.AwaitingFinalPayment:
                return 1f;
            case RenovationState.InProgress:
                var progress = RenovationCatalog.ProgressOf(entry);
                return progress != null ? Math.Min(1f, Math.Max(0f, progress.GetCurrentProgress())) : 0f;
            default:
                return 0f;
        }
    }

    /// <summary>True while the player talks to a contractor, the only time <see cref="Start"/> works.</summary>
    public static bool ContractorOpen
    {
        get
        {
            var controller = TenementController.instance;
            return controller != null && controller.contractorOpen && controller.currentContractor != null;
        }
    }

    /// <summary>
    /// Orders a renovation from the open contractor, paying and taking materials like the upgrade screen.
    /// False when no contractor is open, the renovation is not available, the contractor does not do it,
    /// or the player can't pay.
    /// </summary>
    public static bool Start(string id)
    {
        var entry = RenovationCatalog.Find(id);
        var controller = TenementController.instance;
        if (entry == null || !ContractorOpen || entry.Info.State != RenovationState.Available)
            return false;
        var contractor = controller.currentContractor;
        var size = entry.Apartment != null && entry.Apartment.type != null ? (int)entry.Apartment.type.size : (int)UpgradeType.ApartmentType.Large;
        if (!contractor.CanUpgrade(entry.Upgrade, (UpgradeType.ApartmentType)size))
            return false;
        if (!controller.ignoreUpgradePricesAndItems && !CanPay(entry, contractor))
            return false;

        TenementUpgradeProgress progress;
        using (WorldChanges.EnterMod())
            progress = entry.Apartment == null ? controller.general.StartUpdate(entry.Upgrade) : entry.Apartment.StartUpdate(entry.Upgrade);
        if (progress == null)
            return false;
        controller.RemoveSupersededMarkedUpgrades(entry.Info.Apartment, entry.Upgrade, entry.Line);
        if (controller.upgradePanel != null)
            controller.upgradePanel.CloseUpgradePanel();
        if (controller.blueprintManager != null)
            controller.blueprintManager.UpdateAll();
        return true;
    }

    internal static RenovationInfo Describe(TenementUpgradeProgress progress)
    {
        int number = progress.apartmentUpgrade ? progress.apartmentNumber : 0;
        var upgrade = progress.upgrade;
        return new RenovationInfo(HousingRules.RenovationId(number, upgrade.Id), upgrade.GetName(number), number, upgrade.GetPriceStartOC(),
            upgrade.GetPriceFinalRM(), upgrade.GetRequiredTimeHours(), RenovationState.Installed);
    }

    private static bool CanPay(RenovationEntry entry, TenementContractor contractor) =>
        Money.instance != null && Money.instance.CheckMoney(entry.Upgrade.GetPriceStartOC()) && entry.Upgrade.CanUpgrade(entry.Apartment, true, contractor);
}

[HarmonyPatch(typeof(TenementUpgradeProgress), nameof(TenementUpgradeProgress.FinishUpgrade))]
internal static class RenovationFinishPatch
{
    [HarmonyPostfix]
    private static void After(TenementUpgradeProgress __instance, bool silentUpgrade)
    {
        if (silentUpgrade || __instance == null || __instance.upgrade == null || !Renovations.Finished.HasSubscribers || !WorldChanges.Active)
            return;
        try
        {
            Renovations.Finished.Raise(Renovations.Describe(__instance));
        }
        catch (Exception e)
        {
            WorldLog.Failed("Renovation hook", e);
        }
    }
}
