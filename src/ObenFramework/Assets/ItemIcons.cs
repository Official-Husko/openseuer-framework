// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
using System;
using System.Collections.Generic;
using HarmonyLib;
using ObenFramework.Items;
using ObenLoader;
using ObenLoader.Runtime;
using UnityEngine;

namespace ObenFramework.Assets;

/// <summary>
/// Replaces the inventory icon of any item, the game's or a mod's, with a sprite or an image file of the mod's,
/// for as long as the mod is loaded. The icon is put back whenever the game reloads the item's sprite, and the
/// original returns when the mod unloads. The newest replacement of an item wins.
/// </summary>
public static class ItemIcons
{
    private sealed class Entry
    {
        public int ItemId;
        public Sprite Sprite;
        public Sprite Original;
        public List<Sprite> OriginalSkins;
    }

    private static readonly List<Entry> Entries = new List<Entry>();

    /// <summary>Uses <paramref name="sprite"/> as the item's icon (also for its skins) until <paramref name="owner"/> unloads.</summary>
    public static IDisposable Set(ITracker owner, int itemId, Sprite sprite)
    {
        if (owner == null)
            throw new ArgumentNullException(nameof(owner));
        if (sprite == null)
            throw new ArgumentNullException(nameof(sprite));
        var entry = new Entry { ItemId = itemId, Sprite = sprite };
        Entries.Add(entry);
        Apply(itemId);
        var registration = Disposable.Create(() => Remove(entry));
        owner.Track(registration);
        return registration;
    }

    /// <summary>Loads a PNG or JPEG from the mod's folder (square ones look best, 128 or 256 pixels) and uses it as the icon.</summary>
    public static IDisposable SetFromFile(ObenMod owner, int itemId, string path) => Set(owner, itemId, ModAssets.Sprite(owner, path));

    /// <summary>
    /// Draws <paramref name="model"/> with <see cref="ModelIcons"/> and uses the picture as the icon until
    /// <paramref name="owner"/> unloads. Throws when the model has nothing to draw.
    /// </summary>
    public static IDisposable SetFromModel(ITracker owner, int itemId, GameObject model, IconOptions options = null)
    {
        if (owner == null)
            throw new ArgumentNullException(nameof(owner));
        var sprite = ModelIcons.Render(model, options) ?? throw new ArgumentException($"There is nothing to draw in '{model.name}'.", nameof(model));
        var icon = Set(owner, itemId, sprite);
        var registration = Disposable.Create(() =>
        {
            icon.Dispose();
            ModelIcons.Release(sprite);
        });
        owner.Track(registration);
        return registration;
    }

    /// <inheritdoc cref="SetFromModel(ITracker, int, GameObject, IconOptions)"/>
    public static IDisposable SetFromModel(ITracker owner, int itemId, ModelTemplate model, IconOptions options = null)
    {
        if (model == null)
            throw new ArgumentNullException(nameof(model));
        if (!model.IsAlive || model.Prefab == null)
            throw new ArgumentException($"Model {model.Name} has been unloaded.", nameof(model));
        return SetFromModel(owner, itemId, model.Prefab, options);
    }

    /// <summary>True when a mod replaced the item's icon.</summary>
    public static bool IsReplaced(int itemId) => Top(itemId) != null;

    // After the game (re)loads an item's sprites, put the replacement back on.
    internal static void Reapply(Item item)
    {
        if (item == null || Entries.Count == 0)
            return;
        var top = Top(item.ID);
        if (top != null && item.Appearance != null && item.Appearance.Sprite != top.Sprite)
        {
            foreach (var entry in Entries)
            {
                if (entry.ItemId == item.ID)
                    Remember(entry, item);
            }
            Put(item, top.Sprite);
        }
    }

    internal static void Reset()
    {
        foreach (var entry in new List<Entry>(Entries))
            Remove(entry);
        Entries.Clear();
    }

    private static void Apply(int itemId)
    {
        var item = ItemLookup.ById(itemId);
        if (item == null || item.Appearance == null)
            return;
        var top = Top(itemId);
        if (top == null)
            return;
        Remember(top, item);
        Put(item, top.Sprite);
    }

    private static void Remember(Entry entry, Item item)
    {
        if (entry.Original != null || item.Appearance.Sprite == entry.Sprite)
            return;
        // The sprite the item showed before any mod: the first entry that saw it keeps it.
        foreach (var other in Entries)
        {
            if (other.ItemId == item.ID && other.Original != null)
            {
                entry.Original = other.Original;
                entry.OriginalSkins = other.OriginalSkins;
                return;
            }
        }
        entry.Original = item.Appearance.Sprite;
        entry.OriginalSkins = item.Appearance.SkinSprites != null ? new List<Sprite>(item.Appearance.SkinSprites) : null;
    }

    private static void Put(Item item, Sprite sprite)
    {
        item.Appearance.Sprite = sprite;
        var skins = item.Appearance.SkinSprites;
        if (skins != null)
        {
            for (int i = 0; i < skins.Count; i++)
                skins[i] = sprite;
        }
    }

    private static void Remove(Entry entry)
    {
        if (!Entries.Remove(entry))
            return;
        var item = ItemLookup.ById(entry.ItemId);
        if (item == null || item.Appearance == null)
            return;
        var top = Top(entry.ItemId);
        if (top != null)
        {
            if (top.Original == null)
            {
                top.Original = entry.Original;
                top.OriginalSkins = entry.OriginalSkins;
            }
            Put(item, top.Sprite);
            return;
        }
        if (entry.Original != null)
        {
            item.Appearance.Sprite = entry.Original;
            if (entry.OriginalSkins != null)
                item.Appearance.SkinSprites = new List<Sprite>(entry.OriginalSkins);
        }
    }

    private static Entry Top(int itemId)
    {
        for (int i = Entries.Count - 1; i >= 0; i--)
        {
            if (Entries[i].ItemId == itemId)
                return Entries[i];
        }
        return null;
    }

    [HarmonyPatch(typeof(ItemDatabase), nameof(ItemDatabase.LoadItemReferencedAssets))]
    private static class ReloadPatch
    {
        [HarmonyPostfix]
        private static void After(Item i)
        {
            try { Reapply(i); }
            catch (Exception e) { AssetsLog.Error("Item icon hook failed", e); }
        }
    }
}
