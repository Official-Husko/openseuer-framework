// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
using System;
using System.Collections.Generic;
using HarmonyLib;

namespace ObenFramework.Building;

/// <summary>
/// Build menu groups for custom furniture. A game group is used by its name ("Chairs"); any other name gets a
/// new group, numbered from 1000 so it can't clash with the game's, and named through a patch on the game's
/// group name lookup (which would otherwise fail on a number it has no description for).
/// </summary>
public static class FurnitureCategories
{
    private const int FirstCustom = 1000;
    private static readonly List<string> Names = new List<string>();

    /// <summary>The build menu group for a name; <paramref name="fallback"/> when the name is empty.</summary>
    public static global::Furniture.Category For(string name, global::Furniture.Category fallback)
    {
        if (string.IsNullOrWhiteSpace(name))
            return fallback;
        foreach (global::Furniture.Category value in Enum.GetValues(typeof(global::Furniture.Category)))
        {
            if (string.Equals(value.ToString(), name, StringComparison.OrdinalIgnoreCase) || string.Equals(GameName(value), name, StringComparison.OrdinalIgnoreCase))
                return value;
        }
        int index = Names.FindIndex(n => string.Equals(n, name, StringComparison.OrdinalIgnoreCase));
        if (index < 0)
        {
            Names.Add(name.Trim());
            index = Names.Count - 1;
        }
        return (global::Furniture.Category)(FirstCustom + index);
    }

    /// <summary>The name shown for a group, the game's or a mod's.</summary>
    public static string NameOf(global::Furniture.Category category)
    {
        int index = (int)category - FirstCustom;
        return index >= 0 && index < Names.Count ? Names[index] : GameName(category);
    }

    /// <summary>True for groups made by mods.</summary>
    public static bool IsCustom(global::Furniture.Category category) => (int)category >= FirstCustom;

    internal static void Reset() => Names.Clear();

    private static string GameName(global::Furniture.Category category)
    {
        try { return BuildingSystemSlot.GetCategoryName(category); }
        catch (Exception) { return category.ToString(); }
    }

    [HarmonyPatch(typeof(BuildingSystemSlot), nameof(BuildingSystemSlot.GetCategoryName))]
    private static class NamePatch
    {
        [HarmonyPrefix]
        private static bool Before(global::Furniture.Category category, ref string __result)
        {
            int index = (int)category - FirstCustom;
            if (index < 0)
                return true;
            __result = index < Names.Count ? Names[index] : "Mods";
            return false;
        }
    }
}
