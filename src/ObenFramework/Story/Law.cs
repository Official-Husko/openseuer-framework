// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
using System;
using System.Collections.Generic;
using HarmonyLib;
using NPC;
using ObenFramework.Saves;
using UnityEngine;

namespace ObenFramework.Story;

/// <summary>
/// The player's crimes and their consequences: the record, the fine, the sentence, committing and clearing
/// crimes, paying the fine and being arrested. The record is the game's own (<c>Crime</c>), saved with the
/// game. Police chases are in <see cref="Npcs.Pursuit"/>, being seen by a witness or camera in
/// <see cref="Npcs.Surveillance.PlayerSpotted"/>. Everything needs a loaded world (<see cref="IsAvailable"/>).
/// </summary>
public static partial class Law
{
    public const int MaxSeverity = 1000000;
    private const float MaxSeverityFactor = 100f;

    private static readonly AccessTools.FieldRef<Crime, bool> ArrestInProgress = AccessTools.FieldRefAccess<Crime, bool>("arrestInProgress");
    internal static readonly AccessTools.FieldRef<Prison, bool> InPrisonFlag = AccessTools.FieldRefAccess<Prison, bool>("inPrison");

    public static bool IsAvailable => Crime.instance != null;

    /// <summary>Everything on the record plus a tenth of the value of the stolen goods the player carries.</summary>
    public static float Total => Read(c => c.CountCrimes(true), 0f);

    /// <summary>Only what is on the record: what fines and sentences are worked out from.</summary>
    public static float TotalOnRecord => Read(c => c.CountCrimes(false), 0f);

    /// <summary>What paying the fine at the police station costs right now, in the real currency.</summary>
    public static int Fine => Read(c => (int)Math.Round(c.GetCrimeFineAmount()), 0);

    /// <summary>Hours in prison if arrested now (at least 1).</summary>
    public static int SentenceHours => Read(c => Mathf.Clamp(Mathf.RoundToInt(c.CountCrimes(false) / c.crimePerPrisonHour), 1, 100 + c.timesInJail * 12), 0);

    public static int TimesInJail => Read(c => c.timesInJail, 0);

    public static bool IsArrestInProgress => Read(c => c.ArrestInProgress, false);

    public static bool HasStolenGoods => Read(c => Inventory.instance != null && c.HasStolenGoods(), false);

    /// <summary>The full value of the stolen goods the player carries.</summary>
    public static int StolenGoodsValue => Read(c => Inventory.instance == null ? 0 : (int)Math.Min(c.CountStolenGoodsValue(), int.MaxValue), 0);

    /// <summary>The player is serving a sentence (a prison level is loaded and the player is locked in).</summary>
    public static bool InPrison
    {
        get
        {
            var prison = UnityEngine.Object.FindFirstObjectByType<Prison>();
            return prison != null && InPrisonFlag(prison);
        }
    }

    /// <summary>The general (not against anyone) amount of one kind of crime.</summary>
    public static float Generic(CrimeKind kind) => Read(c =>
    {
        float total = 0f;
        foreach (var line in c.crimeRecord)
        {
            if (line.generic && line.type == (Crime.CrimeType)kind)
                total += line.amount;
        }
        return total;
    }, 0f);

    /// <summary>Everything on the record against one NPC.</summary>
    public static float Against(int npcId) => npcId <= 0 ? 0f : Read(c => c.CountCrimes(false, 0f, 0, new NPCReference(npcId)), 0f);

    /// <summary>The record line by line (lines that were cleared read 0).</summary>
    public static List<CrimeRecordLine> Records()
    {
        var lines = new List<CrimeRecordLine>();
        var crime = Crime.instance;
        if (crime == null)
            return lines;
        foreach (var line in crime.crimeRecord)
        {
            bool victim = !line.generic && line.victim != null && line.victim.ID > 0;
            lines.Add(new CrimeRecordLine((CrimeKind)line.type, victim ? line.victim.ID : -1, victim ? line.victim.name : null, line.amount));
        }
        return lines;
    }

    /// <summary>
    /// Puts a crime on the record the way the game does (a notification unless <paramref name="silent"/>,
    /// <see cref="Committed"/>, mods' interceptors). <paramref name="victimNpcId"/> 0 or less means nobody in particular.
    /// False when an interceptor cancelled it, the values are out of range, or no world or a save runs.
    /// </summary>
    public static bool Commit(CrimeKind kind, int severity, int victimNpcId = 0, bool silent = false)
    {
        var crime = Crime.instance;
        if (crime == null || !ValidKind(kind) || severity < 1 || severity > MaxSeverity || SaveGate.IsBusy || Achievements.instance == null)
            return false;
        try
        {
            LawPatches.LastCancelled = false;
            crime.CommitCrime(victimNpcId > 0 ? new NPCReference(victimNpcId) : null, severity, (Crime.CrimeType)kind, silent);
            return !LawPatches.LastCancelled;
        }
        catch (Exception e)
        {
            StoryLog.Failed("Law.Commit", e);
            return false;
        }
    }

    /// <summary>Clears the whole record.</summary>
    public static bool Clear() => Edit(c => c.ClearCrime());

    /// <summary>Clears every crime against one NPC.</summary>
    public static bool Clear(int victimNpcId) => victimNpcId > 0 && Edit(c =>
    {
        var victim = new NPCReference(victimNpcId);
        foreach (Crime.CrimeType type in Enum.GetValues(typeof(Crime.CrimeType)))
            c.ClearCrime(victim, type);
    });

    /// <summary>Clears the general amount of one kind of crime.</summary>
    public static bool Clear(CrimeKind kind) => ValidKind(kind) && Edit(c => c.ClearCrime(null, (Crime.CrimeType)kind));

    /// <summary>Clears one kind of crime against one NPC.</summary>
    public static bool Clear(int victimNpcId, CrimeKind kind) =>
        victimNpcId > 0 && ValidKind(kind) && Edit(c => c.ClearCrime(new NPCReference(victimNpcId), (Crime.CrimeType)kind));

    /// <summary>Pays the fine from the player's real money like the police desk does (stolen goods are taken too). Raises <see cref="Fined"/>.</summary>
    public static bool PayFine()
    {
        var crime = Crime.instance;
        if (!CanEdit(crime) || Money.instance == null || Inventory.instance == null || !crime.CanPayFine())
            return false;
        try { return crime.PayFine(); }
        catch (Exception e) { StoryLog.Failed("Law.PayFine", e); return false; }
    }

    /// <summary>
    /// Arrests the player the way the police do: the record is multiplied by <paramref name="severityFactor"/>
    /// (more than 0, at most 100), the player passes out and wakes in prison. Raises <see cref="Arrested"/>.
    /// Refused during a conversation, in prison, during a save and while another arrest runs.
    /// </summary>
    public static bool Arrest(float severityFactor = 1f)
    {
        var crime = Crime.instance;
        if (!(severityFactor > 0f) || severityFactor > MaxSeverityFactor || !CanEdit(crime) || InPrison
            || Dialogue.Conversations.IsActive || (DialogueController.instance != null && DialogueController.instance.DialogueActive)
            || Achievements.instance == null || PlayerStats.instance == null || WaitingController.instance == null || Inventory.instance == null)
            return false;
        try
        {
            crime.GoToPrison(severityFactor);
            return true;
        }
        catch (Exception e)
        {
            ArrestInProgress(crime) = false;
            StoryLog.Failed("Law.Arrest", e);
            return false;
        }
    }

    private static bool ValidKind(CrimeKind kind) => kind != CrimeKind.None && Enum.IsDefined(typeof(CrimeKind), kind);

    private static bool CanEdit(Crime crime)
    {
        var loading = LoadingScreen.instance;
        return crime != null && !SaveGate.IsBusy && !crime.ArrestInProgress && !Changelevel.ChangeLevelActive
            && (loading == null || (loading.LevelLoadDone && !loading.Fading));
    }

    private static bool Edit(Action<Crime> edit)
    {
        var crime = Crime.instance;
        if (!CanEdit(crime))
            return false;
        try
        {
            edit(crime);
            return true;
        }
        catch (Exception e)
        {
            StoryLog.Failed("Law edit", e);
            return false;
        }
    }

    private static T Read<T>(Func<Crime, T> read, T fallback)
    {
        var crime = Crime.instance;
        if (crime == null)
            return fallback;
        try { return read(crime); }
        catch (Exception e) { StoryLog.Failed("Law read", e); return fallback; }
    }
}
