// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
using System;
using System.Collections.Generic;
using FullSerializer;
using HarmonyLib;
using UnityEngine;

namespace ObenFramework.Building;

/// <summary>
/// How custom furniture goes through the game's own save. The game saves a piece of furniture as just its
/// asset path and links it back to that asset on load. A custom piece shares its template's path, so it is
/// saved in full instead (id, title, prices, path) and linked back by id: to the defined piece, or to a
/// placeholder when its mod is gone. Without the framework the game links it to the template, so a save never
/// breaks; the pieces just look and behave like their templates.
/// </summary>
internal static class FurnitureSavePatches
{
    private static bool IsCustom(Dictionary<string, fsData> dict, out string id)
    {
        id = null;
        if (dict == null || !dict.TryGetValue("id", out var value) || !value.IsString)
            return false;
        id = value.AsString;
        return FurnitureIds.Classify(id) == FurnitureIdKind.Furniture;
    }

    private static string Text(Dictionary<string, fsData> dict, string key) =>
        dict.TryGetValue(key, out var value) && value.IsString ? value.AsString : null;

    [HarmonyPatch(typeof(SavedAssets), "IsCollapsible")]
    private static class KeepInFull
    {
        [HarmonyPrefix]
        private static bool Before(Dictionary<string, fsData> dict, ref string path, ref bool __result)
        {
            try
            {
                if (!IsCustom(dict, out _))
                    return true;
                path = null;
                __result = false;
                return false;
            }
            catch (Exception e)
            {
                FurnitureRegistry.Log.Error("Save collapse hook", e);
                return true;
            }
        }
    }

    [HarmonyPatch(typeof(SavedAssets), nameof(SavedAssets.GetAssetFor))]
    private static class LinkById
    {
        [HarmonyPrefix]
        private static bool Before(fsData data, Type storageType, ref ScriptableObject __result)
        {
            try
            {
                if (data == null || !data.IsDictionary || !typeof(global::Furniture).IsAssignableFrom(storageType))
                    return true;
                var dict = data.AsDictionary;
                if (!IsCustom(dict, out string id))
                    return true;
                var resolved = FurnitureResolve.Of(id, Text(dict, "addressableAssetPath"), Text(dict, "title"));
                if (resolved == null || !storageType.IsInstanceOfType(resolved))
                    return true;
                __result = resolved;
                return false;
            }
            catch (Exception e)
            {
                FurnitureRegistry.Log.Error("Save link hook", e);
                return true;
            }
        }
    }

    // The game re-links furniture and task items through LoadFromPath() all over its load code; for custom
    // pieces and blueprints the id decides, not the (template's) path.
    [HarmonyPatch(typeof(SavableScriptableObject), nameof(SavableScriptableObject.LoadFromPath), new Type[0])]
    private static class LoadById
    {
        [HarmonyPrefix]
        private static bool Before(SavableScriptableObject __instance, ref ScriptableObject __result)
        {
            try
            {
                if (__instance is global::Furniture furniture && FurnitureIds.Classify(furniture.id) == FurnitureIdKind.Furniture)
                {
                    var resolved = FurnitureResolve.Of(furniture.id, furniture.addressableAssetPath, furniture.title);
                    __result = resolved != null ? resolved : furniture;
                    return false;
                }
                if (__instance is TaskItemBlueprint blueprint && FurnitureIds.Classify(blueprint.id) == FurnitureIdKind.Blueprint)
                {
                    var resolved = FurnitureBlueprints.Resolve(blueprint);
                    __result = resolved != null ? resolved : blueprint;
                    return false;
                }
                return true;
            }
            catch (Exception e)
            {
                FurnitureRegistry.Log.Error("Load by id hook", e);
                return true;
            }
        }
    }

    // The game ids a furniture's inventory entry by asset path, which a custom piece shares with its template.
    [HarmonyPatch(typeof(global::Furniture), nameof(global::Furniture.CreateTaskItem))]
    private static class TaskItemId
    {
        [HarmonyPostfix]
        private static void After(global::Furniture furniture, TaskItem __result)
        {
            if (furniture != null && __result != null && FurnitureIds.IsModId(furniture.id))
                __result.id = furniture.id;
        }
    }

    // A placeholder uses its template's prefab, whose placeable points at the template; point it at the
    // placeholder so taking it back and saving keep the custom id.
    [HarmonyPatch(typeof(FurniturePlaceable), nameof(FurniturePlaceable.SetFurnitureInfo))]
    private static class PlaceableFurniture
    {
        [HarmonyPostfix]
        private static void After(FurniturePlaceable __instance, BuildingSystem.FurnitureInfo info)
        {
            try
            {
                if (__instance != null && info != null && info.furniture != null && FurnitureIds.IsModId(info.furniture.id)
                    && __instance.furniture != info.furniture)
                    __instance.furniture = info.furniture;
            }
            catch (Exception e)
            {
                FurnitureRegistry.Log.Error("Placement hook", e);
            }
        }
    }
}
