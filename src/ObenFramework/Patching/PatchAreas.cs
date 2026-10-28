using System;
using System.Collections.Generic;
using ObenLoader;

namespace ObenFramework.Patching;

/// <summary>
/// Patch areas: groups of a mod's Harmony patches that stand or fall together. When one patch of an area
/// fails (usually a game update renamed or removed what it hooks), the area's other patches are taken off
/// and later ones are skipped, the failure goes to the log and the Errors page, and the rest of the mod
/// keeps running. The framework's own patches are grouped like this; mods get the same through
/// <see cref="ModPatches"/>. Areas are forgotten when their mod unloads.
/// </summary>
public static class PatchAreas
{
    internal static readonly PatchAreaBook Book = new PatchAreaBook();

    /// <summary>The framework's mod id, the owner of its own areas.</summary>
    public const string Framework = "ObenFramework";

    /// <summary>False when the area's patches failed and it was switched off. Areas never used count as available.</summary>
    public static bool IsAvailable(string owner, string area) => !Book.IsDisabled(owner, area);

    /// <summary>False when the mod's area failed and was switched off.</summary>
    public static bool IsAvailable(ObenMod owner, string area) =>
        owner != null && IsAvailable(owner.Info.Id, area);

    /// <summary>Every known area of every mod, in the order they were first patched.</summary>
    public static List<PatchAreaInfo> All() => Book.Snapshot();

    /// <summary>One mod's areas.</summary>
    public static List<PatchAreaInfo> Of(string owner) => Book.Snapshot(owner ?? throw new ArgumentNullException(nameof(owner)));

    /// <summary>The areas that were switched off, of every mod.</summary>
    public static List<PatchAreaInfo> Failed()
    {
        var all = Book.Snapshot();
        all.RemoveAll(a => a.IsAvailable);
        return all;
    }

    /// <summary>
    /// The area of a patch class: its <see cref="PatchAreaAttribute"/>, or its namespace below
    /// <paramref name="rootNamespace"/> ("ObenFramework.World.Logic" under "ObenFramework" is "World.Logic"),
    /// or "Main" for classes directly in the root namespace.
    /// </summary>
    public static string AreaOf(Type type, string rootNamespace)
    {
        if (type == null)
            return "Main";
        for (var t = type; t != null; t = t.DeclaringType)
        {
            var attribute = (PatchAreaAttribute)Attribute.GetCustomAttribute(t, typeof(PatchAreaAttribute), false);
            if (attribute != null && !string.IsNullOrEmpty(attribute.Name))
                return attribute.Name;
        }
        return PatchAreaBook.AreaFromNamespace(type.Namespace, rootNamespace);
    }
}
