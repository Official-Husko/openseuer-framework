using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using ObenLoader.Logging;

namespace ObenLoader.Runtime;

/// <summary>
/// Makes Harmony find patch handlers without walking every loaded assembly.
/// </summary>
/// <remarks>
/// Harmony keeps each method's patches serialized and reads them back for every patch and unpatch, so it
/// looks every handler up again each time: <c>Patch.PatchMethod</c> goes through all loaded modules,
/// formatting each module's id as a string, until it finds the handler's module, then resolves the token.
/// Hot reload leaves the old copies of every mod loaded, so after a few dozen reloads that walk covers
/// hundreds of assemblies and runs thousands of times per reload; it was half of what made reloads slower
/// and slower. This keeps the modules by id (adding assemblies it hasn't seen when an id is missing) and the
/// handlers by module and token, and fills the field Harmony would otherwise search for.
/// </remarks>
internal static class PatchLookups
{
    private const string HarmonyId = "ObenLoader.PatchLookups";

    private static readonly object Gate = new object();
    private static readonly Dictionary<string, Module> Modules = new Dictionary<string, Module>(StringComparer.Ordinal);
    private static readonly HashSet<Assembly> Indexed = new HashSet<Assembly>();
    private static readonly Dictionary<(string, int), MethodInfo> Handlers = new Dictionary<(string, int), MethodInfo>();

    public static void Use(ModLogger log)
    {
        try
        {
            var getter = AccessTools.PropertyGetter(typeof(Patch), nameof(Patch.PatchMethod));
            if (getter == null || AccessTools.Field(typeof(Patch), "patchMethod") == null
                || AccessTools.Field(typeof(Patch), "methodToken") == null || AccessTools.Field(typeof(Patch), "moduleGUID") == null)
            {
                log.Warn("Couldn't find how Harmony looks up patch handlers; hot reload will get slower the longer the game runs.");
                return;
            }
            new Harmony(HarmonyId).Patch(getter, prefix: new HarmonyMethod(typeof(PatchLookups), nameof(FillHandler)));
        }
        catch (Exception e)
        {
            log.Error("Couldn't speed up Harmony's handler lookups", e);
        }
    }

    // Leaves the field empty when the handler can't be found, so Harmony's own search runs and reports it.
    private static void FillHandler(ref MethodInfo ___patchMethod, int ___methodToken, string ___moduleGUID)
    {
        if ((object)___patchMethod != null || ___moduleGUID == null)
            return;
        try
        {
            ___patchMethod = Resolve(___moduleGUID, ___methodToken);
        }
        catch (Exception)
        {
        }
    }

    private static MethodInfo Resolve(string moduleId, int token)
    {
        lock (Gate)
        {
            if (Handlers.TryGetValue((moduleId, token), out var known))
                return known;
            if (!Modules.TryGetValue(moduleId, out var module))
            {
                IndexNewAssemblies();
                if (!Modules.TryGetValue(moduleId, out module))
                    return null;
            }
            var method = module.ResolveMethod(token) as MethodInfo;
            if (method != null)
                Handlers[(moduleId, token)] = method;
            return method;
        }
    }

    private static void IndexNewAssemblies()
    {
        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            if (!Indexed.Add(assembly))
                continue;
            try
            {
                foreach (var module in assembly.GetLoadedModules())
                    Modules[module.ModuleVersionId.ToString()] = module;
            }
            catch (Exception)
            {
                // Some dynamic assemblies can't list their modules; Harmony never puts handlers there.
            }
        }
    }
}
