// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
using System;
using System.Collections.Generic;
using ObenFramework.World;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ObenFramework.Building;

/// <summary>
/// Builds the game furniture of a definition: a copy of the template asset with the mod's id, texts, prices,
/// icon and category, and copies of the template's prefabs (kept under one inactive object, so the game never
/// wakes them) with the mod's model fitted in. The copy keeps the template's asset path, which is what lets a
/// save fall back to the template when the framework itself is missing.
/// </summary>
internal static class FurnitureBuild
{
    private static GameObject _holder;

    /// <summary>The built piece of an entry, building it on first use. Null when the build failed.</summary>
    public static global::Furniture Registered(FurnitureEntry entry)
    {
        if (entry.Built != null || entry.BuildFailed)
            return entry.Built;
        try
        {
            entry.Built = Build(entry);
        }
        catch (Exception e)
        {
            FurnitureRegistry.Log.Error("building " + entry.Id, e);
        }
        entry.BuildFailed = entry.Built == null;
        return entry.Built;
    }

    /// <summary>The game piece a definition copies. Throws with the reason when it can't be used.</summary>
    public static global::Furniture Template(string name)
    {
        var template = FurnitureKinds.Find(name);
        if (template == null)
            throw new ArgumentException($"There is no furniture '{name}' to copy. Use the name the add_furniture console command takes.");
        if (FurnitureIds.IsModId(template.id))
            throw new ArgumentException($"'{name}' is a mod's piece; copy one of the game's.");
        if (template is FurnitureSurface || template is Train)
            throw new ArgumentException($"'{name}' is a wall or floor surface or a train, which can't be copied.");
        if (template.prefab == null || template.prefab.transform.childCount == 0 || template.prefab.GetComponent<FurniturePlaceable>() == null)
            throw new ArgumentException($"'{name}' has no placeable prefab with a model child.");
        if (string.IsNullOrEmpty(template.addressableAssetPath))
            throw new ArgumentException($"'{name}' has no asset path, so saves couldn't fall back to it.");
        return template;
    }

    /// <summary>The inactive object the prefab copies live under, kept across scenes.</summary>
    public static Transform Holder()
    {
        if (_holder == null)
        {
            _holder = new GameObject("ObenFramework custom furniture");
            _holder.SetActive(false);
            Object.DontDestroyOnLoad(_holder);
        }
        return _holder.transform;
    }

    private static global::Furniture Build(FurnitureEntry entry)
    {
        var definition = entry.Definition;
        var template = Template(definition.Template);
        var furniture = Object.Instantiate(template);
        var made = new List<GameObject>();
        try
        {
            furniture.id = entry.Id;
            furniture.addressableAssetPath = template.addressableAssetPath;
            furniture.title = definition.Title ?? template.title;
            furniture.name = furniture.title;
            furniture.details = definition.Details ?? template.details;
            furniture.priceOC = definition.PriceOC < 0 ? template.priceOC : definition.PriceOC;
            furniture.priceRM = definition.PriceRM < 0 ? template.priceRM : definition.PriceRM;
            furniture.image = definition.Icon != null ? definition.Icon : template.image;
            furniture.dismantleItems = template.dismantleItems != null ? new List<global::Furniture.ReseourceItem>(template.dismantleItems) : new List<global::Furniture.ReseourceItem>();
            furniture.skins = new global::Furniture.Skin[0];
            furniture.alternativePrefabs = new global::Furniture.AlternativePrefab[0];
            furniture.category = FurnitureCategories.For(definition.Category, template.category);
            furniture.hideFlags = HideFlags.HideAndDontSave;

            furniture.prefab = Copy(template.prefab, made);
            furniture.previewPrefab = template.previewPrefab == template.prefab ? furniture.prefab : Copy(template.previewPrefab, made);
            furniture.customDisplayPrefab = Copy(template.customDisplayPrefab, made);
            furniture.prefab.GetComponent<FurniturePlaceable>().furniture = furniture;
            if (definition.Placement != FurniturePlacement.Template)
                SetPlacement(furniture, definition.Placement, entry.Id);

            if (definition.Model != null)
            {
                var (mesh, materials) = OwnModel(definition);
                FurnitureModelFit.Apply(furniture.prefab, mesh, materials, definition.Colliders, entry.Id);
                if (furniture.previewPrefab != furniture.prefab)
                    FurnitureModelFit.Apply(furniture.previewPrefab, mesh, materials, definition.Colliders, entry.Id);
                FurnitureModelFit.Apply(furniture.customDisplayPrefab, mesh, materials, definition.Colliders, entry.Id);
            }
            return furniture;
        }
        catch
        {
            foreach (var go in made)
                Object.Destroy(go);
            Object.Destroy(furniture);
            throw;
        }
    }

    private static GameObject Copy(GameObject original, List<GameObject> made)
    {
        if (original == null)
            return null;
        var copy = Object.Instantiate(original, Holder());
        copy.name = original.name;
        made.Add(copy);
        return copy;
    }

    // The framework's own copy of the mesh and materials, so the piece outlives the mod's model.
    private static (Mesh, Material[]) OwnModel(FurnitureDefinition definition)
    {
        var source = definition.Model;
        if (!source.IsAlive)
            throw new InvalidOperationException($"Model {source.Name} has been unloaded.");
        var mesh = Object.Instantiate(source.Mesh);
        mesh.name = source.Name;
        mesh.hideFlags = HideFlags.HideAndDontSave;
        var materials = new Material[source.Materials.Length];
        for (int i = 0; i < materials.Length; i++)
        {
            materials[i] = new Material(source.Materials[i]) { hideFlags = HideFlags.HideAndDontSave };
        }
        return (mesh, materials);
    }

    private static void SetPlacement(global::Furniture furniture, FurniturePlacement placement, string id)
    {
        var preview = furniture.previewPrefab != null ? furniture.previewPrefab.GetComponentInChildren<ObjectPreview>(true) : null;
        if (preview == null)
        {
            FurnitureRegistry.Log.Warn("placement " + id, "the preview has no ObjectPreview; the template's placement is kept");
            return;
        }
        preview.SetOrientationType(placement switch
        {
            FurniturePlacement.Floor => PlaceType.floor,
            FurniturePlacement.Wall => PlaceType.wall,
            FurniturePlacement.Ceiling => PlaceType.ceiling,
            FurniturePlacement.FloorAndWall => PlaceType.floorAndWall,
            _ => PlaceType.all,
        });
        if (placement == FurniturePlacement.Ceiling)
            furniture.displayStyle = global::Furniture.DisplayStyle.Ceiling;
    }
}
