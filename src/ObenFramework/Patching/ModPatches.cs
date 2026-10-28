// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using ObenLoader;
using ObenLoader.Runtime;

namespace ObenFramework.Patching;

/// <summary>
/// Patching from code, through the mod's own Harmony instance (so the loader takes everything off when the
/// mod unloads). Methods are found by type, name and argument types; handlers by their type and name. A
/// failed patch returns false and is logged instead of throwing. Give patches an <c>area</c> to group
/// them: when one patch of an area fails, the area's other patches come off too and its later ones are
/// skipped (see <see cref="PatchAreas"/>), so a feature never runs half patched.
/// </summary>
/// <example>
/// <code>
/// ModPatches.Postfix(this, typeof(MarketShop), "Pay", null, typeof(MyHooks), nameof(MyHooks.AfterPay), area: "checkout");
/// if (!PatchAreas.IsAvailable(this, "checkout")) Log.Warn("Checkout bonus is off on this game version.");
/// </code>
/// </example>
public static partial class ModPatches
{
    private const int InheritPriority = -1;

    private static readonly HashSet<string> Tracked = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

    /// <summary>Adds a static prefix. Without <paramref name="args"/> the method name must be unique on the type.</summary>
    public static bool Prefix(ObenMod owner, Type type, string method, Type[] args, Type handlerType, string handlerName, string area = null) =>
        Named(owner, type, method, args, handlerType, handlerName, area, HarmonyPatchType.Prefix);

    /// <summary>Adds a static postfix. Without <paramref name="args"/> the method name must be unique on the type.</summary>
    public static bool Postfix(ObenMod owner, Type type, string method, Type[] args, Type handlerType, string handlerName, string area = null) =>
        Named(owner, type, method, args, handlerType, handlerName, area, HarmonyPatchType.Postfix);

    /// <summary>Adds a static finalizer. Without <paramref name="args"/> the method name must be unique on the type.</summary>
    public static bool Finalizer(ObenMod owner, Type type, string method, Type[] args, Type handlerType, string handlerName, string area = null) =>
        Named(owner, type, method, args, handlerType, handlerName, area, HarmonyPatchType.Finalizer);

    /// <summary>Adds a static transpiler. Without <paramref name="args"/> the method name must be unique on the type.</summary>
    public static bool Transpiler(ObenMod owner, Type type, string method, Type[] args, Type handlerType, string handlerName, string area = null) =>
        Named(owner, type, method, args, handlerType, handlerName, area, HarmonyPatchType.Transpiler);

    /// <summary>
    /// Patches <paramref name="target"/> with any of the handlers (null ones are skipped). Returns false
    /// when the patch failed or its area is already switched off. <paramref name="priority"/> -1 keeps
    /// Harmony's default or the handler's own [HarmonyPriority].
    /// </summary>
    public static bool Patch(ObenMod owner, MethodBase target, MethodInfo prefix = null, MethodInfo postfix = null,
        MethodInfo transpiler = null, MethodInfo finalizer = null, string area = null, int priority = InheritPriority)
    {
        if (owner == null)
            throw new ArgumentNullException(nameof(owner));
        string what = target == null ? "a missing method" : MethodFinder.Describe(target.DeclaringType, target.Name, null);
        if (area != null && PatchAreas.Book.IsDisabled(owner.Info.Id, area))
            return false;
        if (target == null)
            return Fail(owner, area, what, "the method doesn't exist", null);

        Track(owner);
        var harmony = owner.Harmony;
        try
        {
            harmony.Patch(target, Wrap(prefix, priority), Wrap(postfix, priority), Wrap(transpiler, priority), Wrap(finalizer, priority));
        }
        catch (Exception e)
        {
            Undo(harmony, target, prefix, postfix, transpiler, finalizer);
            return Fail(owner, area, what, PatchErrors.Describe(e), e);
        }
        if (area != null)
            PatchAreas.Book.Applied(owner.Info.Id, area, () => Undo(harmony, target, prefix, postfix, transpiler, finalizer));
        return true;
    }

    /// <summary>
    /// Switches one of the mod's areas off on purpose (a check of its own failed, say): its patches come
    /// off and later ones are skipped until the mod reloads.
    /// </summary>
    public static void DisableArea(ObenMod owner, string area, string reason)
    {
        if (owner == null)
            throw new ArgumentNullException(nameof(owner));
        Track(owner);
        if (PatchAreas.Book.Fail(owner.Info.Id, area, reason, e => owner.Log.Warn($"Taking a patch of '{area}' off failed: {e.Message}")))
            owner.Log.Warn($"The '{area}' patches are switched off: {reason}");
    }

    private static bool Named(ObenMod owner, Type type, string method, Type[] args, Type handlerType, string handlerName, string area, HarmonyPatchType kind)
    {
        if (owner == null)
            throw new ArgumentNullException(nameof(owner));
        if (area != null && PatchAreas.Book.IsDisabled(owner.Info.Id, area))
            return false;

        MethodInfo handler;
        try
        {
            handler = MethodFinder.Handler(handlerType, handlerName);
        }
        catch (Exception e)
        {
            return Fail(owner, area, $"handler {handlerType?.Name}.{handlerName}", "the handler doesn't exist or isn't static", e);
        }

        var target = MethodFinder.Find(type, method, args, out string problem);
        if (target == null)
            return Fail(owner, area, MethodFinder.Describe(type, method, args), problem, null);

        return kind switch
        {
            HarmonyPatchType.Prefix => Patch(owner, target, prefix: handler, area: area),
            HarmonyPatchType.Postfix => Patch(owner, target, postfix: handler, area: area),
            HarmonyPatchType.Transpiler => Patch(owner, target, transpiler: handler, area: area),
            _ => Patch(owner, target, finalizer: handler, area: area),
        };
    }

    private static bool Fail(ObenMod owner, string area, string what, string reason, Exception e)
    {
        Track(owner);
        string message = $"Patching {what} failed: {reason}";
        if (area == null)
        {
            owner.Log.Error(message, e);
            return false;
        }
        bool first = PatchAreas.Book.Fail(owner.Info.Id, area, $"{what}: {reason}",
            undoError => owner.Log.Warn($"Taking a patch of '{area}' off failed: {undoError.Message}"));
        owner.Log.Error(first ? $"{message}. The '{area}' patches are switched off; the rest of the mod keeps running." : message, e);
        return false;
    }

    // Only handlers that are really on the method: unpatching a method that was never patched would make
    // Harmony build a wrapper for it anyway.
    private static void Undo(Harmony harmony, MethodBase target, params MethodInfo[] handlers)
    {
        var info = Harmony.GetPatchInfo(target);
        if (info == null)
            return;
        foreach (var handler in handlers)
        {
            if (handler != null && (Has(info.Prefixes, handler) || Has(info.Postfixes, handler) || Has(info.Transpilers, handler) || Has(info.Finalizers, handler)))
                harmony.Unpatch(target, handler);
        }
    }

    private static bool Has(IEnumerable<HarmonyLib.Patch> patches, MethodInfo handler)
    {
        foreach (var patch in patches)
        {
            if (patch.PatchMethod == handler)
                return true;
        }
        return false;
    }

    private static HarmonyMethod Wrap(MethodInfo method, int priority)
    {
        if (method == null)
            return null;
        var wrapped = new HarmonyMethod(method);
        if (priority != InheritPriority)
            wrapped.priority = priority;
        return wrapped;
    }

    // The bookkeeping goes when the mod unloads; the loader takes the patches off by Harmony id.
    private static void Track(ObenMod owner)
    {
        string id = owner.Info.Id;
        if (!Tracked.Add(id))
            return;
        owner.Track(Disposable.Create(() =>
        {
            Tracked.Remove(id);
            PatchAreas.Book.Forget(id);
        }));
    }
}
