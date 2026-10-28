using System;
using System.Collections.Generic;
using HarmonyLib;
using ObenLoader;
using ObenLoader.Runtime;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ObenFramework.Items;

/// <summary>
/// Adds crafting recipes to the game. A recipe goes into <see cref="RecipeDatabase.Recipes"/> once the
/// game's recipes and every item it mentions exist, shows up in crafting stations that are already
/// running, and is removed when the owning mod unloads. The game's Recipes.json is never written.
/// </summary>
/// <remarks>
/// A recipe must use one of the game's <see cref="RecipeType"/> values; stations only list the types
/// they were built for. Use <see cref="ReservedIds.For"/> for the id. A recipe with
/// <c>RecipeRequired</c> set is hidden until the player learns it (<c>RecipeController.AddRecipe</c>).
/// </remarks>
public static class RecipeRegistry
{
    private static readonly Action<CraftingBase> UpdateStationRecipes =
        AccessTools.MethodDelegate<Action<CraftingBase>>(AccessTools.Method(typeof(CraftingBase), "UpdateRecipes"));

    private static readonly List<Recipe> Entries = new List<Recipe>();
    private static readonly HashSet<Recipe> Warned = new HashSet<Recipe>();
    private static object _itemsAppliedTo;

    public static IReadOnlyList<Recipe> Recipes => Entries;

    /// <summary>Registers a recipe until <paramref name="owner"/> unloads. Throws when the recipe is invalid or its id is taken.</summary>
    public static Recipe Register(ObenMod owner, Recipe recipe)
    {
        Validate(recipe);
        Entries.Add(recipe);
        owner.Track(Disposable.Create(() => Unregister(recipe)));
        Sync();
        return recipe;
    }

    private static bool IsReady =>
        RecipeDatabase.instance != null && RecipeDatabase.Recipes.Count > 0 && ItemDatabase.instance != null && ItemLookup.IsLoaded;

    /// <summary>
    /// Makes the game's recipe list match the registered recipes: adds the ones that can work now,
    /// removes the ones whose items are gone, and refreshes running stations when something changed.
    /// </summary>
    internal static void Sync()
    {
        if (!IsReady)
            return;

        var list = RecipeDatabase.Recipes;
        bool changed = false;
        foreach (var recipe in Entries)
        {
            bool present = list.Contains(recipe);
            bool valid = ItemsExist(recipe);
            if (valid && !present)
            {
                recipe.Appearance?.LoadSprite();
                recipe.SetItems();
                list.Add(recipe);
                changed = true;
            }
            else if (!valid && present)
            {
                list.Remove(recipe);
                changed = true;
            }
            else if (!valid && Warned.Add(recipe))
            {
                ItemsLog.Warn($"Recipe {recipe.ID} ({recipe.Name}) waits for an item it uses that doesn't exist.");
            }
        }

        if (changed)
            RefreshStations();
    }

    /// <summary>The item database was built or reloaded: recipes must point at the new item objects.</summary>
    internal static void ItemsChanged()
    {
        if (!IsReady)
            return;

        if (!ReferenceEquals(_itemsAppliedTo, ItemDatabase.database))
        {
            _itemsAppliedTo = ItemDatabase.database;
            foreach (var recipe in Entries)
            {
                if (RecipeDatabase.Recipes.Contains(recipe) && ItemsExist(recipe))
                    recipe.SetItems();
            }
        }
        Sync();
    }

    private static void Unregister(Recipe recipe)
    {
        Entries.Remove(recipe);
        Warned.Remove(recipe);
        if (RecipeDatabase.Recipes.Remove(recipe))
            RefreshStations();
    }

    private static void Validate(Recipe recipe)
    {
        if (recipe == null)
            throw new ArgumentNullException(nameof(recipe));
        if (!ReservedIds.Contains(recipe.ID))
            throw new ArgumentException($"Recipe id {recipe.ID} is outside the range reserved for mods ({ReservedIds.Min}-{ReservedIds.Max}). Use ReservedIds.For.");
        if (Entries.Exists(r => r.ID == recipe.ID))
            throw new ArgumentException($"A recipe with id {recipe.ID} is already registered. Give '{recipe.Name}' a different key.");
        if (recipe.output == null || recipe.output.Count == 0)
            throw new ArgumentException($"Recipe {recipe.ID} ({recipe.Name}) needs at least one output.");

        // The game reads all of these without checks.
        recipe.input ??= new Dictionary<ItemReference, float>();
        recipe.outputFail ??= new Dictionary<ItemReference, float>();
        recipe.Appearance ??= new Recipe.RecipeAppearance();
        recipe.metaSettings ??= new Recipe.RecipeMetaSettigns();
        recipe.allowedNonStolenItemsIds ??= new List<int>();
        recipe.RequiredConditions ??= new List<RecipeCondition>();
        recipe.ExcludingConditions ??= new List<RecipeCondition>();
        recipe.RecipeTags ??= new List<string>();

        CheckNoRepeats(recipe, recipe.input, "input");
        CheckNoRepeats(recipe, recipe.output, "output");
        CheckNoRepeats(recipe, recipe.outputFail, "fail output");
    }

    // Recipe.SetItems adds each entry to a dictionary keyed by item and throws on a repeat.
    private static void CheckNoRepeats(Recipe recipe, Dictionary<ItemReference, float> items, string what)
    {
        var seen = new HashSet<int>();
        foreach (var reference in items.Keys)
        {
            if (reference == null || !seen.Add(reference.ID))
                throw new ArgumentException($"Recipe {recipe.ID} ({recipe.Name}) lists an item twice in its {what}. List each item once with the total amount.");
        }
    }

    private static bool ItemsExist(Recipe recipe) =>
        AllExist(recipe.input) && AllExist(recipe.output) && AllExist(recipe.outputFail);

    private static bool AllExist(Dictionary<ItemReference, float> items)
    {
        foreach (var reference in items.Keys)
        {
            if (reference == null || !ItemLookup.Exists(reference.ID))
                return false;
        }
        return true;
    }

    // UpdateRecipes is what a station runs once when it starts; running it again picks up the new list.
    private static void RefreshStations()
    {
        if (RecipeDatabase.instance == null)
            return;

        foreach (var station in Object.FindObjectsByType<CraftingBase>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            try
            {
                UpdateStationRecipes(station);
            }
            catch (Exception e)
            {
                ItemsLog.Error($"Couldn't refresh the recipes of {station.name}", e);
            }
        }
    }
}
