// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
using System;
using System.Collections.Generic;
using ObenFramework.Patching;
using ObenFramework.Saves;
using ObenFramework.World;

namespace ObenFramework.Housing;

/// <summary>
/// Tenement renovations: every upgrade line of the building and of each apartment, where each upgrade
/// stands, starting one and paying its final bill. Both go through the contractor the player is talking to,
/// like the upgrade screen, so they pay the same and take the same materials. Refused while the game saves.
/// </summary>
public static class Renovations
{
    /// <summary>A renovation was finished (its final bill paid, or finished by a script). Silent finishes are not raised.</summary>
    public static readonly WorldEvent<RenovationInfo> Finished = new WorldEvent<RenovationInfo>(nameof(Finished));

    /// <summary>
    /// A renovation started, its work was done, or it was finished (then also <see cref="Finished"/>). Upgrades the
    /// story sets up silently are not raised.
    /// </summary>
    public static readonly WorldEvent<RenovationChange> Changed = new WorldEvent<RenovationChange>(nameof(Changed));

    /// <summary>The hooks behind the events are in place (false when a game update broke them).</summary>
    public static bool IsAvailable => PatchAreas.IsAvailable(PatchAreas.Framework, "Housing");

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

    /// <summary>True while the player talks to a contractor, the only time <see cref="Start"/> and <see cref="PayFinal"/> work.</summary>
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
    /// the player can't pay, or while the game saves. With the game's instant upgrades on (a debug switch) it is
    /// installed at once, final bill included, like the screen does.
    /// </summary>
    public static bool Start(string id)
    {
        var entry = SaveGate.IsBusy ? null : RenovationCatalog.Find(id);
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
        {
            progress = entry.Apartment == null ? controller.general.StartUpdate(entry.Upgrade) : entry.Apartment.StartUpdate(entry.Upgrade);
            if (progress != null && controller.completeUpgradesInstantly)
            {
                controller.CompleteUpgradeInstantly(progress);
                progress.FinishUpgrade();
            }
        }
        if (progress == null)
            return false;
        Refresh(controller, entry);
        return true;
    }

    /// <summary>
    /// Pays a finished renovation's final bill (Real Money) at the open contractor, which installs it, like the
    /// upgrade screen. False when no contractor is open, the work isn't done, the player can't pay, or while the
    /// game saves.
    /// </summary>
    public static bool PayFinal(string id)
    {
        var entry = SaveGate.IsBusy ? null : RenovationCatalog.Find(id);
        var controller = TenementController.instance;
        if (entry == null || !ContractorOpen || entry.Info.State != RenovationState.AwaitingFinalPayment || Money.instance == null)
            return false;
        var progress = RenovationCatalog.ProgressOf(entry);
        if (progress == null || (!controller.ignoreUpgradePricesAndItems && !Money.instance.CheckMoney(entry.Upgrade.GetPriceFinalRM(), Money.Currency.RM)))
            return false;
        using (WorldChanges.EnterMod())
            progress.FinishUpgrade();
        Refresh(controller, entry);
        return true;
    }

    // What the upgrade screen tidies after a press. The work already went through, so a failure here only logs.
    private static void Refresh(TenementController controller, RenovationEntry entry)
    {
        try
        {
            controller.RemoveSupersededMarkedUpgrades(entry.Info.Apartment, entry.Upgrade, entry.Line);
            if (controller.upgradePanel != null)
                controller.upgradePanel.CloseUpgradePanel();
            if (controller.blueprintManager != null)
                controller.blueprintManager.UpdateAll();
        }
        catch (Exception e)
        {
            WorldLog.Failed("Renovation screen refresh", e);
        }
    }

    internal static RenovationInfo Describe(TenementUpgradeProgress progress, RenovationState state)
    {
        int number = progress.apartmentUpgrade ? progress.apartmentNumber : 0;
        var upgrade = progress.upgrade;
        return new RenovationInfo(HousingRules.RenovationId(number, upgrade.Id), upgrade.GetName(number), number, upgrade.GetPriceStartOC(),
            upgrade.GetPriceFinalRM(), upgrade.GetRequiredTimeHours(), state);
    }

    private static bool CanPay(RenovationEntry entry, TenementContractor contractor) =>
        Money.instance != null && Money.instance.CheckMoney(entry.Upgrade.GetPriceStartOC()) && entry.Upgrade.CanUpgrade(entry.Apartment, true, contractor);
}
