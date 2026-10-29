using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;

namespace ObenLoader;

/// <summary>Taking a Harmony id's patches off quickly.</summary>
public static class Unpatching
{
    // Harmony internals for putting a method back as it was. Harmony's own unpatch builds a new replacement
    // even when no patch is left: a full copy of the method, compiled again, and kept for good.
    private static readonly object HarmonyLock = Internal(typeof(PatchProcessor), "locker");
    private static readonly IDictionary Detours = Internal(typeof(Harmony).Assembly.GetType("HarmonyLib.PatchTools"), "detours") as IDictionary;
    private static readonly IDictionary States = Internal(typeof(Harmony).Assembly.GetType("HarmonyLib.HarmonySharedState"), "state") as IDictionary;

    /// <summary>
    /// Takes every patch of <paramref name="harmonyId"/> off. A method only that id patched gets its own code
    /// back; one other ids patch too is rebuilt once with their patches. Harmony's own <c>UnpatchAll(id)</c>
    /// builds and compiles a new copy of the method after each handler it removes, which made a mod with a
    /// few hundred patches take seconds to unload, and longer the more often mods were reloaded. A method
    /// that fails is passed to <paramref name="onError"/> and the rest still come off. Returns how many
    /// methods changed.
    /// </summary>
    public static int RemoveAll(string harmonyId, Action<MethodBase, Exception> onError = null)
    {
        if (string.IsNullOrEmpty(harmonyId) || harmonyId == "*")
            throw new ArgumentException("Give the Harmony id whose patches should come off", nameof(harmonyId));

        var harmony = new Harmony(harmonyId);
        int changed = 0;
        foreach (var original in new List<MethodBase>(Harmony.GetAllPatchedMethods()))
        {
            try
            {
                var info = Harmony.GetPatchInfo(original);
                if (info == null || !info.Owners.Contains(harmonyId))
                    continue;
                if (info.Owners.Count > 1 || !Restore(original, harmonyId))
                    harmony.Unpatch(original, HarmonyPatchType.All, harmonyId);
                changed++;
            }
            catch (Exception e)
            {
                onError?.Invoke(original, e);
            }
        }
        return changed;
    }

    // Undoes the method's detour (what Harmony does first when it swaps one replacement for the next) and
    // forgets its patch state, so the next patch starts from the original again.
    private static bool Restore(MethodBase original, string harmonyId)
    {
        if (HarmonyLock == null || Detours == null || States == null)
            return false;
        lock (HarmonyLock)
        {
            // Under Harmony's lock, in case another id patched it since it was looked at.
            var info = Harmony.GetPatchInfo(original);
            if (info == null || info.Owners.Count != 1 || !info.Owners.Contains(harmonyId))
                return false;
            lock (Detours)
            {
                if (!Detours.Contains(original))
                    return false;
                (Detours[original] as IDisposable)?.Dispose();
                Detours.Remove(original);
            }
            lock (States)
                States.Remove(original);
        }
        return true;
    }

    // Null when this Harmony version keeps it elsewhere; unpatching then goes the slower, ordinary way.
    private static object Internal(Type type, string field)
    {
        try
        {
            return type == null ? null : AccessTools.Field(type, field)?.GetValue(null);
        }
        catch (Exception)
        {
            return null;
        }
    }
}
