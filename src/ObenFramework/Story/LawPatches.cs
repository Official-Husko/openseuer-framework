// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
using System;
using HarmonyLib;
using NPC;

namespace ObenFramework.Story;

/// <summary>
/// The game side of <see cref="Law"/>: crimes on their way onto the record (interceptors, then the
/// <see cref="Law.Committed"/> event), paid fines, and release from prison.
/// </summary>
[HarmonyPatch]
internal static class LawPatches
{
    /// <summary>The last CommitCrime call was cancelled by an interceptor (for <see cref="Law.Commit"/>'s answer).</summary>
    internal static bool LastCancelled;

    [HarmonyPrefix]
    [HarmonyPatch(typeof(Crime), nameof(Crime.CommitCrime))]
    private static bool BeforeCommit(NPCReference victim, ref int severity, ref Crime.CrimeType type, ref bool silent, out CrimeAttempt __state)
    {
        __state = null;
        try
        {
            var attempt = new CrimeAttempt((CrimeKind)type, Math.Max(1, severity), victim == null || victim.ID <= 0 ? -1 : victim.ID, silent);
            if (Law.HasInterceptors)
            {
                Law.RunInterceptors(attempt);
                if (attempt.Cancel)
                {
                    LastCancelled = true;
                    return false;
                }
                if (attempt.Kind == CrimeKind.None || !Enum.IsDefined(typeof(CrimeKind), attempt.Kind))
                    attempt.Kind = (CrimeKind)type;
                attempt.Severity = Math.Max(1, Math.Min(attempt.Severity, Law.MaxSeverity));
                severity = attempt.Severity;
                type = (Crime.CrimeType)attempt.Kind;
                silent = attempt.Silent;
            }
            __state = attempt;
        }
        catch (Exception e)
        {
            StoryLog.Failed("CommitCrime hook", e);
        }
        return true;
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(Crime), nameof(Crime.CommitCrime))]
    private static void AfterCommit(CrimeAttempt __state)
    {
        if (__state == null)
            return;
        try { Law.QueueCommitted(new CrimeCommitted(__state.Kind, __state.Severity, __state.VictimId)); }
        catch (Exception e) { StoryLog.Failed("CommitCrime hook", e); }
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(Crime), nameof(Crime.PayFine))]
    private static void BeforePayFine(Crime __instance, out int __state)
    {
        __state = -1;
        try { __state = (int)Math.Round(__instance.GetCrimeFineAmount()); }
        catch (Exception e) { StoryLog.Failed("PayFine hook", e); }
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(Crime), nameof(Crime.PayFine))]
    private static void AfterPayFine(bool __result, int __state)
    {
        if (!__result || __state <= 0)
            return;
        try { Law.QueueFined(__state); }
        catch (Exception e) { StoryLog.Failed("PayFine hook", e); }
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(Prison), nameof(Prison.Release))]
    private static void BeforeRelease(Prison __instance, out bool __state)
    {
        __state = false;
        try { __state = Law.InPrisonFlag(__instance); }
        catch (Exception e) { StoryLog.Failed("Prison.Release hook", e); }
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(Prison), nameof(Prison.Release))]
    private static void AfterRelease(Prison __instance, bool __state)
    {
        try
        {
            if (__state && !Law.InPrisonFlag(__instance))
                Law.QueueReleased();
        }
        catch (Exception e)
        {
            StoryLog.Failed("Prison.Release hook", e);
        }
    }
}
