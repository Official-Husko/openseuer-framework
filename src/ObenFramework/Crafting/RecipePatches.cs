// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
using System;
using HarmonyLib;
using ObenFramework.Items;
using ObenFramework.World;

namespace ObenFramework.Crafting;

/// <summary>
/// The recipe list hooks: <see cref="RecipeFilter"/> skips buttons as the window builds its list, and
/// selection by index (controller navigation) is pointed at the filtered list. Also raises
/// <see cref="Recipes.Unlocked"/>.
/// </summary>
[HarmonyPatch]
internal static class RecipePatches
{
    [HarmonyPrefix]
    [HarmonyPatch(typeof(RecipeListUI), nameof(RecipeListUI.CreateButton))]
    private static bool CreatingButton(Recipe recipe, CraftingBase activeCraftingBase)
    {
        if (recipe == null || !RecipeFilter.Active)
            return true;
        try { return RecipeFilter.Keep(recipe, activeCraftingBase); }
        catch (Exception e) { ItemsLog.Failed("Recipe filter hook", e); return true; }
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(CraftingBase), nameof(CraftingBase.SetSelectedRecipe), new[] { typeof(string) })]
    private static bool SelectingByIndex(CraftingBase __instance, string index) => Redirect(__instance, index);

    [HarmonyPrefix]
    [HarmonyPatch(typeof(manufacturingProcess), nameof(manufacturingProcess.SetSelectedRecipe), new[] { typeof(string) })]
    private static bool ProcessSelectingByIndex(manufacturingProcess __instance, string index) => Redirect(__instance, index);

    // A dishwasher's craft makes up a recipe (id -1) and the game files it as learned; that's no recipe to report.
    [HarmonyPostfix]
    [HarmonyPatch(typeof(RecipeController), nameof(RecipeController.AddRecipe), new[] { typeof(Recipe), typeof(bool), typeof(bool) })]
    private static void RecipeAdded(Recipe recipe, bool learned, bool __result)
    {
        if (!__result || recipe == null || recipe.ID < 0 || !Recipes.Unlocked.HasSubscribers || !WorldChanges.Active)
            return;
        try { Recipes.Unlocked.Raise(new RecipeUnlock(recipe.ID, recipe.Name, learned)); }
        catch (Exception e) { ItemsLog.Failed("Recipe unlocked hook", e); }
    }

    // The index counts the buttons on screen; with a filter they are not the station's own recipe list.
    private static bool Redirect(CraftingBase station, string index)
    {
        if (station == null || !RecipeFilter.Active || !int.TryParse(index, out int number))
            return true;
        try
        {
            var list = VisibleList(station);
            var visible = list != null ? list.currentRecipes : null;
            if (visible == null || number < 0 || number >= visible.Count || visible[number] == null)
                return true;
            station.SetSelectedRecipe(visible[number]);
            return false;
        }
        catch (Exception e)
        {
            ItemsLog.Failed("Recipe selection hook", e);
            return true;
        }
    }

    private static RecipeListUI VisibleList(CraftingBase station)
    {
        if (station is Growing growing)
        {
            var panel = GrowingPanel.instance;
            return panel != null && panel.ActiveManu == growing && panel.growingPanelUI != null ? panel.growingPanelUI.recipeListUI : null;
        }
        if (station is manufacturingProcess process)
        {
            var panel = ManufacturingProcessPanel.instance;
            return panel != null && panel.ActiveManu == process && panel.processPanelUI != null ? panel.processPanelUI.recipeListUI : null;
        }
        if (station is manufacturing manufacturing)
        {
            var panel = ManufacturingPanel.instance;
            return panel != null && panel.ActiveManu == manufacturing && panel.manufacturingPanelUI != null ? panel.manufacturingPanelUI.recipeListUI : null;
        }
        return null;
    }
}
