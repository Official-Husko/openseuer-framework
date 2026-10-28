// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
using System;
using System.Collections.Generic;
using ObenFramework.Items;
using ObenFramework.World;
using UnityEngine;

namespace ObenFramework.Building;

/// <summary>The blueprint of one custom piece.</summary>
internal sealed class BlueprintEntry
{
    public BlueprintEntry(string id, FurnitureEntry target, BlueprintDefinition definition)
    {
        Id = id;
        Target = target;
        Definition = definition;
    }

    public string Id { get; }
    public FurnitureEntry Target { get; }
    public BlueprintDefinition Definition { get; }
    public TaskItemBlueprint Built;
}

/// <summary>
/// Builds the game blueprint (a <c>TaskItemBlueprint</c>) of custom furniture and links saved blueprints back to
/// it by id. A blueprint whose mod is gone stays what the save made of it: it crafts the placeholder of its piece.
/// </summary>
internal static class FurnitureBlueprints
{
    public static void Add(FurnitureEntry target, BlueprintDefinition definition)
    {
        target.Blueprint = new BlueprintEntry(FurnitureIds.BlueprintFor(target.Id), target, definition);
        FurniturePools.Changed();
    }

    public static IEnumerable<BlueprintEntry> All()
    {
        foreach (var entry in FurnitureRegistry.All)
        {
            if (entry.Blueprint != null)
                yield return entry.Blueprint;
        }
    }

    public static BlueprintEntry Find(string blueprintId) => FurnitureRegistry.Find(FurnitureIds.FurnitureOfBlueprint(blueprintId))?.Blueprint;

    /// <summary>The built blueprint, building it on first use. Null when it can't be built.</summary>
    public static TaskItemBlueprint Registered(BlueprintEntry entry)
    {
        if (entry.Built != null)
            return entry.Built;
        try
        {
            entry.Built = Build(entry);
        }
        catch (Exception e)
        {
            FurnitureRegistry.Log.Error("building blueprint " + entry.Id, e);
        }
        return entry.Built;
    }

    /// <summary>What a blueprint loaded from a save becomes: the mod's built one, else the saved one as it is.</summary>
    public static TaskItemBlueprint Resolve(TaskItemBlueprint saved)
    {
        var entry = Find(saved.id);
        var built = entry != null ? Registered(entry) : null;
        if (built == null)
            FurnitureRegistry.Log.Warn("blueprint " + saved.id, $"needs the mod {FurnitureIds.ModOf(saved.id) ?? "?"}; it crafts a stand-in until the mod is back");
        return built;
    }

    /// <summary>True when the player holds the blueprint.</summary>
    public static bool IsKnown(string blueprintId)
    {
        var tasks = TaskItemsManager.instance;
        if (tasks == null)
            return false;
        foreach (var info in tasks.taskItems)
        {
            if (info.taskItem is TaskItemBlueprint && info.taskItem.id == blueprintId)
                return true;
        }
        return false;
    }

    public static void Withdraw(FurnitureEntry entry)
    {
        if (entry.Blueprint == null)
            return;
        FurniturePools.Changed();
    }

    private static TaskItemBlueprint Build(BlueprintEntry entry)
    {
        var definition = entry.Definition;
        var result = FurnitureBuild.Registered(entry.Target);
        if (result == null)
            throw new InvalidOperationException($"its furniture {entry.Target.Id} didn't build");

        var resources = new List<global::Furniture.ReseourceItem>();
        foreach (var input in definition.Items)
        {
            var item = ItemLookup.ById(input.Key) ?? throw new InvalidOperationException($"ingredient item {input.Key} doesn't exist");
            resources.Add(new global::Furniture.ReseourceItem { item = new ItemReference(item), amount = input.Value });
        }
        foreach (string piece in definition.Furniture)
        {
            var furniture = FurnitureIds.IsModId(piece) ? FurnitureRegistry.Resolve(piece) : FurnitureKinds.Find(piece);
            if (furniture == null)
                throw new InvalidOperationException($"furniture ingredient {piece} doesn't exist");
            resources.Add(new global::Furniture.ReseourceItem { furniture = furniture, item = new ItemReference((Item)null), amount = 1 });
        }

        string title = definition.Title ?? result.title + " Blueprint";
        var blueprint = ScriptableObject.CreateInstance<TaskItemBlueprint>();
        blueprint.id = entry.Id;
        blueprint.name = title;
        blueprint.itemName = title;
        blueprint.itemDetails = definition.Details ?? "";
        blueprint.image = definition.Icon != null ? definition.Icon : result.image;
        blueprint.itemType = TaskItem.Type.Blueprints;
        blueprint.meta = new List<string>();
        blueprint.addressableAssetPath = "";
        blueprint.result = result;
        blueprint.Amount = definition.Amount;
        blueprint.processTime = definition.Seconds / 60.0;
        blueprint.skill = definition.Skill;
        blueprint.minimumSkill = definition.MinimumSkill;
        blueprint.resourceItems = resources;
        blueprint.hideFlags = HideFlags.HideAndDontSave;
        return blueprint;
    }
}
