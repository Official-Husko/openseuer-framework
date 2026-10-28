using System;
using System.Collections.Generic;
using ObenLoader;
using ObenLoader.Runtime;
using UnityEngine;

namespace ObenFramework.Items;

/// <summary>
/// Adds new items to the game's item database. Registered items behave like the game's own: they can
/// be given, stacked, sold and crafted with. They are put back into the database whenever the game
/// rebuilds it, and taken out again when the owning mod unloads.
/// </summary>
/// <remarks>
/// Items need ids from <see cref="ReservedIds"/> (use <see cref="ReservedIds.For"/>) and a sprite, or the
/// inventory draws their slot empty. Saved games keep only the id: if the mod is removed, stacks of its
/// items show up as Open Sewer Coin until the player drops them.
/// </remarks>
public static class ItemRegistry
{
    private static readonly Dictionary<int, Item> Entries = new Dictionary<int, Item>();

    /// <summary>All items currently registered by mods.</summary>
    public static IEnumerable<Item> Items => Entries.Values;

    public static bool IsModded(int id) => Entries.ContainsKey(id);

    /// <summary>
    /// Registers an item until <paramref name="owner"/> unloads. Throws when the item is invalid or its
    /// id is taken. Returns the item for chaining.
    /// </summary>
    /// <param name="item">
    /// A new <see cref="Item"/>. Give it an id from <see cref="ReservedIds"/>, a title, a
    /// <c>Appearance</c> with <c>Sprite</c> assigned, and optionally <c>Appearance.Prefab</c> (the pickup
    /// object, which needs a <c>CollectibleItem</c>).
    /// </param>
    public static Item Register(ObenMod owner, Item item)
    {
        Validate(item);
        Entries[item.ID] = item;
        owner.Track(Disposable.Create(() => Unregister(item)));
        Apply(item);
        RecipeRegistry.Sync();
        return item;
    }

    /// <summary>
    /// Copies an existing item as the starting point for a new one: same actions, meta, categories and
    /// look, with its own id, title and appearance, so changing the copy doesn't change the original.
    /// </summary>
    public static Item Derive(Item template, int id, string title)
    {
        if (template == null)
            throw new ArgumentNullException(nameof(template));

        var copy = template.Instantiate();
        copy.ID = id;
        copy.Title = title;
        copy.Appearance = template.Appearance?.Copy();
        copy.Categories = (string[])template.Categories?.Clone() ?? Array.Empty<string>();
        return copy;
    }

    /// <summary>Puts every registered item back into the database. Called after the game builds or reloads it.</summary>
    internal static void ApplyAll()
    {
        foreach (var item in Entries.Values)
            Apply(item);
        RecipeRegistry.ItemsChanged();
    }

    private static void Validate(Item item)
    {
        if (item == null)
            throw new ArgumentNullException(nameof(item));
        if (!ReservedIds.Contains(item.ID))
            throw new ArgumentException($"Item id {item.ID} is outside the range reserved for mods ({ReservedIds.Min}-{ReservedIds.Max}). Use ReservedIds.For.");
        if (Entries.ContainsKey(item.ID) || ItemLookup.Exists(item.ID))
            throw new ArgumentException($"An item with id {item.ID} already exists. Give '{item.Title}' a different key.");
        if (string.IsNullOrWhiteSpace(item.Title))
            throw new ArgumentException($"Item {item.ID} needs a title.");
        if (item.Appearance == null)
            throw new ArgumentException($"Item {item.ID} ({item.Title}) needs an Appearance.");

        // The game dereferences these without checks.
        item.Categories ??= Array.Empty<string>();
        item.Actions ??= Array.Empty<Item.ItemAction>();
        item.Attachments ??= new Item.ItemAttachments { Name = "", Attachment1 = "none", Attachment2 = "none" };
        item.Meta ??= Array.Empty<object>();
        if (item.BaseStackable < 1)
            item.BaseStackable = 1;

        try
        {
            // Parses ColorValue now; a bad value would otherwise throw inside the game's database load.
            item.Appearance.GetColor();
        }
        catch (Exception e)
        {
            throw new ArgumentException($"Item {item.ID} ({item.Title}) has an invalid Appearance.ColorValue: {e.Message}");
        }

        if (item.Appearance.Sprite == null && string.IsNullOrEmpty(item.Appearance.SpritePath))
            ItemsLog.Warn($"Item {item.ID} ({item.Title}) has no sprite, so its inventory slot will look empty.");
    }

    private static void Apply(Item item)
    {
        var database = ItemDatabase.database;
        // Before the game's own load finishes there is nothing to add to. The postfix on its load calls ApplyAll.
        if (database.Count == 0)
            return;

        var existing = ItemLookup.ById(item.ID);
        if (existing != null)
        {
            if (!ReferenceEquals(existing, item))
                ItemsLog.Error($"Item {item.ID} ({item.Title}) wasn't added: the database already has '{existing.Title}' with that id.");
            return;
        }

        database.Add(item);
        ItemIndex.Invalidate();
        try
        {
            // Only the meta assets: the sprite and prefab were assigned directly, and the game's
            // sprite loading would clear the skin sprites a mod set.
            ItemDatabase.LoadItemReferencedAssets(item, onlyMeta: true);
        }
        catch (Exception e)
        {
            ItemsLog.Error($"Couldn't load the assets of item {item.ID} ({item.Title})", e);
        }
    }

    private static void Unregister(Item item)
    {
        if (!Entries.Remove(item.ID))
            return;
        if (ItemDatabase.database.Remove(item))
            ItemIndex.Invalidate();
        RecipeRegistry.Sync();
    }
}
