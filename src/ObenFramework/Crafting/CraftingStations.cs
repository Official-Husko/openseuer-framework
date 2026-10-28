// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
using System;
using System.Collections.Generic;
using HarmonyLib;
using ObenFramework.Items;
using ObenFramework.Saves;
using ObenFramework.World;

namespace ObenFramework.Crafting;

/// <summary>
/// The crafting stations of the loaded scenes: listing them with ids that survive sessions, and starting,
/// stopping, pausing and resuming them the way their own buttons do. Starting checks that the station holds
/// the ingredients (including extra <see cref="CraftingSources"/>) and refuses otherwise. Every control call
/// refuses while a save loads or while the player has a station window open.
/// </summary>
public static partial class CraftingStations
{
    private static readonly AccessTools.FieldRef<CraftingBase, Recipe> SelectedRecipe =
        AccessTools.FieldRefAccess<CraftingBase, Recipe>("selectedRecipe");

    private static readonly AccessTools.FieldRef<manufacturing, int> CraftsRunning =
        AccessTools.FieldRefAccess<manufacturing, int>("CraftingInProgress");

    private static readonly Func<CraftingBase, Recipe, ItemStack[], bool, bool, bool> CompareRecipeToItems =
        AccessTools.MethodDelegate<Func<CraftingBase, Recipe, ItemStack[], bool, bool, bool>>(
            AccessTools.Method(typeof(CraftingBase), "CompareRecipeToItems"));

    /// <summary>Every active station in the loaded scenes. A scan; not for per-frame use.</summary>
    public static List<StationInfo> All()
    {
        var list = new List<StationInfo>();
        foreach (var station in ObjectIds.Active<CraftingBase>())
            list.Add(Describe(station));
        return list;
    }

    /// <summary>
    /// The station's id: the n-th station inside a piece of furniture the player placed is
    /// <c>furniture:{furnitureId}:{n}</c>, any other its game GUID or object address.
    /// </summary>
    public static string IdOf(CraftingBase station) => ObjectIds.IdOf(station);

    /// <summary>The station with this id, or null.</summary>
    public static CraftingBase Find(string id) => ObjectIds.Find<CraftingBase>(id);

    public static StationInfo Describe(CraftingBase station)
    {
        if (station == null)
            throw new ArgumentNullException(nameof(station));
        return new StationInfo(station, IdOf(station), Names(station.RecipesWithUpgrades), Names(station.ModifiersWithUpgrades));
    }

    /// <summary>The station whose window the player has open, or null.</summary>
    public static CraftingBase Open => CraftingBase.currentCraftingBase != null ? CraftingBase.currentCraftingBase : null;

    /// <summary>The recipe selected in the station's window, or null.</summary>
    public static Recipe Selected(CraftingBase station) => station != null ? SelectedRecipe(station) : null;

    /// <summary>
    /// True when the station's input slots (plus its extra <see cref="CraftingSources"/>) hold what the recipe
    /// needs, <paramref name="multiples"/> times over.
    /// </summary>
    public static bool HasIngredients(CraftingBase station, Recipe recipe, int multiples = 1)
    {
        if (station == null || recipe == null || station.InputOutputStorage == null)
            return false;
        if (!CompareRecipeToItems(station, recipe, null, false, true))
            return false;
        return multiples <= 1 || station.GetRecipeMultiplier(recipe) >= multiples;
    }

    /// <summary>
    /// Starts a station the way its own button does: stills and similar process their loaded batch,
    /// workbench-like stations craft their selected recipe (unless they need the player present), and grow
    /// boxes plant their selected seed in one free spot. Returns true when something started.
    /// </summary>
    public static bool Start(string stationId)
    {
        var station = Find(stationId);
        if (station == null || Refused())
            return false;

        using (WorldChanges.EnterMod())
        {
            if (station is manufacturingProcess process)
                return StartProcess(process);
            if (station is Growing growing)
                return StartGrow(growing);
            if (station.GetType() == typeof(manufacturing))
                return StartCraft((manufacturing)station);
        }
        return false;
    }

    internal static bool Refused() => SaveGate.IsBusy || CraftingBase.currentCraftingBase != null;

    internal static List<string> Names<T>(IEnumerable<T> values)
    {
        var names = new List<string>();
        if (values == null)
            return names;
        foreach (var value in values)
            names.Add(value.ToString());
        return names;
    }

    private static bool StartProcess(manufacturingProcess station)
    {
        var recipe = station.ActiveRecipe.Key;
        if (station.stage != manufacturingProcess.Stage.LoadingItems || !station.PoweredAndActive || recipe == null)
            return false;
        if (!HasIngredients(station, recipe, station.ActiveRecipe.Value))
            return false;
        station.StartProcess();
        return station.stage == manufacturingProcess.Stage.Processing;
    }

    private static bool StartCraft(manufacturing station)
    {
        var recipe = SelectedRecipe(station);
        if (recipe == null || station.RequirePlayer || !station.PoweredAndActive || !HasIngredients(station, recipe))
            return false;
        int before = CraftsRunning(station);
        if (before >= station.AllowSimultaneous)
            return false;
        station.Craft();
        return CraftsRunning(station) > before || recipe.ProcessTime <= 0;
    }

    private static bool StartGrow(Growing station)
    {
        var recipe = SelectedRecipe(station);
        int free = FreeSpots(station);
        if (recipe == null || free == 0 || !HasIngredients(station, recipe))
            return false;
        station.StartGrow(1);
        return FreeSpots(station) < free;
    }

    private static int FreeSpots(Growing station)
    {
        int free = 0;
        foreach (var spot in station.GrowingSlots)
        {
            if (spot != null && spot.Plant == null)
                free++;
        }
        return free;
    }
}
