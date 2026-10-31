// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
using System;
using HarmonyLib;
using ObenFramework.Economy;
using ObenFramework.World;

namespace ObenFramework.Housing;

/// <summary>
/// The mailbox and parcel locker hooks: <see cref="Mailboxes.Used"/> and <see cref="ParcelLockers.Used"/> from
/// the game methods that move the mail (so the framework's own calls are raised too), the player's scope around
/// using a box or taking a parcel, and quiet takes.
/// </summary>
[HarmonyPatch]
internal static class MailPatches
{
    private static int _quiet;

    /// <summary>While the returned scope is open, taking mail shows no toasts.</summary>
    internal static QuietScope Quiet(bool on)
    {
        if (on)
            _quiet++;
        return new QuietScope(on);
    }

    internal readonly struct QuietScope : IDisposable
    {
        private readonly bool _on;

        internal QuietScope(bool on) => _on = on;

        public void Dispose()
        {
            if (_on)
                _quiet = Math.Max(0, _quiet - 1);
        }
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(MailController), nameof(MailController.TakeMailitem))]
    private static void TakingItem(ref bool silent)
    {
        if (_quiet > 0)
            silent = true;
    }

    // The player using a box or a locker; a mod's own scope still wins.
    [HarmonyPrefix]
    [HarmonyPatch(typeof(Mailbox), nameof(Mailbox.Interact))]
    private static void BoxUsing() => PlayerStarts();

    [HarmonyFinalizer]
    [HarmonyPatch(typeof(Mailbox), nameof(Mailbox.Interact))]
    private static Exception BoxUsed(Exception __exception) => PlayerEnds(__exception);

    [HarmonyPrefix]
    [HarmonyPatch(typeof(Mailbox), nameof(Mailbox.TakeMail))]
    private static void BeforeTakeMail(Mailbox __instance, out int __state) => __state = Count(__instance);

    [HarmonyPostfix]
    [HarmonyPatch(typeof(Mailbox), nameof(Mailbox.TakeMail))]
    private static void AfterTakeMail(Mailbox __instance, int __state) => RaiseMailbox(__instance, __state);

    [HarmonyPrefix]
    [HarmonyPatch(typeof(Mailbox), nameof(Mailbox.InsertMail))]
    private static void BeforeInsert(Mailbox __instance, out int __state) => __state = Count(__instance);

    [HarmonyPostfix]
    [HarmonyPatch(typeof(Mailbox), nameof(Mailbox.InsertMail))]
    private static void AfterInsert(Mailbox __instance, int __state) => RaiseMailbox(__instance, __state);

    [HarmonyFinalizer]
    [HarmonyPatch(typeof(ParcelLocker), nameof(ParcelLocker.Interact))]
    private static Exception LockerUsed(Exception __exception) => PlayerEnds(__exception);

    [HarmonyFinalizer]
    [HarmonyPatch(typeof(ParcelLocker), nameof(ParcelLocker.TakeParcelFromLocker))]
    private static Exception ParcelTaken(Exception __exception) => PlayerEnds(__exception);

    [HarmonyPrefix]
    [HarmonyPatch(typeof(ParcelLocker), nameof(ParcelLocker.Interact))]
    private static void BeforeClaim(out int __state)
    {
        PlayerStarts();
        __state = ParcelLockers.Used.HasSubscribers && MailController.instance != null ? MailController.instance.GetPackageAmount() : 0;
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(ParcelLocker), nameof(ParcelLocker.Interact))]
    private static void AfterClaim(ParcelLocker __instance, int __state)
    {
        if (__state <= 0 || !WorldChanges.Active)
            return;
        int claimed = __state - MailController.instance.GetPackageAmount();
        if (claimed > 0)
            RaiseLocker(new LockerUse(__instance, LockerUseKind.Claimed, claimed, -1, null, WorldChanges.Current));
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(ParcelLocker), nameof(ParcelLocker.TakeParcelFromLocker))]
    private static void BeforeTake(ParcelLocker __instance, int lockerind, out MailInfo __state)
    {
        PlayerStarts();
        __state = null;
        if (ParcelLockers.Used.HasSubscribers && __instance.lockers != null && lockerind >= 0 && lockerind < __instance.lockers.Length)
            __state = ParcelLockers.ParcelIn(__instance.lockers[lockerind]);
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(ParcelLocker), nameof(ParcelLocker.TakeParcelFromLocker))]
    private static void AfterTake(ParcelLocker __instance, int lockerind, MailInfo __state)
    {
        if (__state != null && WorldChanges.Active && ParcelLockers.ParcelIn(__instance.lockers[lockerind]) == null)
            RaiseLocker(new LockerUse(__instance, LockerUseKind.Taken, 1, lockerind, __state, WorldChanges.Current));
    }

    private static void PlayerStarts() => WorldChanges.Adjust(ChangeSource.LocalPlayer, 1);

    private static Exception PlayerEnds(Exception exception)
    {
        WorldChanges.Adjust(ChangeSource.LocalPlayer, -1);
        return exception;
    }

    private static void RaiseMailbox(Mailbox mailbox, int before)
    {
        if (before <= 0 || !WorldChanges.Active)
            return;
        try
        {
            int moved = before - Mailboxes.CountOf(mailbox);
            if (moved > 0)
                Mailboxes.Used.Raise(new MailboxUse(mailbox, Mailboxes.ModeOf(mailbox), moved, WorldChanges.Current));
        }
        catch (Exception e)
        {
            WorldLog.Failed("Mailbox hook", e);
        }
    }

    private static int Count(Mailbox mailbox)
    {
        if (!Mailboxes.Used.HasSubscribers)
            return 0;
        try
        {
            return Mailboxes.CountOf(mailbox);
        }
        catch (Exception e)
        {
            WorldLog.Failed("Mailbox hook", e);
            return 0;
        }
    }

    private static void RaiseLocker(LockerUse use)
    {
        try
        {
            ParcelLockers.Used.Raise(use);
        }
        catch (Exception e)
        {
            WorldLog.Failed("Parcel locker hook", e);
        }
    }
}
