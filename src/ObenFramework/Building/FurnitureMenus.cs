// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;

namespace ObenFramework.Building;

/// <summary>
/// Makes custom furniture known to the developer console's furniture list (which <c>add_furniture</c>,
/// <c>find_furniture</c> and the framework's own furniture lookups read), so a defined piece can be added by
/// its title like any other. Built pieces are added on the console's next use; pieces whose mod unloaded are
/// taken out again.
/// </summary>
internal static class FurnitureMenus
{
    private static readonly List<global::Furniture> Added = new List<global::Furniture>();
    private static bool _dirty = true;

    public static void Changed() => _dirty = true;

    /// <summary>Brings the console's list in line with the defined pieces. Builds them, so call it when they're wanted.</summary>
    public static void Sync()
    {
        var console = DeveloperConsole.Instance;
        var list = console != null && console.furniture != null ? console.furniture.furnitures : null;
        if (list == null)
            return;

        foreach (var old in Added)
            list.Remove(old);
        Added.Clear();
        foreach (var entry in FurnitureRegistry.All)
        {
            var built = FurnitureBuild.Registered(entry);
            if (built != null && !list.Contains(built))
            {
                list.Add(built);
                Added.Add(built);
            }
        }
        _dirty = false;
        World.FurnitureKinds.Reset();
    }

    public static void Reset()
    {
        var console = DeveloperConsole.Instance;
        var list = console != null && console.furniture != null ? console.furniture.furnitures : null;
        if (list != null)
        {
            foreach (var old in Added)
                list.Remove(old);
        }
        Added.Clear();
        _dirty = true;
    }

    [HarmonyPatch]
    private static class ConsolePatch
    {
        [HarmonyTargetMethods]
        private static IEnumerable<MethodBase> Targets()
        {
            yield return AccessTools.Method(typeof(DeveloperConsoleFurniture), nameof(DeveloperConsoleFurniture.GetFurniture));
            yield return AccessTools.Method(typeof(DeveloperConsoleFurniture), nameof(DeveloperConsoleFurniture.AddAllFurniture));
            yield return AccessTools.Method(typeof(DeveloperConsoleFurniture), nameof(DeveloperConsoleFurniture.FindFurniture));
        }

        [HarmonyPrefix]
        private static void Before()
        {
            if (!_dirty)
                return;
            try { Sync(); }
            catch (Exception e) { FurnitureRegistry.Log.Error("Console furniture hook", e); }
        }
    }
}
