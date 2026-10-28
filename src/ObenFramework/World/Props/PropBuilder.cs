// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
using System;
using ObenFramework.Assets;
using ObenFramework.Items;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace ObenFramework.World.Props;

/// <summary>
/// Builds the GameObject for a prop record. Game prefabs (furniture, items) are copied under an inactive
/// holder so none of their scripts wake up, stripped of every script, and only then moved into the scene.
/// </summary>
internal static class PropBuilder
{
    /// <summary>Why a source can't be built right now, or null when it can.</summary>
    public static string Check(WorldPropKind kind, string key, int alt)
    {
        switch (kind)
        {
            case WorldPropKind.Model:
                return ModelRegistry.Find(key) == null ? $"the model {key} is not loaded" : null;
            case WorldPropKind.Furniture:
                return FurniturePrefab(key, alt) == null ? $"the furniture {key} (alternative {alt}) has no prefab" : null;
            case WorldPropKind.Item:
                return ItemPrefab(alt) == null ? $"item {alt} has no world model" : null;
            default:
                return null;
        }
    }

    /// <summary>Builds the prop, or returns null with the reason.</summary>
    public static GameObject Build(PropRecord record, out string reason)
    {
        reason = null;
        GameObject go = null;
        try
        {
            switch (record.Kind)
            {
                case WorldPropKind.Model: go = Model(record, out reason); break;
                case WorldPropKind.Primitive: go = Primitive(record); break;
                case WorldPropKind.Furniture: go = Prefab(record, FurniturePrefab(record.Key, record.Alt), out reason); break;
                case WorldPropKind.Item: go = Prefab(record, ItemPrefab(record.Alt), out reason); break;
                case WorldPropKind.Light: go = Light(record); break;
                case WorldPropKind.Anchor: go = Anchor(record); break;
                default: reason = $"the prop kind {record.Kind} is unknown"; break;
            }
            if (go == null)
                return null;
            Finish(record, go);
            return go;
        }
        catch (Exception e)
        {
            if (go != null)
                Object.Destroy(go);
            reason = "the prop could not be built: " + e.Message;
            return null;
        }
    }

    public static string NameOf(PropRecord record) => record.Mod + "/" + record.Id;

    private static GameObject Model(PropRecord record, out string reason)
    {
        reason = null;
        var template = ModelRegistry.Find(record.Key);
        if (template == null)
        {
            reason = $"the model {record.Key} is not loaded";
            return null;
        }
        var go = Object.Instantiate(template.Prefab, PropHolder.Transform);
        go.name = NameOf(record);
        if (record.Collider)
            PropParts.EnsureCollider(go);
        else
            PropParts.RemoveColliders(go);
        return go;
    }

    private static GameObject Primitive(PropRecord record)
    {
        var go = GameObject.CreatePrimitive(Shape(record.Alt));
        go.SetActive(false);
        go.name = NameOf(record);
        if (!record.Collider)
            PropParts.RemoveColliders(go);
        return go;
    }

    private static GameObject Prefab(PropRecord record, GameObject prefab, out string reason)
    {
        reason = null;
        if (prefab == null)
        {
            reason = $"the prop {NameOf(record)} has no prefab";
            return null;
        }
        var go = Object.Instantiate(prefab, PropHolder.Transform);
        go.name = NameOf(record);
        if (!PropParts.Strip(go))
        {
            Object.Destroy(go);
            reason = $"the prefab of {NameOf(record)} keeps a script that can't be removed, so it is refused";
            return null;
        }
        if (!record.Collider)
            PropParts.RemoveColliders(go);
        return go;
    }

    private static GameObject Light(PropRecord record)
    {
        var go = new GameObject(NameOf(record));
        go.SetActive(false);
        var light = go.AddComponent<UnityEngine.Light>();
        light.type = record.LightKind == WorldPropLight.Spot ? LightType.Spot : LightType.Point;
        light.color = PropPose.Color(record.LightColor);
        light.range = record.LightRange;
        light.intensity = record.LightIntensity;
        if (light.type == LightType.Spot)
            light.spotAngle = record.LightSpotAngle;
        return go;
    }

    private static GameObject Anchor(PropRecord record)
    {
        var go = new GameObject(NameOf(record));
        go.SetActive(false);
        if (record.Collider)
            PropParts.EnsureCollider(go);
        return go;
    }

    private static void Finish(PropRecord record, GameObject go)
    {
        go.SetActive(false);
        PropParts.Settle(go);
        go.transform.SetParent(null, false);
        go.transform.localPosition = PropPose.Vector(record.Position);
        go.transform.localRotation = PropPose.Quaternion(record.Rotation);
        go.transform.localScale = PropPose.Vector(record.Scale);
        if (record.Static)
            go.isStatic = true;
        var scene = SceneManager.GetSceneByName(record.Scene);
        if (scene.IsValid() && scene.isLoaded)
            SceneManager.MoveGameObjectToScene(go, scene);
        go.SetActive(true);
    }

    private static GameObject FurniturePrefab(string key, int alt)
    {
        var furniture = FurnitureKinds.Find(key);
        if (furniture == null || alt < 0 || (alt > 0 && (furniture.AlternativePrefabs == null || alt > furniture.AlternativePrefabs.Length)))
            return null;
        return furniture.GetPrefab(alt);
    }

    private static GameObject ItemPrefab(int itemId)
    {
        var item = ItemLookup.ById(itemId);
        return item != null ? item.Prefab : null;
    }

    private static PrimitiveType Shape(int primitive)
    {
        switch ((WorldPrimitive)primitive)
        {
            case WorldPrimitive.Sphere: return PrimitiveType.Sphere;
            case WorldPrimitive.Cylinder: return PrimitiveType.Cylinder;
            case WorldPrimitive.Plane: return PrimitiveType.Plane;
            default: return PrimitiveType.Cube;
        }
    }
}
