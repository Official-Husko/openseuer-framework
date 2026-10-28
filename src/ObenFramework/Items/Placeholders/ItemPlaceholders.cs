// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
using System;
using System.Collections.Generic;
using ObenFramework.Events;
using ObenFramework.Patching;
using ObenFramework.Saves;
using ObenLoader;
using ObenLoader.Runtime;
using UnityEngine;

namespace ObenFramework.Items;

/// <summary>
/// Stand-ins for items whose mod is gone. A save only keeps an item's id; without its mod the game would
/// turn such a stack into Open Sewer Coin on the first lookup and lose the id. Instead, a lookup of a
/// missing id in the mod range (<see cref="ReservedIds"/>) gets a "Missing item" with the same id, named
/// after the mod and title the save recorded. It can't be used or sold, keeps its stack size and data,
/// and is saved under its own id, so when the mod is installed again the stack is the real item again.
/// If the mod comes back while the game runs (a hot reload), the stand-ins already handed out turn into
/// the real item on the spot.
/// </summary>
[PatchArea("ItemPlaceholders")]
public static class ItemPlaceholders
{
    private const string IconPath = "Sprites/Items/Paper Bag_spr";
    private const string PrefabPath = "Prefabs/Items/ERROR";
    private const int StackLimit = 100_000_000;

    internal static readonly ItemManifestBook Manifest = new ItemManifestBook();
    private static readonly Dictionary<int, Item> Cache = new Dictionary<int, Item>();
    private static ModSaveData<ItemManifestFile> _data;

    /// <summary>The stand-in hook is in place (its patches didn't fail).</summary>
    public static bool IsAvailable => PatchAreas.IsAvailable(PatchAreas.Framework, "ItemPlaceholders");

    /// <summary>True when <paramref name="id"/> is shown as a stand-in right now (its mod isn't adding it).</summary>
    public static bool IsPlaceholder(int id) => Cache.ContainsKey(id) && !ItemRegistry.IsModded(id);

    /// <summary>True for a stand-in item object.</summary>
    public static bool IsPlaceholder(Item item) =>
        item != null && Cache.TryGetValue(item.ID, out var placeholder) && ReferenceEquals(placeholder, item);

    /// <summary>The ids handed out as stand-ins this session, with what the save said about them.</summary>
    public static List<ItemManifestEntry> Active()
    {
        var list = new List<ItemManifestEntry>(Cache.Count);
        foreach (int id in Cache.Keys)
        {
            var entry = Manifest.Find(id);
            list.Add(new ItemManifestEntry { Id = id, Mod = entry?.Mod, Title = entry?.Title });
        }
        return list;
    }

    /// <summary>Mod items the loaded save lists whose mod isn't adding them now (whether or not a stack was looked at yet).</summary>
    public static List<ItemManifestEntry> MissingInSave() => new List<ItemManifestEntry>(Manifest.Carried);

    internal static void Init(ObenMod framework)
    {
        Cache.Clear();
        Manifest.Clear();
        // Stand-ins the previous copy handed out still sit in item references; keep them so they can
        // still turn into the real item. Item is a game type, so it outlives the reload.
        if (ReloadState.Take(CacheKey, out Dictionary<int, Item> previous))
        {
            foreach (var pair in previous)
                Cache[pair.Key] = pair.Value;
        }
        _data = ModSaves.Register<ItemManifestFile>(framework, "items");
        _data.DataLoaded.Subscribe(framework, Loaded);
        Loaded(_data.Value);
        GameEvents.Saving.Subscribe(framework, BeforeSave);
        GameEvents.MainMenu.Subscribe(framework, () => Manifest.Clear());
        framework.Track(Disposable.Create(() =>
        {
            if (Cache.Count > 0)
                ReloadState.Put(CacheKey, new Dictionary<int, Item>(Cache));
            Cache.Clear();
            Manifest.Clear();
            _data = null;
        }));
    }

    private const string CacheKey = "ObenFramework.ItemPlaceholders.Cache";

    /// <summary>The stand-in for a missing mod item id. Called from the item lookup hook.</summary>
    internal static Item For(int id)
    {
        if (!Cache.TryGetValue(id, out var item))
        {
            item = Create(id);
            Cache[id] = item;
            ItemsLog.WarnOnce("placeholder:" + id, $"Item {id} isn't added by any loaded mod; it shows as '{item.Title}' and keeps its id until the mod is back.");
        }
        return item;
    }

    /// <summary>
    /// A mod registered <paramref name="real"/> again. Stand-ins handed out for its id become a copy of
    /// it, so stacks and pickups that hold one show the real item without reloading the level.
    /// </summary>
    internal static void Returned(Item real)
    {
        Manifest.Forget(real.ID);
        if (!Cache.TryGetValue(real.ID, out var placeholder))
            return;
        Cache.Remove(real.ID);
        placeholder.Title = real.Title;
        placeholder.Categories = real.Categories;
        placeholder.BaseValue = real.BaseValue;
        placeholder.Description = real.Description;
        placeholder.BaseStackable = real.BaseStackable;
        placeholder.Appearance = real.Appearance;
        placeholder.Actions = real.Actions;
        placeholder.Attachments = real.Attachments;
        placeholder.Meta = real.Meta;
        ItemsLog.Info($"Item {real.ID} ({real.Title}) is back; its stand-ins show the real item again.");
    }

    /// <summary>A mod took its item out (it unloaded): keep its name for the stand-ins and the next save.</summary>
    internal static void Removed(ItemManifestEntry entry) => Manifest.Carry(entry);

    private static Item Create(int id)
    {
        var entry = Manifest.Find(id);
        var item = new Item
        {
            ID = id,
            Title = ItemManifestBook.Label(entry),
            Description = string.IsNullOrEmpty(entry?.Mod)
                ? "The mod that adds this item isn't installed or couldn't add it. It comes back when that mod is installed again; until then it can't be used or sold."
                : $"{entry.Mod} isn't installed or couldn't add this item. It comes back when {entry.Mod} is installed again; until then it can't be used or sold.",
            BaseValue = 0,
            BaseStackable = StackLimit,
            Categories = Array.Empty<string>(),
            Actions = Array.Empty<Item.ItemAction>(),
            Attachments = new Item.ItemAttachments { Name = "", Attachment1 = "none", Attachment2 = "none" },
            Meta = Array.Empty<object>(),
            // No ColorValue: the game parses it as "RGBA(r, g, b, a)" and skips an empty one.
            Appearance = new Item.ItemAppearance { SpritePath = IconPath, PrefabPath = PrefabPath },
        };
        item.Appearance.LoadSprite();
        item.Appearance.Prefab = Resources.Load<GameObject>(PrefabPath);
        return item;
    }

    private static void Loaded(ItemManifestFile file)
    {
        int dropped = Manifest.Load(file, ItemRegistry.IsModded);
        if (dropped > 0)
            ItemsLog.Warn($"The save lists more than {ItemManifestBook.MaxCarried} missing mod items; {dropped} are no longer tracked by name.");
        foreach (var entry in Manifest.Carried)
            ItemsLog.WarnOnce("missing:" + entry.Id, $"The save has '{entry.Title}' (item {entry.Id}) from {entry.Mod}, which isn't adding it now. Its stacks show as a missing item until {entry.Mod} is back.");
        Relabel();
    }

    private static void BeforeSave()
    {
        if (_data == null)
            return;
        _data.Value.Items = Manifest.Build(ItemRegistry.ManifestEntries()).Items;
    }

    // Stand-ins made before the save's list was read get their names now.
    private static void Relabel()
    {
        foreach (var pair in Cache)
        {
            var entry = Manifest.Find(pair.Key);
            if (entry != null)
                pair.Value.Title = ItemManifestBook.Label(entry);
        }
    }
}
