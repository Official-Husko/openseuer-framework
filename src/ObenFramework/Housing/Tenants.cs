// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
using System;
using NPC;
using ObenFramework.World;

namespace ObenFramework.Housing;

/// <summary>Why a tenant can't rent an apartment, as answered by <see cref="Tenants.CheckRent"/>.</summary>
public enum RentRefusal
{
    None,

    /// <summary>The NPC has no tenancy profile: they never rent.</summary>
    NotATenant,

    /// <summary>The tenant (or a relative) already rents in the building.</summary>
    AlreadyRenting,

    NoSuchApartment,

    /// <summary>Apartment 1 is the player's.</summary>
    PlayersApartment,

    /// <summary>Someone lives there or is moving in.</summary>
    Occupied,

    /// <summary>The tenant wants a different size.</summary>
    WrongSize,

    /// <summary>The apartment's upgrades are below the tenant's standards.</summary>
    NotGoodEnough,

    /// <summary>The building itself is below the tenant's standards.</summary>
    BuildingNotGoodEnough,

    /// <summary>Renting needs the tenement's blueprint screen, which only exists in the tenement scenes, or the rent screen is open.</summary>
    NotHere,
}

/// <summary>
/// Tenants: who can rent, renting an apartment to an NPC directly (no rent screen), and starting one of
/// a tenant's events. Changes are reported through <see cref="Tenement.TenantChanged"/>.
/// </summary>
public static class Tenants
{
    /// <summary>The NPC's tenancy profile, or null when they are not a tenant.</summary>
    public static TenantInfo Profile(int npcId)
    {
        var resident = ResidentOf(npcId);
        return resident != null ? new TenantInfo(resident.resident.ID, resident.resident.name) : null;
    }

    /// <summary>True when the NPC is a tenant and does not rent in the building yet.</summary>
    public static bool CanRent(int npcId)
    {
        var resident = ResidentOf(npcId);
        return resident != null && Tenement.IsAvailable && !TenementController.instance.ResidentRented(resident.resident.ID, true);
    }

    /// <summary>Whether the NPC could rent this apartment now, and if not, why.</summary>
    public static RentRefusal CheckRent(int npcId, int apartmentNumber)
    {
        var resident = ResidentOf(npcId);
        if (resident == null)
            return RentRefusal.NotATenant;
        var apartment = Tenement.Find(apartmentNumber);
        if (apartment == null)
            return RentRefusal.NoSuchApartment;
        if (apartment.apartmentNumber == 1)
            return RentRefusal.PlayersApartment;
        var controller = TenementController.instance;
        if (controller.ResidentRented(resident.resident.ID, true) || LivesInAnApartment(controller, resident))
            return RentRefusal.AlreadyRenting;
        if (apartment.resident != null && !apartment.onlyFurniture)
            return RentRefusal.Occupied;
        if (controller.blueprintManager == null || controller.residentOpen)
            return RentRefusal.NotHere;
        switch (resident.CheckRequirements(apartment))
        {
            case TenementResident.CantRentReason.None: return RentRefusal.None;
            case TenementResident.CantRentReason.Size: return RentRefusal.WrongSize;
            case TenementResident.CantRentReason.Resident: return RentRefusal.Occupied;
            case TenementResident.CantRentReason.PlayerApartment: return RentRefusal.PlayersApartment;
            case TenementResident.CantRentReason.Tenement: return RentRefusal.BuildingNotGoodEnough;
            default: return RentRefusal.NotGoodEnough;
        }
    }

    /// <summary>
    /// Rents the apartment to the NPC, skipping the rent screen. They move in after <paramref name="moveInHours"/>
    /// game hours. With <paramref name="announce"/> the game plays its own rent scene (a fade, the deposit paid
    /// to the player, achievements); without it the rent is quiet and no deposit changes hands, now or when they
    /// leave. Returns the reason when refused.
    /// </summary>
    public static RentRefusal Rent(int npcId, int apartmentNumber, float moveInHours = HousingRules.DefaultMoveInHours, bool announce = false)
    {
        var refusal = CheckRent(npcId, apartmentNumber);
        if (refusal != RentRefusal.None)
            return refusal;

        var resident = ResidentOf(npcId);
        var apartment = Tenement.Find(apartmentNumber);
        float hours = moveInHours <= 0f ? 0f : HousingRules.ClampTimerHours(moveInHours);
        using (WorldChanges.EnterMod())
            TenementController.instance.RentApartment(resident, apartment, hours, !announce);
        if (!announce)
            apartment.deposit = new Money.money(0, 0f);
        return RentRefusal.None;
    }

    /// <summary>
    /// Starts one of the tenant's events of this kind (noise, trash, complaints...) if it may run now. The
    /// tenant has to live in the building. True when one started.
    /// </summary>
    public static bool TriggerEvent(int npcId, TenantEventKind kind)
    {
        if (!Enum.IsDefined(typeof(TenantEventKind), kind) || kind == TenantEventKind.None)
            throw new ArgumentOutOfRangeException(nameof(kind));
        var resident = ResidentOf(npcId);
        if (resident == null || resident.tenementEvents == null || TenementEventController.instance == null || !LivesHere(resident))
            return false;
        var type = (TenementEvent.EventType)(int)kind;
        foreach (var tenantEvent in resident.tenementEvents.tenementEvents)
        {
            if (tenantEvent == null || tenantEvent.type != type || !tenantEvent.CanTrigger())
                continue;
            using (WorldChanges.EnterMod())
                tenantEvent.OnTrigger(resident.resident.ID);
            return true;
        }
        return false;
    }

    private static bool LivesHere(TenementResident resident)
    {
        if (!Tenement.IsAvailable)
            return false;
        foreach (var apartment in TenementController.instance.apartments)
        {
            if (apartment != null && apartment.resident != null && apartment.resident.Compare(resident) && !apartment.moveStarted && !apartment.onlyFurniture)
                return true;
        }
        return false;
    }

    // The game's own resident list only fills when someone rents during play, so tenants a level starts
    // with are missing from it; the apartments know who lives where.
    private static bool LivesInAnApartment(TenementController controller, TenementResident resident)
    {
        if (controller.apartments == null)
            return false;
        foreach (var apartment in controller.apartments)
        {
            if (apartment != null && apartment.resident != null && !apartment.onlyFurniture && apartment.resident.Compare(resident))
                return true;
        }
        return false;
    }

    private static TenementResident ResidentOf(int npcId)
    {
        var director = NPCDirector.instance;
        if (director == null)
            return null;
        var data = director.GetNPCDataByID(npcId);
        var character = data != null ? data.character : null;
        var resident = character != null ? character.resident : null;
        return resident != null && resident.resident != null ? resident : null;
    }
}
