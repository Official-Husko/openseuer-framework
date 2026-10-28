// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
using System;
using System.Collections.Generic;
using ObenFramework.World;

namespace ObenFramework.Housing;

/// <summary>
/// The player's tenement: its apartments, who lives in them, evictions, rent and upgrade levels. The
/// tenement's state is global (saved once, the same in every level), so this works outside the tenement
/// too. Apartment 1 is the player's own. Renting and tenant events are in <see cref="Tenants"/>,
/// renovations in <see cref="Renovations"/>.
/// </summary>
public static class Tenement
{
    /// <summary>A tenant rented, moved in, got or escaped an eviction notice, or left. Not raised while a save loads.</summary>
    public static readonly WorldEvent<TenantChange> TenantChanged = new WorldEvent<TenantChange>(nameof(TenantChanged));

    /// <summary>The tenants paid their rent (the "Rent paid" moment, by default in the evening of pay day). The payload is the total.</summary>
    public static readonly WorldEvent<RentAmount> RentPaid = new WorldEvent<RentAmount>(nameof(RentPaid));

    /// <summary>A tenant did something in the building: made noise, left trash, broke things, complained.</summary>
    public static readonly WorldEvent<TenantEvent> EventStarted = new WorldEvent<TenantEvent>(nameof(EventStarted));

    public static bool IsAvailable => TenementController.instance != null && TenementController.instance.apartments != null;

    /// <summary>Every apartment, the player's included. Empty without a tenement.</summary>
    public static List<ApartmentInfo> Apartments()
    {
        var list = new List<ApartmentInfo>();
        if (!IsAvailable)
            return list;
        foreach (var apartment in TenementController.instance.apartments)
        {
            if (apartment != null)
                list.Add(Describe(apartment));
        }
        list.Sort((a, b) => a.Number.CompareTo(b.Number));
        return list;
    }

    /// <summary>One apartment, or null.</summary>
    public static ApartmentInfo Apartment(int number)
    {
        var apartment = Find(number);
        return apartment != null ? Describe(apartment) : null;
    }

    /// <summary>
    /// Serves the tenant an eviction notice: they leave after <paramref name="hours"/> game hours (18, like
    /// the game, by default). Refused (false) for the player's apartment, an empty one, a tenant who is still
    /// moving in, one already under notice, and tenants the story keeps.
    /// </summary>
    public static bool StartEviction(int number, float hours = HousingRules.DefaultEvictionHours)
    {
        var apartment = Find(number);
        if (apartment == null || apartment.apartmentNumber == 1 || apartment.resident == null || apartment.resident.disableEvictButton
            || apartment.onlyFurniture || apartment.evictionStarted || apartment.moveStarted)
            return false;

        float clamped = HousingRules.ClampTimerHours(hours);
        var controller = TenementController.instance;
        using (WorldChanges.EnterMod())
        {
            // The game's method also refreshes the blueprint screen, which only exists in some scenes.
            if (controller.blueprintManager != null)
            {
                controller.StartEviction(apartment, clamped);
            }
            else
            {
                apartment.evictionStarted = true;
                apartment.evictionSecondsLeft = (int)(clamped * 3600f);
                TenementPatches.RaiseTenant(apartment, TenantChangeKind.EvictionStarted);
            }
        }
        return apartment.evictionStarted;
    }

    /// <summary>Withdraws an eviction notice. False when there was none.</summary>
    public static bool CancelEviction(int number)
    {
        var apartment = Find(number);
        if (apartment == null || !apartment.evictionStarted)
            return false;
        var controller = TenementController.instance;
        using (WorldChanges.EnterMod())
        {
            if (controller.blueprintManager != null)
            {
                controller.CancelEviction(apartment);
            }
            else
            {
                apartment.evictionStarted = false;
                apartment.evictionSecondsLeft = 0;
                TenementPatches.RaiseTenant(apartment, TenantChangeKind.EvictionCancelled);
            }
        }
        return !apartment.evictionStarted;
    }

    /// <summary>What the tenants pay per payment, after the building's modifiers.</summary>
    public static RentAmount TotalIncome => IsAvailable ? ToAmount(TenementController.instance.GetTotalIncome()) : default;

    /// <summary>What the tenants would pay before the building's modifiers.</summary>
    public static RentAmount RawIncome => IsAvailable ? ToAmount(TenementController.instance.GetTotalRawIncome()) : default;

    /// <summary>The building's level in an upgrade line (electricity, roof, sauna...), or -1 without a tenement.</summary>
    public static int UpgradeLevel(TenementController.GeneralUpgradeCategory category) =>
        IsAvailable ? TenementController.instance.GetGeneralUpgradeLevel(category) : -1;

    /// <summary>An apartment's level in an upgrade line, or -1 when there is no such apartment.</summary>
    public static int UpgradeLevel(int number, TenementController.ApartmentUpgradeCategory category) =>
        Find(number) != null ? TenementController.instance.GetApartmentUpgradeLevelByNumber(number, category) : -1;

    /// <summary>How much of an apartment's left-behind furniture is cleared, in percent, or -1 when there is no such apartment.</summary>
    public static int FurnitureClearPercent(int number)
    {
        var apartment = Find(number);
        return apartment != null ? apartment.FurnitureClearPercent() : -1;
    }

    internal static TenementApartment Find(int number) => IsAvailable ? TenementController.instance.GetApartmentFromNumber(number) : null;

    internal static TenantInfo TenantOf(TenementApartment apartment)
    {
        var resident = apartment != null ? apartment.resident : null;
        if (resident == null || apartment.onlyFurniture || resident.resident == null)
            return null;
        return new TenantInfo(resident.resident.ID, resident.resident.name);
    }

    internal static ApartmentInfo Describe(TenementApartment apartment)
    {
        var info = new ApartmentInfo(apartment.apartmentNumber, apartment.apartmentFloor,
            apartment.type != null ? HousingRules.SizeOf((int)apartment.type.size) : ApartmentSize.Unknown)
        {
            Tenant = TenantOf(apartment),
            MovingIn = apartment.moveStarted,
            MoveInHoursLeft = apartment.moveStarted ? HousingRules.HoursOf(apartment.moveSecondsLeft) : 0f,
            EvictionPending = apartment.evictionStarted,
            EvictionHoursLeft = apartment.evictionStarted ? HousingRules.HoursOf(apartment.evictionSecondsLeft) : 0f,
            FurnitureOnly = apartment.onlyFurniture && apartment.resident != null,
            Rent = new RentAmount(apartment.GetOcIncome(), apartment.GetRmIncome()),
            Deposit = ToAmount(apartment.deposit),
            Locked = apartment.locked,
        };
        var prefabs = apartment.tenementApartmentPrefabs;
        var manager = prefabs != null ? prefabs.furnitureManager : null;
        info.FurnitureManagerGuid = manager != null && !string.IsNullOrEmpty(manager.GUID) ? manager.GUID : null;
        return info;
    }

    internal static RentAmount ToAmount(Money.money money) => new RentAmount(money.OC, money.RM);
}
