// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using ObenLoader.Logging;

namespace ObenLoader;

/// <summary>
/// Every patch the loader and the framework put on, as it happens, for tools that follow patching (the API
/// tracer adds new handlers to a running trace with it). Reported are the loader's patching of a mod's
/// [HarmonyPatch] classes, ObenFramework's <c>ModPatches</c> and the patches of switchable features. Mods
/// that call Harmony themselves can report theirs with <see cref="Report"/>.
/// </summary>
public static class PatchEvents
{
    private static readonly ModLogger Log = new ModLogger("ObenLoader");

    /// <summary>
    /// Raised after each patch went on or failed, on the thread that patched (the main thread for mods).
    /// Handlers should be quick; one that throws is logged and the others still run.
    /// </summary>
    public static event Action<PatchReport> Installed;

    /// <summary>True when something listens, so reporters can skip building reports nobody reads.</summary>
    public static bool HasListeners => Installed != null;

    public static void Report(PatchReport report)
    {
        var listeners = Installed;
        if (listeners == null || report == null)
            return;
        foreach (Action<PatchReport> listener in listeners.GetInvocationList())
        {
            try
            {
                listener(report);
            }
            catch (Exception e)
            {
                Log.Error("A patch listener failed", e);
            }
        }
    }

    /// <summary>
    /// Reports what a patch class put on, from the replacements <c>CreateClassProcessor(type).Patch()</c>
    /// returned: one report per method, with the handlers of <paramref name="patchClass"/> on it.
    /// </summary>
    public static void ReportClass(string owner, Type patchClass, IEnumerable<MethodInfo> replacements)
    {
        if (!HasListeners || patchClass == null || replacements == null)
            return;
        foreach (var replacement in replacements)
        {
            var original = replacement == null ? null : Harmony.GetOriginalMethod(replacement);
            var info = original == null ? null : Harmony.GetPatchInfo(original);
            if (info == null)
                continue;
            Report(new PatchReport(owner, Describe(original), original, From(info.Prefixes, owner, patchClass),
                From(info.Postfixes, owner, patchClass), From(info.Transpilers, owner, patchClass), From(info.Finalizers, owner, patchClass)));
        }
    }

    /// <summary>
    /// The handlers of every patch on right now, or only those of <paramref name="owner"/> (a Harmony id).
    /// Read from Harmony itself, so it stays right across hot reloads.
    /// </summary>
    public static List<MethodInfo> InstalledHandlers(string owner = null)
    {
        var handlers = new List<MethodInfo>();
        var seen = new HashSet<MethodInfo>();
        foreach (var original in new List<MethodBase>(Harmony.GetAllPatchedMethods()))
        {
            var info = Harmony.GetPatchInfo(original);
            if (info == null)
                continue;
            Collect(handlers, seen, info.Prefixes, owner);
            Collect(handlers, seen, info.Postfixes, owner);
            Collect(handlers, seen, info.Transpilers, owner);
            Collect(handlers, seen, info.Finalizers, owner);
        }
        return handlers;
    }

    /// <summary>"Type.Method" for reports.</summary>
    public static string Describe(MethodBase method) =>
        method == null ? "a missing method" : (method.DeclaringType?.FullName ?? "?") + "." + method.Name;

    private static MethodInfo From(IEnumerable<Patch> patches, string owner, Type patchClass)
    {
        foreach (var patch in patches)
        {
            if (patch.owner == owner && patch.PatchMethod?.DeclaringType == patchClass)
                return patch.PatchMethod;
        }
        return null;
    }

    private static void Collect(List<MethodInfo> into, HashSet<MethodInfo> seen, IEnumerable<Patch> patches, string owner)
    {
        foreach (var patch in patches)
        {
            if ((owner == null || patch.owner == owner) && patch.PatchMethod != null && seen.Add(patch.PatchMethod))
                into.Add(patch.PatchMethod);
        }
    }
}
