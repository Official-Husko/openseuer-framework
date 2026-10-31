// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
using System;
using HarmonyLib;
using ObenFramework.World;

namespace ObenFramework.Housing;

/// <summary>Raises the tenement events from the controller's tenancy, rent and tenant event methods.</summary>
[HarmonyPatch]
internal static class TenementPatches
{
    private static bool _inRentCheck;
    private static bool _rentDue;

    /// <summary>Set by <see cref="Tenants.Rent"/> for a quiet rent, whose deposit is dropped before anyone hears of it.</summary>
    internal static bool QuietRent;

    [HarmonyPostfix]
    [HarmonyPatch(typeof(TenementController), nameof(TenementController.RentApartment), new[] { typeof(TenementResident), typeof(TenementApartment), typeof(float), typeof(bool) })]
    private static void Rented(TenementApartment apartment)
    {
        if (QuietRent && apartment != null)
            apartment.deposit = new Money.money(0, 0f);
        RaiseTenant(apartment, TenantChangeKind.Rented);
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(TenementController), nameof(TenementController.MoveResident))]
    private static void MovedIn(TenementApartment apartment) => RaiseTenant(apartment, TenantChangeKind.MovedIn);

    [HarmonyPrefix]
    [HarmonyPatch(typeof(TenementController), nameof(TenementController.EvictResident))]
    private static void BeforeEvict(TenementApartment apartment, out TenantInfo __state) =>
        __state = Tenement.TenantChanged.HasSubscribers ? Tenement.TenantOf(apartment) : null;

    [HarmonyPostfix]
    [HarmonyPatch(typeof(TenementController), nameof(TenementController.EvictResident))]
    private static void AfterEvict(TenementApartment apartment, TenantInfo __state)
    {
        if (__state == null || Tenement.TenantOf(apartment) != null)
            return;
        Raise(new TenantChange(apartment.apartmentNumber, __state, TenantChangeKind.MovedOut, WorldChanges.Current));
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(TenementController), nameof(TenementController.StartEviction))]
    private static void BeforeNotice(TenementApartment apartment, out bool __state) => __state = apartment != null && apartment.evictionStarted;

    [HarmonyPostfix]
    [HarmonyPatch(typeof(TenementController), nameof(TenementController.StartEviction))]
    private static void AfterNotice(TenementApartment apartment, bool __state)
    {
        if (!__state && apartment != null && apartment.evictionStarted)
            RaiseTenant(apartment, TenantChangeKind.EvictionStarted);
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(TenementController), nameof(TenementController.CancelEviction))]
    private static void BeforeCancel(TenementApartment apartment, out bool __state) => __state = apartment != null && apartment.evictionStarted;

    [HarmonyPostfix]
    [HarmonyPatch(typeof(TenementController), nameof(TenementController.CancelEviction))]
    private static void AfterCancel(TenementApartment apartment, bool __state)
    {
        if (__state && apartment != null && !apartment.evictionStarted)
            RaiseTenant(apartment, TenantChangeKind.EvictionCancelled);
    }

    // The rent screen's buttons are the player's; a mod's own scope still wins.
    [HarmonyPrefix]
    [HarmonyPatch(typeof(ApartmentRentUI), nameof(ApartmentRentUI.OnRentButtonPressed))]
    private static void RentPressing() => WorldChanges.Adjust(ChangeSource.LocalPlayer, 1);

    [HarmonyFinalizer]
    [HarmonyPatch(typeof(ApartmentRentUI), nameof(ApartmentRentUI.OnRentButtonPressed))]
    private static Exception RentPressed(Exception __exception) => PlayerDone(__exception);

    [HarmonyPrefix]
    [HarmonyPatch(typeof(ApartmentRentUI), nameof(ApartmentRentUI.OnEvictButtonPressed))]
    private static void EvictPressing() => WorldChanges.Adjust(ChangeSource.LocalPlayer, 1);

    [HarmonyFinalizer]
    [HarmonyPatch(typeof(ApartmentRentUI), nameof(ApartmentRentUI.OnEvictButtonPressed))]
    private static Exception EvictPressed(Exception __exception) => PlayerDone(__exception);

    [HarmonyPrefix]
    [HarmonyPatch(typeof(ApartmentRentUI), nameof(ApartmentRentUI.OnCancelEvictButtonPressed))]
    private static void CancelPressing() => WorldChanges.Adjust(ChangeSource.LocalPlayer, 1);

    [HarmonyFinalizer]
    [HarmonyPatch(typeof(ApartmentRentUI), nameof(ApartmentRentUI.OnCancelEvictButtonPressed))]
    private static Exception CancelPressed(Exception __exception) => PlayerDone(__exception);

    private static Exception PlayerDone(Exception exception)
    {
        WorldChanges.Adjust(ChangeSource.LocalPlayer, -1);
        return exception;
    }

    // UpdateRentPayment runs every game second; only the difficulty's "is rent due" answer inside it tells a payment happened.
    [HarmonyPrefix]
    [HarmonyPatch(typeof(TenementController), "UpdateRentPayment")]
    private static void BeforeRent()
    {
        _inRentCheck = Tenement.RentPaid.HasSubscribers;
        _rentDue = false;
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(DifficultyInfo), nameof(DifficultyInfo.GetRentPayment))]
    private static void RentAnswer(bool __result)
    {
        if (_inRentCheck)
            _rentDue = __result;
    }

    [HarmonyFinalizer]
    [HarmonyPatch(typeof(TenementController), "UpdateRentPayment")]
    private static void AfterRent(Exception __exception)
    {
        bool due = _rentDue && __exception == null;
        _inRentCheck = _rentDue = false;
        if (!due || !WorldChanges.Active)
            return;
        try
        {
            Tenement.RentPaid.Raise(Tenement.TotalIncome);
        }
        catch (Exception e)
        {
            WorldLog.Failed("Rent hook", e);
        }
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(TenementEvent), nameof(TenementEvent.OnTrigger))]
    private static void TenantEventStarted(TenementEvent __instance, int targetNPC)
    {
        if (!Tenement.EventStarted.HasSubscribers || !WorldChanges.Active)
            return;
        try
        {
            int type = (int)__instance.type;
            var kind = Enum.IsDefined(typeof(TenantEventKind), type) ? (TenantEventKind)type : TenantEventKind.None;
            Tenement.EventStarted.Raise(new TenantEvent(__instance.id, kind, targetNPC, WorldChanges.Current));
        }
        catch (Exception e)
        {
            WorldLog.Failed("Tenant event hook", e);
        }
    }

    internal static void RaiseTenant(TenementApartment apartment, TenantChangeKind kind)
    {
        if (apartment == null || !Tenement.TenantChanged.HasSubscribers)
            return;
        var tenant = Tenement.TenantOf(apartment);
        if (tenant != null)
            Raise(new TenantChange(apartment.apartmentNumber, tenant, kind, WorldChanges.Current));
    }

    private static void Raise(TenantChange change)
    {
        if (!WorldChanges.Active)
            return;
        try
        {
            Tenement.TenantChanged.Raise(change);
        }
        catch (Exception e)
        {
            WorldLog.Failed("Tenant hook", e);
        }
    }
}
