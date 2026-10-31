// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
using System;
using HarmonyLib;
using ObenFramework.Saves;
using OS.Items;

namespace ObenFramework.Env;

/// <summary>
/// The player's radiation: the dose taken so far, what the surroundings give per hour, and the dosimeter in
/// hand. The game adds the measured rate to the dose once a game minute and lets it fade slowly; above the
/// damage threshold the dose hurts. Rates are in mSv per hour, doses in mSv.
/// </summary>
public static class Radiation
{
    private static readonly AccessTools.FieldRef<Dosimeter, bool> DosimeterSwitch = AccessTools.FieldRefAccess<Dosimeter, bool>("on");
    private static readonly AccessTools.FieldRef<TriggerRadiation, bool> PlayerInside = AccessTools.FieldRefAccess<TriggerRadiation, bool>("playerIn");

    /// <summary>True when the player and the radiation system exist (any gameplay scene).</summary>
    public static bool IsAvailable => PlayerStats.instance != null && PlayerStats.instance.radiation != null && RadiationController.instance != null;

    /// <summary>The dose the player carries now; 0 without a player.</summary>
    public static float Dose => PlayerStats.instance != null && PlayerStats.instance.radiation != null ? PlayerStats.instance.radiation.Dose : 0f;

    /// <summary>The dose above which radiation starts to hurt (1000 by default).</summary>
    public static float DamageThreshold => PlayerStats.instance != null && PlayerStats.instance.radiation != null ? PlayerStats.instance.radiation.DoseDamageThreshold : 0f;

    /// <summary>Sets the player's dose (0 or more). False without a player, or while a save is written or loaded.</summary>
    public static bool SetDose(float dose)
    {
        if (float.IsNaN(dose) || float.IsInfinity(dose))
            throw new ArgumentException("Give a finite dose.", nameof(dose));
        var stats = PlayerStats.instance != null ? PlayerStats.instance.radiation : null;
        if (stats == null || SaveGate.IsBusy)
            return false;
        stats.Dose = Math.Max(0f, dose);
        return true;
    }

    /// <summary>Adds to (or with a negative amount takes from) the player's dose; it never goes below 0.</summary>
    public static bool AddDose(float amount) => SetDose(Dose + amount);

    /// <summary>The background level of the scene: tiny normally, high while the wasteland effect is on.</summary>
    public static float Background => RadiationController.instance != null && SMVEffects.instance != null ? RadiationController.instance.GetBackgroundRadiation() : 0f;

    /// <summary>The wasteland effect (the SMV world) is active, with its high background radiation.</summary>
    public static bool IsWasteland => SMVEffects.instance != null && SMVEffects.instance.IsWastelandIsActive();

    /// <summary>
    /// What the player's head receives right now: background, radiation sources in range, and the radiation
    /// zones the player stands in. Zones made to never hurt stop adding once the dose reaches the damage
    /// threshold, as in the game. Measures on every call.
    /// </summary>
    public static float RateHere
    {
        get
        {
            var controller = RadiationController.instance;
            var player = PlayerLocator.instance;
            if (controller == null || player == null || player.playerHead == null || SMVEffects.instance == null)
                return 0f;
            float rate = controller.MeasureRadiation(player.playerHead);
            bool atThreshold = Dose >= DamageThreshold;
            foreach (var zone in RadiationController.allTriggers)
            {
                if (zone != null && zone.enabled && zone.Enabled && PlayerInside(zone) && !(zone.dontDamagePlayer && atThreshold))
                    rate += zone.radiation;
            }
            return rate;
        }
    }

    /// <summary>The player holds a dosimeter in either hand.</summary>
    public static bool DosimeterEquipped => HeldDosimeter() != null;

    /// <summary>The held dosimeter is switched on and has charge; null without one.</summary>
    public static bool? DosimeterOn
    {
        get
        {
            var dosimeter = HeldDosimeter();
            if (dosimeter == null)
                return null;
            var ammo = AmmoOf(dosimeter);
            return DosimeterSwitch(dosimeter) && ammo != null && ammo.ammo > 0f;
        }
    }

    /// <summary>The held dosimeter's battery, 0 to 1; null without one.</summary>
    public static float? DosimeterCharge
    {
        get
        {
            var dosimeter = HeldDosimeter();
            var ammo = dosimeter != null ? AmmoOf(dosimeter) : null;
            if (ammo == null || ammo.maxAmmo <= 0f)
                return null;
            return ammo.ammo / ammo.maxAmmo;
        }
    }

    private static Dosimeter HeldDosimeter()
    {
        var hands = PlayerHandItems.instance;
        if (hands == null)
            return null;
        if (hands.righthandItem is Dosimeter right && right != null && right.equipped)
            return right;
        if (hands.lefthandItem is Dosimeter left && left != null && left.equipped)
            return left;
        return null;
    }

    private static ItemAmmodata AmmoOf(Dosimeter dosimeter) =>
        dosimeter.meta != null ? ItemMetaUtilities.GetMetaOfType<ItemAmmodata>(dosimeter.meta) : null;
}
