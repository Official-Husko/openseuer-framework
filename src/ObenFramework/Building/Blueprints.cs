// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
using System;
using System.Collections.Generic;
using ObenFramework.Saves;

namespace ObenFramework.Building;

/// <summary>One furniture blueprint the level can hand out.</summary>
public readonly struct BlueprintInfo
{
    public BlueprintInfo(TaskItemBlueprint blueprint, string id, string title, string result, bool known, bool custom)
    {
        Blueprint = blueprint;
        Id = id;
        Title = title;
        Result = result;
        Known = known;
        Custom = custom;
    }

    public TaskItemBlueprint Blueprint { get; }

    /// <summary>The blueprint's id (asset name for the game's, <c>oben:.../blueprint</c> for a mod's).</summary>
    public string Id { get; }

    public string Title { get; }

    /// <summary>The title of the furniture it crafts.</summary>
    public string Result { get; }

    /// <summary>The player holds it.</summary>
    public bool Known { get; }

    /// <summary>A mod's blueprint.</summary>
    public bool Custom { get; }
}

/// <summary>
/// Every furniture blueprint of the current level, the game's and the mods': list them, see which the player
/// holds and hand one over.
/// </summary>
public static class Blueprints
{
    /// <summary>The blueprints the level's blueprint controller knows (all pools and categories), mods' included.</summary>
    public static List<BlueprintInfo> All()
    {
        var list = new List<BlueprintInfo>();
        var seen = new HashSet<TaskItemBlueprint>();
        var controller = FurnitureBlueprintController.instance;
        if (controller != null)
        {
            foreach (var blueprint in controller.GetAllBluePrints())
            {
                if (blueprint != null && seen.Add(blueprint))
                    list.Add(Describe(blueprint));
            }
        }
        foreach (var entry in FurnitureBlueprints.All())
        {
            var blueprint = FurnitureBlueprints.Registered(entry);
            if (blueprint != null && seen.Add(blueprint))
                list.Add(Describe(blueprint));
        }
        return list;
    }

    /// <summary>A blueprint by id, asset name or title (ignoring case), or null.</summary>
    public static TaskItemBlueprint Find(string name)
    {
        if (string.IsNullOrEmpty(name))
            return null;
        foreach (var info in All())
        {
            if (info.Id == name || string.Equals(info.Blueprint.name, name, StringComparison.OrdinalIgnoreCase)
                || string.Equals(info.Title, name, StringComparison.OrdinalIgnoreCase))
                return info.Blueprint;
        }
        return null;
    }

    public static bool IsKnown(TaskItemBlueprint blueprint) =>
        blueprint != null && TaskItemsManager.instance != null && TaskItemsManager.instance.CheckIfPlayerHasTaskItem(blueprint);

    /// <summary>Gives the player a blueprint, with the game's notification unless <paramref name="quiet"/>. False when they hold it.</summary>
    public static bool Give(TaskItemBlueprint blueprint, bool quiet = false)
    {
        if (blueprint == null || SaveGate.IsBusy || TaskItemsManager.instance == null || IsKnown(blueprint))
            return false;
        return quiet ? TaskItemsManager.instance.AddTaskItem(blueprint, 1) : blueprint.GiveItemWithResults();
    }

    public static BlueprintInfo Describe(TaskItemBlueprint blueprint)
    {
        if (blueprint == null)
            throw new ArgumentNullException(nameof(blueprint));
        string id = string.IsNullOrEmpty(blueprint.id) ? blueprint.name : blueprint.id;
        return new BlueprintInfo(blueprint, id, blueprint.itemName, blueprint.result != null ? blueprint.result.title : null,
            IsKnown(blueprint), FurnitureIds.IsModId(blueprint.id));
    }
}
