// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
using System;
using System.Collections.Generic;
using ObenFramework.Items;
using ObenLoader.Runtime;

namespace ObenFramework.Crafting;

/// <summary>
/// Hides recipes from the station windows' recipe lists. A filter gets each recipe the window is about to
/// list, with the station, and returns false to hide it; a recipe shows only if every filter keeps it.
/// Use it for search boxes, "only what I can make" toggles or hiding recipes a mod replaces. Selecting with a
/// controller follows the filtered list. Filters are removed when their mod unloads.
/// </summary>
public static class RecipeFilter
{
    private static readonly List<Func<RecipeContext, bool>> Filters = new List<Func<RecipeContext, bool>>();
    private static readonly Action<Exception> Failed = e => ItemsLog.Failed("A recipe filter", e);

    /// <summary>True while at least one filter is registered.</summary>
    public static bool Active => Filters.Count > 0;

    /// <summary>Adds a filter until <paramref name="owner"/> unloads or the returned object is disposed.</summary>
    public static IDisposable Add(ITracker owner, Func<RecipeContext, bool> keep)
    {
        if (owner == null)
            throw new ArgumentNullException(nameof(owner));
        if (keep == null)
            throw new ArgumentNullException(nameof(keep));

        Filters.Add(keep);
        var registration = Disposable.Create(() => Filters.Remove(keep));
        owner.Track(registration);
        return registration;
    }

    /// <summary>True when the recipe's name contains the text, ignoring case; an empty text matches all. Handy for search filters.</summary>
    public static bool NameContains(Recipe recipe, string text) => recipe != null && RecipeText.Contains(recipe.Name, text);

    /// <summary>
    /// Makes the open station window build its recipe list again, for example after a filter's search text
    /// changed. Does nothing when no window is open.
    /// </summary>
    public static void Refresh()
    {
        var station = CraftingBase.currentCraftingBase;
        if (station == null)
            return;
        try
        {
            station.CheckItems(true);
        }
        catch (Exception e)
        {
            ItemsLog.Failed("RecipeFilter.Refresh", e);
        }
    }

    internal static bool Keep(Recipe recipe, CraftingBase station) =>
        Filters.Count == 0 || RecipeText.KeepAll(Filters, new RecipeContext(recipe, station), Failed);
}

/// <summary>A recipe a station window is about to list, handed to <see cref="RecipeFilter"/> filters.</summary>
public readonly struct RecipeContext
{
    public RecipeContext(Recipe recipe, CraftingBase station)
    {
        Recipe = recipe;
        Station = station;
    }

    public Recipe Recipe { get; }

    /// <summary>The station whose window lists it.</summary>
    public CraftingBase Station { get; }

    public int RecipeId => Recipe != null ? Recipe.ID : -1;

    public string Name => Recipe != null ? Recipe.Name : null;

    /// <summary>The player has found or learned it.</summary>
    public bool Known => Recipes.IsKnown(Recipe);
}
