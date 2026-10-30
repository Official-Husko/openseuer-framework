// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
using System;
using System.Collections.Generic;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ObenFramework.Building;

/// <summary>
/// Stands in for a custom piece whose mod isn't installed (or whose definition failed to build): a copy of the
/// template the piece was made from, with the saved id, texts and prices, so the piece keeps its place, contents and
/// identity and turns back into the real thing when the mod returns. One per id, kept for the session.
/// </summary>
internal static class FurniturePlaceholders
{
    private static readonly Dictionary<string, global::Furniture> Made = new Dictionary<string, global::Furniture>(StringComparer.Ordinal);

    /// <summary>The ids that loaded as placeholders this session.</summary>
    public static ICollection<string> Ids => Made.Keys;

    /// <summary>A placeholder for a saved piece, or null when its template can't be loaded either.</summary>
    public static global::Furniture For(SavedPiece saved)
    {
        string id = saved.Id, templatePath = saved.TemplatePath;
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
        placeholder.title = string.IsNullOrEmpty(saved.Title) ? template.title : saved.Title;
        placeholder.name = placeholder.title;
        if (saved.Details != null)
            placeholder.details = saved.Details;
        if (saved.PriceOC >= 0)
            placeholder.priceOC = saved.PriceOC;
        if (saved.PriceRM >= 0)
            placeholder.priceRM = saved.PriceRM;
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

/// <summary>What a save kept of a custom piece: its id and template, and the texts and prices its stand-in shows.</summary>
internal readonly struct SavedPiece
{
    public SavedPiece(string id, string templatePath, string title, string details, int priceOC, int priceRM)
    {
        Id = id;
        TemplatePath = templatePath;
        Title = title;
        Details = details;
        PriceOC = priceOC;
        PriceRM = priceRM;
    }

    public string Id { get; }
    public string TemplatePath { get; }
    public string Title { get; }
    public string Details { get; }

    /// <summary>-1 when the save has none (the template's is used).</summary>
    public int PriceOC { get; }
    public int PriceRM { get; }

    public static SavedPiece Of(global::Furniture furniture) =>
        new SavedPiece(furniture.id, furniture.addressableAssetPath, furniture.title, furniture.details, furniture.priceOC, furniture.priceRM);
}

/// <summary>Turns a saved custom id into the furniture to use: the defined piece, else a placeholder.</summary>
internal static class FurnitureResolve
{
    public static global::Furniture Of(SavedPiece saved)
    {
        var resolved = FurnitureRegistry.Resolve(saved.Id);
        return resolved != null ? resolved : FurniturePlaceholders.For(saved);
    }
}
