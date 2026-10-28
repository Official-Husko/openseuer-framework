using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using ObenLoader;

namespace ObenFramework.Patching;

public static partial class ModPatches
{
    /// <summary>
    /// Applies the mod's [HarmonyPatch] classes area by area (<see cref="PatchAreas.AreaOf"/>): a class that
    /// fails switches off its area, taking the area's other classes off again, and the rest are still
    /// applied. Classes with a [HarmonyPatchCategory] are left to their <c>ModFeature</c>. For this to
    /// replace the loader's all-or-nothing patching, set <c>"autoPatch": false</c> in mod.json and call it
    /// first thing in <c>OnLoad</c>. Returns how many classes failed.
    /// </summary>
    public static int ApplyClasses(ObenMod mod)
    {
        if (mod == null)
            throw new ArgumentNullException(nameof(mod));
        Track(mod);
        string owner = mod.Info.Id;
        string root = mod.GetType().Namespace;
        var harmony = mod.Harmony;
        int failed = 0;

        foreach (var assembly in mod.Info.Assemblies)
        {
            foreach (var type in AccessTools.GetTypesFromAssembly(assembly))
            {
                if (!IsPatchClass(type))
                    continue;
                string area = PatchAreas.AreaOf(type, root);
                if (PatchAreas.Book.IsDisabled(owner, area))
                    continue;
                if (!ApplyClass(mod, harmony, type, area))
                    failed++;
            }
        }
        return failed;
    }

    private static bool IsPatchClass(Type type)
    {
        if (type == null || type.IsDefined(typeof(HarmonyPatchCategory), true))
            return false;
        try
        {
            var attributes = HarmonyMethodExtensions.GetFromType(type);
            return attributes != null && attributes.Count > 0;
        }
        catch (Exception)
        {
            return false; // A type whose attributes can't be read isn't one we can patch with either.
        }
    }

    private static bool ApplyClass(ObenMod mod, Harmony harmony, Type type, string area)
    {
        try
        {
            harmony.CreateClassProcessor(type).Patch();
            PatchAreas.Book.Applied(mod.Info.Id, area, () => TakeOff(harmony, type));
            return true;
        }
        catch (Exception e)
        {
            // A class can fail half way through its targets; take off what it did patch.
            try { TakeOff(harmony, type); }
            catch (Exception undo) { mod.Log.Warn($"Taking {type.FullName}'s patches off failed: {undo.Message}"); }
            Fail(mod, area, type.FullName, PatchErrors.Describe(e), e);
            return false;
        }
    }

    /// <summary>
    /// Removes the patches whose handlers are declared in <paramref name="type"/>. Looks at what is really
    /// patched instead of resolving the class's targets again (Harmony's own unpatch would wrap targets
    /// that were never patched).
    /// </summary>
    private static void TakeOff(Harmony harmony, Type type)
    {
        var patched = new List<MethodBase>(harmony.GetPatchedMethods());
        foreach (var original in patched)
        {
            var info = Harmony.GetPatchInfo(original);
            if (info == null)
                continue;
            Remove(harmony, original, info.Prefixes, type);
            Remove(harmony, original, info.Postfixes, type);
            Remove(harmony, original, info.Transpilers, type);
            Remove(harmony, original, info.Finalizers, type);
        }
    }

    private static void Remove(Harmony harmony, MethodBase original, IEnumerable<HarmonyLib.Patch> patches, Type type)
    {
        List<MethodInfo> ours = null;
        foreach (var patch in patches)
        {
            if (patch.owner == harmony.Id && patch.PatchMethod != null && patch.PatchMethod.DeclaringType == type)
                (ours ??= new List<MethodInfo>()).Add(patch.PatchMethod);
        }
        if (ours == null)
            return;
        foreach (var handler in ours)
            harmony.Unpatch(original, handler);
    }
}
