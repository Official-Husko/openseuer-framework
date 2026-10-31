// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
using System;
using System.Collections.Generic;
using HarmonyLib;
using ObenFramework.Items;
using ObenFramework.Patching;
using ObenFramework.Saves;
using ObenFramework.World;
using OS.Items;

namespace ObenFramework.Env;

/// <summary>A wall, floor or ceiling's covering, as read by <see cref="Surfaces"/> and raised by <see cref="Surfaces.SurfaceChanged"/>.</summary>
public readonly struct SurfaceInfo
{
    public SurfaceInfo(string id, string name, int wallpaperItemId, int skinIndex, ChangeSource source)
    {
        Id = id;
        Name = name;
        WallpaperItemId = wallpaperItemId;
        SkinIndex = skinIndex;
        Source = source;
    }

    public string Id { get; }

    public string Name { get; }

    /// <summary>The wallpaper, paint or panel item on it, or 0 when it is bare.</summary>
    public int WallpaperItemId { get; }

    /// <summary>Which of the item's variants (colours) is used.</summary>
    public int SkinIndex { get; }

    /// <summary>For a change: who made it. For a plain read: <see cref="ChangeSource.Game"/>.</summary>
    public ChangeSource Source { get; }

    public bool IsBare => WallpaperItemId <= 0;
}

/// <summary>
/// Walls, floors and ceilings that take wallpaper, paint or panels (<c>FurnitureSurfaceArea</c>), mostly
/// in the player's apartments. Coverings are saved with the game. Changes are refused (false) while a save
/// is written or loaded.
/// </summary>
public static class Surfaces
{
    private static readonly AccessTools.FieldRef<FurnitureSurfaceArea, int> SavedItem = AccessTools.FieldRefAccess<FurnitureSurfaceArea, int>("savedItemId");
    private static readonly AccessTools.FieldRef<FurnitureSurfaceArea, int> SavedSkin = AccessTools.FieldRefAccess<FurnitureSurfaceArea, int>("savedSkinIndex");

    /// <summary>A surface got a new covering or was scraped bare. Not raised while a save loads.</summary>
    public static readonly WorldEvent<SurfaceInfo> SurfaceChanged = new WorldEvent<SurfaceInfo>(nameof(SurfaceChanged));

    /// <summary>True when the hooks are in place, so <see cref="SurfaceChanged"/> is raised.</summary>
    public static bool IsAvailable => PatchAreas.IsAvailable(PatchAreas.Framework, "Env");

    /// <summary>Every surface of the loaded scenes. A scan.</summary>
    public static List<SurfaceInfo> All()
    {
        var list = new List<SurfaceInfo>();
        foreach (var surface in ObjectIds.Active<FurnitureSurfaceArea>())
            list.Add(Describe(surface));
        return list;
    }

    public static FurnitureSurfaceArea Find(string id) => ObjectIds.Find<FurnitureSurfaceArea>(id);

    public static SurfaceInfo Describe(FurnitureSurfaceArea surface, ChangeSource source = ChangeSource.Game) =>
        new SurfaceInfo(ObjectIds.IdOf(surface), surface.name, SavedItem(surface), SavedSkin(surface), source);

    /// <summary>The wallpaper, paint and panel items (items with wallpaper data).</summary>
    public static List<Item> CoveringItems()
    {
        var list = new List<Item>();
        foreach (var item in ItemDatabase.database)
        {
            if (item != null && ItemMetaUtilities.GetMetaOfType<ItemWallpaperData>(item.Meta) != null)
                list.Add(item);
        }
        return list;
    }

    /// <summary>
    /// Puts a covering on a surface, without taking an item from the player. Returns false when the item
    /// does not fit there (wrong area or type) or has no such variant. Throws for an unknown surface or item.
    /// </summary>
    public static bool SetCovering(string id, int itemId, int skinIndex = 0)
    {
        var surface = Find(id);
        if (surface == null)
            throw new ArgumentException($"No surface '{id}'.", nameof(id));
        if (surface.HasSameWallpaper(itemId, skinIndex))
            return true;
        var item = ItemLookup.ById(itemId) ?? throw new ArgumentException($"No item {itemId}.", nameof(itemId));
        var data = ItemMetaUtilities.GetMetaOfType<ItemWallpaperData>(item.Meta);
        if (data == null)
            throw new ArgumentException($"{item.Title} is not a wallpaper, paint or panel.", nameof(itemId));
        data.LoadVariants();
        if (data.GetMaterial(skinIndex) == null || !surface.AllowedToPlace(item, skinIndex) || SaveGate.IsBusy)
            return false;
        using (WorldChanges.EnterMod())
            surface.SetWallpaper(data, skinIndex, itemId);
        return true;
    }

    /// <summary>Scrapes a surface bare, without a scraper. True when it is bare afterwards. Throws for an unknown surface.</summary>
    public static bool Clear(string id)
    {
        var surface = Find(id);
        if (surface == null)
            throw new ArgumentException($"No surface '{id}'.", nameof(id));
        if (SavedItem(surface) <= 0)
            return true;
        if (SaveGate.IsBusy)
            return false;
        using (WorldChanges.EnterMod())
            surface.RemoveWallpaper();
        return SavedItem(surface) <= 0;
    }

    internal static int ItemOf(FurnitureSurfaceArea surface) => SavedItem(surface);

    internal static int SkinOf(FurnitureSurfaceArea surface) => SavedSkin(surface);
}
