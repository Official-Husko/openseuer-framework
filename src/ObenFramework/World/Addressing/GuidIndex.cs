using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ObenFramework.World.Addressing;

/// <summary>
/// Finds scene objects by the <c>GUID</c> field the game's save system keys on. The game has no cheap
/// lookup for it (<c>SaveController.GetObjectByGUID(guid, type)</c> scans the scene), so one index is
/// built per scene on first use and dropped when the scene changes. Never touched per frame.
/// </summary>
internal static class GuidIndex
{
    // Rebuilding on a miss lets objects that appear later (apartment prefabs) be found, but a mod
    // asking for a GUID that doesn't exist must not trigger a scene scan every call.
    private const float MissRebuildSeconds = 2f;

    private static readonly Dictionary<Type, FieldInfo> Fields = new Dictionary<Type, FieldInfo>();
    private static readonly Dictionary<string, Component> Map = new Dictionary<string, Component>();
    private static bool _dirty = true;
    private static float _builtAt = float.NegativeInfinity;

    /// <summary>Forget everything; the next lookup rebuilds. Called when the scene changes.</summary>
    public static void Invalidate() => _dirty = true;

    public static void Reset()
    {
        Map.Clear();
        _dirty = true;
    }

    /// <summary>The object's GUID, or null when its type has none or it isn't set.</summary>
    public static string GuidOf(Component component)
    {
        if (component == null)
            return null;

        var field = FieldOf(component.GetType());
        if (field == null)
            return null;
        var guid = field.GetValue(component) as string;
        return string.IsNullOrEmpty(guid) ? null : guid;
    }

    public static Component Find(string guid)
    {
        if (string.IsNullOrEmpty(guid))
            return null;

        if (_dirty)
            Rebuild();
        if (Map.TryGetValue(guid, out var found) && found != null)
            return found;

        if (Time.realtimeSinceStartup - _builtAt < MissRebuildSeconds)
            return null;
        Rebuild();
        return Map.TryGetValue(guid, out found) && found != null ? found : null;
    }

    private static void Rebuild()
    {
        Map.Clear();
        _dirty = false;
        _builtAt = Time.realtimeSinceStartup;
        foreach (var script in Object.FindObjectsByType<SavableScript>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            // Placed furniture gets a new GUID on every save, so it can't be an address.
            if (script is FurniturePlaceable)
                continue;
            var guid = GuidOf(script);
            if (guid != null && !Map.ContainsKey(guid))
                Map[guid] = script;
        }
    }

    // The game reads the same public field by name (SaveController.SerializeData).
    private static FieldInfo FieldOf(Type type)
    {
        if (!Fields.TryGetValue(type, out var field))
        {
            field = type.GetField("GUID", BindingFlags.Public | BindingFlags.Instance);
            if (field != null && field.FieldType != typeof(string))
                field = null;
            Fields[type] = field;
        }
        return field;
    }
}
