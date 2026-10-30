using System;
using System.Collections.Generic;
using HarmonyLib;
using ObenFramework.Patching;
using ObenLoader.Runtime;

namespace ObenFramework.Items;

/// <summary>
/// Stand-ins for recipes whose mod is gone. The game keeps a recipe's id in blueprint items, letters, recipe
/// books and the player's recipe list, and looking up an id it doesn't have throws (using such a blueprint,
/// or loading a save with such a book), or quietly turns the id into 0 when the item is copied. A lookup of a
/// missing id in the mod range (<see cref="ReservedIds"/>) instead gets a "Missing recipe" with the same id,
/// named after the mod and recipe the save recorded. It makes nothing and no station lists it; it only keeps
/// the id, so the recipe is the real one again once its mod is back. Recipes the player knew are kept the
/// same way.
/// </summary>
[PatchArea("RecipePlaceholders")]
public static class RecipePlaceholders
{
    private const string CacheKey = "ObenFramework.RecipePlaceholders.Cache";

    private static readonly Func<RecipeController, Recipe, TaskItem> CreateTaskItem =
        AccessTools.MethodDelegate<Func<RecipeController, Recipe, TaskItem>>(AccessTools.Method(typeof(RecipeController), "CreateTaskItem"));

    internal static readonly ItemManifestBook Manifest = new ItemManifestBook();
    private static readonly Dictionary<int, Recipe> Cache = new Dictionary<int, Recipe>();

    /// <summary>The stand-in hook is in place (its patch didn't fail).</summary>
    public static bool IsAvailable => PatchAreas.IsAvailable(PatchAreas.Framework, "RecipePlaceholders");

    /// <summary>True for a stand-in recipe object.</summary>
    public static bool IsPlaceholder(Recipe recipe) =>
        recipe != null && Cache.TryGetValue(recipe.ID, out var placeholder) && ReferenceEquals(placeholder, recipe);

    /// <summary>The ids handed out as stand-ins this session, with what the save said about them.</summary>
    public static List<ItemManifestEntry> Active()
    {
        var list = new List<ItemManifestEntry>(Cache.Count);
        foreach (int id in Cache.Keys)
        {
            var entry = Manifest.Find(id) ?? RecipeRegistry.ManifestEntry(id);
            list.Add(new ItemManifestEntry { Id = id, Mod = entry?.Mod, Title = entry?.Title });
        }
        return list;
    }

    /// <summary>Mod recipes the loaded save lists whose mod isn't adding them now.</summary>
    public static List<ItemManifestEntry> MissingInSave() => new List<ItemManifestEntry>(Manifest.Carried);

    internal static void Init(ObenLoader.ObenMod framework)
    {
        Cache.Clear();
        Manifest.Clear();
        // Stand-ins the previous copy handed out still sit in the player's recipe list.
        if (ReloadState.Take(CacheKey, out Dictionary<int, Recipe> previous))
        {
            foreach (var pair in previous)
                Cache[pair.Key] = pair.Value;
        }
        framework.Track(Disposable.Create(() =>
        {
            if (Cache.Count > 0)
                ReloadState.Put(CacheKey, new Dictionary<int, Recipe>(Cache));
            Cache.Clear();
            Manifest.Clear();
        }));
    }

    /// <summary>The stand-in for a missing mod recipe id. Called from the recipe lookup hook.</summary>
    internal static Recipe For(int id)
    {
        if (!Cache.TryGetValue(id, out var recipe))
        {
            recipe = Create(id);
            Cache[id] = recipe;
            ItemsLog.WarnOnce("recipe-placeholder:" + id, $"Recipe {id} isn't in the game now; it shows as '{recipe.Name}' and keeps its id until it is back.");
        }
        return recipe;
    }

    /// <summary>The save's list of mod recipes was read: name the ones whose mod isn't adding them.</summary>
    internal static void Loaded(List<ItemManifestEntry> entries)
    {
        int dropped = Manifest.Load(entries, RecipeRegistry.IsModded);
        if (dropped > 0)
            ItemsLog.Warn($"The save lists more than {ItemManifestBook.MaxCarried} missing mod recipes; {dropped} are no longer tracked by name.");
        foreach (var entry in Manifest.Carried)
            ItemsLog.WarnOnce("missing-recipe:" + entry.Id, $"The save has the recipe '{entry.Title}' ({entry.Id}) from {entry.Mod}, which isn't adding it now. It shows as a missing recipe until {entry.Mod} is back.");
        foreach (var pair in Cache)
            pair.Value.Name = Label(pair.Key);
    }

    /// <summary>What to write into the save: the recipes mods register now and the missing ones it listed.</summary>
    internal static List<ItemManifestEntry> ToSave() => Manifest.Build(RecipeRegistry.ManifestEntries()).Items;

    /// <summary>A mod took its recipe out (it unloaded): keep its name for the stand-ins and the next save.</summary>
    internal static void Removed(ItemManifestEntry entry) => Manifest.Carry(entry);

    /// <summary>
    /// A registered recipe is in the game again. Stand-ins for it in the player's recipe lists are swapped for
    /// it, as loading the save would.
    /// </summary>
    internal static void Returned(Recipe real)
    {
        Manifest.Forget(real.ID);
        if (!Cache.TryGetValue(real.ID, out var placeholder))
            return;
        Cache.Remove(real.ID);
        var controller = RecipeController.instance;
        if (controller == null)
            return;
        if (Swap(controller.collectedRecipes, placeholder, real) && TaskItemsManager.instance != null)
            TaskItemsManager.instance.UpdateTaskItem(CreateTaskItem(controller, real), migrateMissingIds: true);
        Swap(controller.learnedRecipes, placeholder, real);
        ItemsLog.Info($"Recipe {real.ID} ({real.Name}) is back; the player's recipe list shows it again.");
    }

    private static bool Swap(List<Recipe> recipes, Recipe placeholder, Recipe real)
    {
        bool swapped = false;
        for (int i = 0; i < recipes.Count; i++)
        {
            if (ReferenceEquals(recipes[i], placeholder))
            {
                recipes[i] = real;
                swapped = true;
            }
        }
        return swapped;
    }

    private static string Label(int id) =>
        ItemManifestBook.Label(Manifest.Find(id) ?? RecipeRegistry.ManifestEntry(id), "Missing recipe");

    // Nothing in, nothing out, a type no station crafts, and hidden until learned: only the id matters.
    private static Recipe Create(int id)
    {
        var recipe = new Recipe
        {
            ID = id,
            Name = Label(id),
            Type = RecipeType.None,
            RecipeRequired = true,
            input = new Dictionary<ItemReference, float>(),
            output = new Dictionary<ItemReference, float>(),
            outputFail = new Dictionary<ItemReference, float>(),
            Appearance = new Recipe.RecipeAppearance(),
            metaSettings = new Recipe.RecipeMetaSettigns(),
            allowedNonStolenItemsIds = new List<int>(),
            RequiredConditions = new List<RecipeCondition>(),
            ExcludingConditions = new List<RecipeCondition>(),
            RecipeTags = new List<string>(),
        };
        recipe.SetItems();
        return recipe;
    }
}

/// <summary>Hands out <see cref="RecipePlaceholders"/> where the game looks a recipe up by id. One comparison when the recipe exists.</summary>
[PatchArea("RecipePlaceholders")]
[HarmonyPatch(typeof(RecipeDatabase), nameof(RecipeDatabase.FetchRecipesById))]
internal static class RecipePlaceholderLookupPatch
{
    private static void Postfix(int id, ref Recipe __result)
    {
        if (__result != null || !ReservedIds.Contains(id))
            return;
        try
        {
            __result = RecipePlaceholders.For(id);
        }
        catch (Exception e)
        {
            ItemsLog.Failed("Missing recipe stand-in", e);
        }
    }
}
