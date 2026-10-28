// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
using System;
using System.Collections.Generic;
using ObenLoader.Runtime;
using OS.Items;

namespace ObenFramework.Items;

/// <summary>
/// Reads and changes what consumable items do (food, drink, health, intoxication...). The values live on the
/// item's definition, so a change applies to every copy of the item. Changes stack: the newest one wins, and
/// when it is removed (its mod unloads or the returned object is disposed) the one under it, or the game's own
/// values, come back. They are kept for the session and re-applied when the game rebuilds its item database;
/// they are not saved, so set them again in your mod's load.
/// </summary>
public static class ItemConsumables
{
    private static readonly Dictionary<int, ConsumableValues> Vanilla = new Dictionary<int, ConsumableValues>();
    private static readonly Dictionary<int, List<Layer>> Layers = new Dictionary<int, List<Layer>>();

    /// <summary>The item's current values, or null when it isn't consumable.</summary>
    public static ConsumableValues Get(int itemId)
    {
        var consumable = Find(itemId);
        return consumable != null ? ConsumableValues.Read(consumable) : null;
    }

    /// <summary>The game's own values of an item, before any mod changed them, or null.</summary>
    public static ConsumableValues Original(int itemId)
    {
        if (Vanilla.TryGetValue(itemId, out var values))
            return values.Copy();
        return Get(itemId);
    }

    /// <summary>
    /// Sets an item's values until <paramref name="owner"/> unloads or the returned object is disposed. Returns
    /// null when the item isn't consumable or a value is not a finite number.
    /// </summary>
    public static IDisposable Set(ITracker owner, int itemId, ConsumableValues values)
    {
        if (owner == null)
            throw new ArgumentNullException(nameof(owner));
        if (values == null || !values.IsFinite())
            return null;
        var consumable = Find(itemId);
        if (consumable == null)
            return null;

        if (!Vanilla.ContainsKey(itemId))
            Vanilla[itemId] = ConsumableValues.Read(consumable);
        if (!Layers.TryGetValue(itemId, out var stack))
            Layers[itemId] = stack = new List<Layer>();
        var layer = new Layer(values.Copy());
        stack.Add(layer);
        layer.Values.WriteTo(consumable);

        var registration = Disposable.Create(() => Remove(itemId, layer));
        owner.Track(registration);
        return registration;
    }

    /// <summary>Puts every change back onto the item definitions. Called after the game rebuilds its item database.</summary>
    internal static void Sync()
    {
        foreach (var pair in Layers)
        {
            var consumable = Find(pair.Key);
            if (consumable != null && pair.Value.Count > 0)
                pair.Value[pair.Value.Count - 1].Values.WriteTo(consumable);
        }
    }

    private static void Remove(int itemId, Layer layer)
    {
        if (!Layers.TryGetValue(itemId, out var stack) || !stack.Remove(layer))
            return;
        var consumable = Find(itemId);
        var top = stack.Count > 0 ? stack[stack.Count - 1].Values : (Vanilla.TryGetValue(itemId, out var original) ? original : null);
        if (consumable != null && top != null)
            top.WriteTo(consumable);
        if (stack.Count == 0)
            Layers.Remove(itemId);
    }

    private static ItemConsumable Find(int itemId)
    {
        var item = ItemLookup.ById(itemId);
        if (item == null || item.Actions == null)
            return null;
        foreach (var action in item.Actions)
        {
            if (action is ItemConsumable consumable)
                return consumable;
        }
        return null;
    }

    private sealed class Layer
    {
        public Layer(ConsumableValues values)
        {
            Values = values;
        }

        public ConsumableValues Values { get; }
    }
}
