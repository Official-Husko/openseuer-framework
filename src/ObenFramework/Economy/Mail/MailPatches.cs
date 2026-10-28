using System;
using System.Collections.Generic;
using HarmonyLib;
using ObenFramework.World;

namespace ObenFramework.Economy;

/// <summary>
/// Turns the game's mail deliveries into <see cref="PlayerMail.Received"/>. Every delivery to the player ends in
/// one of two places: the funnel <c>SendMail(Mail, Target, TimeAndDay)</c> when there is no delay, or a timer
/// callback when the mail was in transit. The game's own <c>MailReceived</c> event only carries the name and is
/// replayed for waiting mail on every load, so these methods are watched instead and the list that grew
/// tells what arrived.
/// </summary>
[HarmonyPatch]
internal static class MailPatches
{
    [HarmonyPatch(typeof(MailController), nameof(MailController.SendMail),
        new[] { typeof(MailController.Mail), typeof(MailController.Target), typeof(TimeAndDay) })]
    [HarmonyPostfix]
    private static void Sent(MailController __instance, MailController.Mail mail, MailController.Target target)
    {
        if (!PlayerMail.Received.HasSubscribers || mail == null)
            return;
        try
        {
            if (target == MailController.Target.Player && __instance.receivedMail.Contains(mail))
                PlayerMail.RaiseReceived(mail, MailKind.Letter);
            else if (target == MailController.Target.PlayerParcelLocker && __instance.receivedPackages.Contains(mail))
                PlayerMail.RaiseReceived(mail, MailKind.Parcel);
        }
        catch (Exception e)
        {
            WorldLog.Failed("Mail hook (send)", e);
        }
    }

    [HarmonyPatch(typeof(MailController), nameof(MailController.TimerSendMailPlayer))]
    [HarmonyPrefix]
    private static void LetterTimerBefore(MailController __instance, out int __state) =>
        __state = __instance.receivedMail.Count;

    [HarmonyPatch(typeof(MailController), nameof(MailController.TimerSendMailPlayer))]
    [HarmonyPostfix]
    private static void LetterTimerAfter(MailController __instance, int __state) =>
        Delivered(__instance.receivedMail, __state, MailKind.Letter);

    [HarmonyPatch(typeof(MailController), nameof(MailController.TimerSendPackagePlayer))]
    [HarmonyPrefix]
    private static void ParcelTimerBefore(MailController __instance, out int __state) =>
        __state = __instance.receivedPackages.Count;

    [HarmonyPatch(typeof(MailController), nameof(MailController.TimerSendPackagePlayer))]
    [HarmonyPostfix]
    private static void ParcelTimerAfter(MailController __instance, int __state) =>
        Delivered(__instance.receivedPackages, __state, MailKind.Parcel);

    private static void Delivered(List<MailController.Mail> list, int before, MailKind kind)
    {
        if (!PlayerMail.Received.HasSubscribers || list.Count <= before)
            return;
        try
        {
            PlayerMail.RaiseReceived(list[list.Count - 1], kind);
        }
        catch (Exception e)
        {
            WorldLog.Failed("Mail hook (delivery)", e);
        }
    }
}
