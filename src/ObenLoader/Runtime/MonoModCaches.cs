using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using ObenLoader.Logging;

namespace ObenLoader.Runtime;

/// <summary>
/// Empties MonoMod's cache of resolved types whenever a mod's new copy loads, so patches on the new copy use
/// its own types.
/// </summary>
/// <remarks>
/// To build a patched method, Harmony (through MonoMod) turns every type the original uses into a Cecil
/// reference and back into a runtime type, and MonoMod's <c>ReflectionHelper</c> caches that last step by the
/// type's full name and the assembly of its outer type. For a generic type over a mod's own type,
/// <c>List&lt;RelationshipRecord&gt;</c> say, that assembly is mscorlib, so every copy hot reload leaves behind
/// gets the same key, and a patch on the new copy is built with the first copy's list: adding the new copy's
/// RelationshipRecord to it throws ArrayTypeMismatchException. Game methods only use the game's own types, so
/// this only hits patches on mods' methods (the API tracer patches the framework's). After the cache is emptied
/// MonoMod resolves again, each type argument by its own assembly, and caches the new copy's types.
/// </remarks>
internal static class MonoModCaches
{
    private static Dictionary<string, WeakReference> _resolved;

    public static void Use(ModLogger log)
    {
        var helper = typeof(Harmony).Assembly.GetType("MonoMod.Utils.ReflectionHelper");
        var field = helper?.GetField("ResolveReflectionCache", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
        _resolved = field?.GetValue(null) as Dictionary<string, WeakReference>;
        if (_resolved == null)
            log.Warn("Couldn't find MonoMod's type cache; patches on a mod's own methods may break after it hot reloads.");
    }

    /// <summary>Called when a mod's new copy has loaded.</summary>
    public static void Forget()
    {
        var resolved = _resolved;
        if (resolved == null)
            return;
        lock (resolved)
            resolved.Clear();
    }
}
