// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
using System;
using System.Collections.Generic;
using ObenFramework.Items;
using ObenFramework.Saves;
using ObenFramework.World;

namespace ObenFramework.Crafting;

/// <summary>
/// Reads the game's recipes (vanilla and those added through <see cref="RecipeRegistry"/>): finding them,
/// what the player knows, whether something can be crafted, and giving the player a recipe. Nothing here
/// writes the game's Recipes.json.
/// </summary>
public static class Recipes
{
    /// <summary>
    /// The framework's crafting hooks are in place. False when they failed to apply (a game update changed what
    /// they patch); the events then stay quiet and what this class changes may not take.
    /// </summary>
    public static bool IsAvailable => CraftingModule.HooksInPlace;

    /// <summary>
    /// The player found or learned a recipe. Not raised while a save loads, nor for the recipes a dishwasher
    /// makes up for each wash (id -1), which the game also files as learned.
    /// </summary>
    public static readonly WorldEvent<RecipeUnlock> Unlocked = new WorldEvent<RecipeUnlock>(nameof(Unlocked));

    /// <summary>Every recipe the game has.</summary>
    public static List<RecipeInfo> All()
    {
        var list = new List<RecipeInfo>();
        foreach (var recipe in Database())
        {
            if (recipe != null)
                list.Add(new RecipeInfo(recipe, IsKnown(recipe)));
        }
        return list;
    }

    /// <summary>The recipe with this id, or null.</summary>
    public static Recipe ById(int recipeId)
    {
        foreach (var recipe in Database())
        {
            if (recipe != null && recipe.ID == recipeId)
                return recipe;
        }
        return null;
    }

    public static RecipeInfo Get(int recipeId)
    {
        var recipe = ById(recipeId);
        return recipe != null ? new RecipeInfo(recipe, IsKnown(recipe)) : null;
    }

    /// <summary>The recipes that make an item (in their normal output).</summary>
    public static List<RecipeInfo> Making(int itemId) => Matching(r => Lists(r.output, itemId));

    /// <summary>The recipes that use an item as an ingredient.</summary>
    public static List<RecipeInfo> Using(int itemId) => Matching(r => Lists(r.input, itemId));

    /// <summary>Recipes whose name contains the text, ignoring case.</summary>
    public static List<RecipeInfo> Search(string text) => Matching(r => RecipeText.Contains(r.Name, text));

    /// <summary>The player has found or learned the recipe.</summary>
    public static bool IsKnown(Recipe recipe)
    {
        var controller = RecipeController.instance;
        return recipe != null && controller != null && (controller.HasRecipe(recipe) || controller.HasLearnedRecipe(recipe));
    }

    /// <summary>The player carries everything the recipe takes (inventory grid, bag and equipment).</summary>
    public static bool CanCraftFromInventory(int recipeId)
    {
        var recipe = ById(recipeId);
        if (recipe == null || Inventory.instance == null)
            return false;
        if (recipe.Input == null)
            recipe.SetItems();
        var slots = PlayerSlots.Current();
        var stacks = new ItemStack[slots.Count];
        for (int i = 0; i < stacks.Length; i++)
            stacks[i] = slots[i].itemStack;
        return CraftingBaseHelperFunctions.StorageHasRequiredItems(recipe, stacks, out _);
    }

    /// <summary>The station lists this recipe: it crafts its type and provides the conditions it needs.</summary>
    public static bool CanCraftAt(int recipeId, CraftingBase station)
    {
        var recipe = ById(recipeId);
        return recipe != null && station != null && Array.IndexOf(station.RecipesWithUpgrades, recipe.Type) >= 0
            && RecipeInfoUI.CheckModifiers(recipe, station.ModifiersWithUpgrades);
    }

    /// <summary>The player's skill is at the recipe's minimum.</summary>
    public static bool MeetsSkill(int recipeId)
    {
        var recipe = ById(recipeId);
        return recipe != null && PlayerStats.instance != null && RecipeInfoUI.CheckSkillSoft(recipe);
    }

    /// <summary>
    /// Gives the player a recipe as if they found it (it shows in their recipe list and in stations).
    /// Returns false when they already had it, it doesn't exist, or a save is loading.
    /// </summary>
    public static bool Unlock(int recipeId, bool notify = true)
    {
        var recipe = ById(recipeId);
        var controller = RecipeController.instance;
        if (recipe == null || controller == null || TaskItemsManager.instance == null || SaveGate.IsBusy)
            return false;
        using (WorldChanges.EnterMod())
            return controller.AddRecipe(recipe, false, notify);
    }

    private static List<Recipe> Database() => RecipeDatabase.Recipes ?? new List<Recipe>();

    private static List<RecipeInfo> Matching(Func<Recipe, bool> keep)
    {
        var list = new List<RecipeInfo>();
        foreach (var recipe in Database())
        {
            if (recipe != null && keep(recipe))
                list.Add(new RecipeInfo(recipe, IsKnown(recipe)));
        }
        return list;
    }

    private static bool Lists(Dictionary<ItemReference, float> items, int itemId)
    {
        if (items == null)
            return false;
        foreach (var reference in items.Keys)
        {
            if (reference != null && reference.ID == itemId)
                return true;
        }
        return false;
    }
}
