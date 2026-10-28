// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
using System;
using System.Collections.Generic;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ObenFramework.Building;

/// <summary>
/// Stands in for a custom piece whose mod isn't installed (or whose definition failed to build): a copy of the
/// template the piece was made from, with the saved id and title, so the piece keeps its place, contents and
/// identity and turns back into the real thing when the mod returns. One per id, kept for the session.
/// </summary>
internal static class FurniturePlaceholders
{
    private static readonly Dictionary<string, global::Furniture> Made = new Dictionary<string, global::Furniture>(StringComparer.Ordinal);

    /// <summary>The ids that loaded as placeholders this session.</summary>
    public static ICollection<string> Ids => Made.Keys;

    /// <summary>A placeholder for a saved piece, or null when its template can't be loaded either.</summary>
    public static global::Furniture For(string id, string templatePath, string title)
    {
        if (Made.TryGetValue(id, out var made) && made != null)
            return made;
        if (string.IsNullOrEmpty(templatePath))
            return null;

        var template = SavableScriptableObject.LoadFromPath(templatePath) as global::Furniture;
        if (template == null || template.prefab == null)
        {
            FurnitureRegistry.Log.Warn("placeholder " + id, $"its template {templatePath} doesn't load; the piece is lost from this session");
            return null;
        }

        var placeholder = Object.Instantiate(template);
        placeholder.id = id;
        placeholder.title = string.IsNullOrEmpty(title) ? template.title : title;
        placeholder.name = placeholder.title;
        placeholder.addressableAssetPath = templatePath;
        placeholder.hideFlags = HideFlags.HideAndDontSave;
        Made[id] = placeholder;

        string mod = FurnitureIds.ModOf(id) ?? "?";
        if (FurnitureRegistry.Find(id) != null)
            FurnitureRegistry.Log.Warn("placeholder " + id, $"\"{placeholder.title}\" didn't build (see the error above); it looks like its template until it does");
        else
            FurnitureRegistry.Log.Warn("placeholder " + id, $"\"{placeholder.title}\" needs the mod {mod}; it looks like its template until the mod is back");
        return placeholder;
    }

    public static bool IsPlaceholder(global::Furniture furniture) =>
        furniture != null && furniture.id != null && Made.TryGetValue(furniture.id, out var made) && made == furniture;

    public static void Reset() => Made.Clear();
}

/// <summary>Turns a saved custom id into the furniture to use: the defined piece, else a placeholder.</summary>
internal static class FurnitureResolve
{
    public static global::Furniture Of(string id, string templatePath, string title)
    {
        var resolved = FurnitureRegistry.Resolve(id);
        return resolved != null ? resolved : FurniturePlaceholders.For(id, templatePath, title);
    }
}
