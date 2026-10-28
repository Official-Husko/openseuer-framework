// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
using System;
using System.Collections.Generic;
using HarmonyLib;
using ObenFramework.Saves;
using ObenFramework.World;
using ObenFramework.World.Addressing;
using PixelCrushers.DialogueSystem;
using UnityEngine;

namespace ObenFramework.Npcs;

/// <summary>
/// The phones in the scene you are in (wall phones, desk phones, payphones): list them, ring one with a
/// number from the phone book, answer or decline it, start a call on it, and hear when calls start, end or
/// ring. Numbers themselves are in <see cref="PhoneNumbers"/>. Phones are named by their address.
/// </summary>
public static class Phones
{
    private static readonly AccessTools.FieldRef<Phone, bool> CallActive = AccessTools.FieldRefAccess<Phone, bool>("callActive");
    private static readonly AccessTools.FieldRef<Phone, PhoneNumber> IncomingCall = AccessTools.FieldRefAccess<Phone, PhoneNumber>("incomingCall");
    private static readonly AccessTools.FieldRef<Phone, TimeOfDayAzure.Timer> HangupTimer = AccessTools.FieldRefAccess<Phone, TimeOfDayAzure.Timer>("hangupTimer");
    private static readonly AccessTools.FieldRef<Phone, ConversationTrigger> Trigger = AccessTools.FieldRefAccess<Phone, ConversationTrigger>("conversationTrigger");
    private static readonly AccessTools.FieldRef<Phone, InteractableTalk> Talk = AccessTools.FieldRefAccess<Phone, InteractableTalk>("interactableTalk");

    // The number of the call being started or on; the game keeps it only on the dialogue trigger.
    internal static PhoneNumber Active;

    /// <summary>A call started: the conversation with the number on the other end opened.</summary>
    public static readonly WorldEvent<PhoneCallEvent> CallStarted = new WorldEvent<PhoneCallEvent>(nameof(CallStarted));

    /// <summary>A call ended: hung up, or the conversation finished.</summary>
    public static readonly WorldEvent<PhoneCallEvent> CallEnded = new WorldEvent<PhoneCallEvent>(nameof(CallEnded));

    /// <summary>A phone started ringing (story calls, or <see cref="Ring"/>).</summary>
    public static readonly WorldEvent<PhoneCallEvent> Ringing = new WorldEvent<PhoneCallEvent>(nameof(Ringing));

    /// <summary>The phones in the scene. Looks the scene over; not for every frame.</summary>
    public static List<PhoneRecord> All()
    {
        var result = new List<PhoneRecord>();
        foreach (var phone in Scan())
            result.Add(new PhoneRecord(phone, ObjectAddress.Of(phone).ToString(), CallActive(phone), IncomingCall(phone)));
        return result;
    }

    /// <summary>
    /// Rings a phone with a call from <paramref name="number"/> (a number in the phone book). The player can
    /// answer within <paramref name="hangupSeconds"/> game seconds. <paramref name="answered"/> gets true when
    /// they pick up (the number's conversation then starts) and false when it rings out or is declined. False
    /// when the phone or number is unknown, or the phone is in use or already ringing.
    /// </summary>
    public static bool Ring(string phone, string number, int hangupSeconds, Action<bool> answered = null)
    {
        var target = Find(phone);
        var caller = Lookup(number);
        if (target == null || caller == null || SaveGate.IsBusy || hangupSeconds <= 0)
            return false;
        bool placing = true;
        bool refused = false;
        try
        {
            target.ReceiveCall(caller, hangupSeconds,
                () => Report(answered, true),
                () =>
                {
                    if (placing)
                        refused = true;
                    else
                        Report(answered, false);
                });
        }
        catch (Exception e)
        {
            NpcsLog.Failed("Phones.Ring", e);
            return false;
        }
        placing = false;
        return !refused;
    }

    /// <summary>Picks up a ringing phone for the player. False when it is not ringing or a menu or conversation is open.</summary>
    public static bool Answer(string phone)
    {
        var target = Find(phone);
        if (target == null || SaveGate.IsBusy || IncomingCall(target) == null || HangupTimer(target) == null || !Ready(target) || Blocked())
            return false;
        return Run("Answer", target.Interact) && IncomingCall(target) == null;
    }

    /// <summary>Lets a ringing phone ring out at once (the caller hears no answer).</summary>
    public static bool Decline(string phone)
    {
        var target = Find(phone);
        if (target == null || SaveGate.IsBusy || IncomingCall(target) == null || HangupTimer(target) == null)
            return false;
        return Run("Decline", target.ExpireReceivedCall);
    }

    /// <summary>
    /// Starts a call to <paramref name="number"/> on a phone, as if the player had dialled it there. False when the
    /// phone is in use, ringing, a menu or conversation is open, or the number is unknown.
    /// </summary>
    public static bool Call(string phone, string number)
    {
        var target = Find(phone);
        var callee = Lookup(number);
        if (target == null || callee == null || SaveGate.IsBusy || IncomingCall(target) != null || !Ready(target) || Blocked())
            return false;
        if (!Run("Call", () => target.StartCall(callee)))
            return false;
        if (CallActive(target) || PhoneNumbers.LineOf(callee) != null)
            return true;

        // The conversation did not open; take back what StartCall hooked up.
        var talk = Talk(target);
        if (talk != null && talk.onStartTalkCallback == (Action)target.OnCallStarted)
            talk.onStartTalkCallback = null;
        if (talk != null && talk.onEndTalkCallback == (Action)target.OnCallEnded)
            talk.onEndTalkCallback = null;
        if (target.backgroundStatic != null)
            target.backgroundStatic.StopSound(destroySource: true);
        Active = null;
        return false;
    }

    internal static void RaiseStarted(Phone phone)
    {
        if (Active != null && CallStarted.HasSubscribers)
            CallStarted.Raise(new PhoneCallEvent(ObjectAddress.Of(phone).ToString(), PhoneDigits.Format(Active.number)));
    }

    internal static void RaiseEnded(Phone phone)
    {
        var ended = Active;
        Active = null;
        if (ended != null && CallEnded.HasSubscribers)
            CallEnded.Raise(new PhoneCallEvent(ObjectAddress.Of(phone).ToString(), PhoneDigits.Format(ended.number)));
    }

    internal static void RaiseRinging(Phone phone, PhoneNumber number)
    {
        if (number != null && IncomingCall(phone) == number && Ringing.HasSubscribers)
            Ringing.Raise(new PhoneCallEvent(ObjectAddress.Of(phone).ToString(), PhoneDigits.Format(number.number)));
    }

    internal static bool IsRinging(Phone phone) => IncomingCall(phone) != null;

    private static bool Ready(Phone phone) => !CallActive(phone) && Trigger(phone) != null && Talk(phone) != null;

    private static bool Blocked() =>
        (GameUIController.instance != null && GameUIController.instance.IsMenuVisible) ||
        (DialogueController.instance != null && DialogueController.instance.DialogueActive);

    private static PhoneNumber Lookup(string number)
    {
        if (!PhoneDigits.TryParse(number, out var keys))
            return null;
        foreach (var entry in PhoneNumberDatabase.database)
        {
            if (entry != null && PhoneDigits.Same(entry.number, keys))
                return entry;
        }
        return null;
    }

    private static Phone Find(string address)
    {
        if (string.IsNullOrEmpty(address))
            return null;
        foreach (var phone in Scan())
        {
            if (ObjectAddress.Of(phone).ToString() == address)
                return phone;
        }
        return null;
    }

    private static Phone[] Scan() =>
        Game.WorldState.InWorld ? UnityEngine.Object.FindObjectsByType<Phone>(FindObjectsSortMode.None) : Array.Empty<Phone>();

    private static void Report(Action<bool> answered, bool picked)
    {
        if (answered == null)
            return;
        try { answered(picked); }
        catch (Exception e) { NpcsLog.Failed("A phone ring callback", e); }
    }

    private static bool Run(string what, Action action)
    {
        try
        {
            action();
            return true;
        }
        catch (Exception e)
        {
            NpcsLog.Failed("Phones." + what, e);
            return false;
        }
    }
}
