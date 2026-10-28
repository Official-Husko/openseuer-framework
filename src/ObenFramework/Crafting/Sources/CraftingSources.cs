// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
using System;
using System.Collections.Generic;
using HarmonyLib;
using ObenFramework.Saves;
using ObenFramework.World;
using ObenLoader.Runtime;
using UnityEngine;

namespace ObenFramework.Crafting;

/// <summary>
/// Extra ingredient sources for crafting stations: containers whose contents a station can use as if they
/// were in its own input slots (a pantry next to the stove, a crate beside the workbench). A mod registers a
/// provider that returns the containers for a station; the station then lists, checks and takes ingredients
/// from its own slots first and from the extra containers after them, in provider priority order.
/// </summary>
/// <remarks>
/// While a station checks or takes ingredients its input storage's slot list is swapped for its own plus the
/// extra slots, and swapped back right after, so the game's own recipe logic (groups, liquids, ammo, variants)
/// applies unchanged. Starting a craft re-checks the extra containers first and refuses when they no longer
/// hold enough. Dishwashers don't use extra sources. Providers are asked each time a station checks its
/// recipes, so keep them quick; the result is reused within a frame.
/// </remarks>
public static class CraftingSources
{
    private static readonly List<CraftingSourceProvider> Providers = new List<CraftingSourceProvider>();
    private static readonly List<CraftingSourceScope> Open = new List<CraftingSourceScope>();

    private static readonly AccessTools.FieldRef<CraftingBase, ItemStack[]> InputSlotsCache =
        AccessTools.FieldRefAccess<CraftingBase, ItemStack[]>("inputSlots");

    private static int _registrations;
    private static CraftingBase _cachedStation;
    private static int _cachedFrame = -1;
    private static List<CraftingSource> _cachedSources;
    private static StorageSlot[] _cachedOwn;
    private static StorageSlot[] _cachedCombined;

    /// <summary>A station took ingredients from an extra container (once per slot it took from).</summary>
    public static readonly WorldEvent<CraftingTaken> Consumed = new WorldEvent<CraftingTaken>(nameof(Consumed));

    /// <summary>True while at least one provider is registered.</summary>
    public static bool Any => Providers.Count > 0;

    /// <summary>
    /// Adds a provider until <paramref name="owner"/> unloads or the returned object is disposed.
    /// </summary>
    /// <param name="provider">Returns the containers a station may use, or null/empty for none.</param>
    /// <param name="priority">Higher goes first; equal priorities keep their registration order.</param>
    /// <param name="slotFilter">Optional: return false for slots (storage, index, stack) the station must not touch.</param>
    public static IDisposable Register(ITracker owner, Func<StationInfo, IEnumerable<Storage>> provider, int priority = 0,
        Func<Storage, int, StorageSlot, bool> slotFilter = null)
    {
        if (owner == null)
            throw new ArgumentNullException(nameof(owner));
        if (provider == null)
            throw new ArgumentNullException(nameof(provider));

        var entry = new CraftingSourceProvider(owner, provider, priority, slotFilter, _registrations++);
        Providers.Add(entry);
        SourceOrder.Sort(Providers, p => p.Priority, p => p.Order);
        Forget();
        var registration = Disposable.Create(() =>
        {
            Providers.Remove(entry);
            Forget();
        });
        owner.Track(registration);
        return registration;
    }

    /// <summary>The extra containers a station would use right now.</summary>
    public static List<Storage> For(CraftingBase station)
    {
        var list = new List<Storage>();
        if (station == null || !Usable(station))
            return list;
        foreach (var source in Sources(station))
            list.Add(source.Storage);
        return list;
    }

    /// <summary>Drops the per-frame cache. Called on scene changes and when providers change.</summary>
    internal static void Forget()
    {
        _cachedStation = null;
        _cachedSources = null;
        _cachedOwn = null;
        _cachedCombined = null;
        _cachedFrame = -1;
    }

    /// <summary>True when the station has extra sources right now.</summary>
    internal static bool HasSources(CraftingBase station) =>
        Providers.Count > 0 && station != null && Usable(station) && Sources(station).Count > 0;

    internal static bool IsOpen(CraftingBase station)
    {
        foreach (var scope in Open)
        {
            if (scope.Station == station)
                return true;
        }
        return false;
    }

    /// <summary>Swaps in the extra slots for one call, or returns null when the station has no extra sources.</summary>
    internal static CraftingSourceScope Begin(CraftingBase station, bool consume)
    {
        if (Providers.Count == 0 || station == null || !Usable(station) || IsOpen(station))
            return null;

        var sources = Sources(station);
        if (sources.Count == 0)
            return null;

        var storage = station.InputOutputStorage;
        var own = storage.storageSlots;
        var scope = new CraftingSourceScope(station, consume, sources, own, Combined(own, sources));
        Open.Add(scope);
        storage.storageSlots = scope.Combined;
        InputSlotsCache(station) = null;
        return scope;
    }

    internal static void End(CraftingSourceScope scope)
    {
        if (scope == null || scope.Closed)
            return;
        scope.Closed = true;
        Open.Remove(scope);

        var storage = scope.Station != null ? scope.Station.InputOutputStorage : null;
        if (storage != null && storage.storageSlots == scope.Combined)
            storage.storageSlots = scope.Own;
        if (scope.Station != null)
            InputSlotsCache(scope.Station) = null;
        if (!scope.Consume)
            return;

        scope.Refresh();
        if (Consumed.HasSubscribers)
        {
            foreach (var taken in scope.Taken())
                Consumed.Raise(taken);
        }
    }

    private static bool Usable(CraftingBase station) =>
        station.InputOutputStorage != null && station.InputOutputStorage.storageSlots != null && !(station is Dishwasher) && !SaveGate.IsBusy;

    // Valid as long as the sources are: same station, same frame.
    private static StorageSlot[] Combined(StorageSlot[] own, List<CraftingSource> sources)
    {
        if (_cachedCombined != null && ReferenceEquals(_cachedOwn, own) && ReferenceEquals(sources, _cachedSources))
            return _cachedCombined;
        _cachedOwn = own;
        _cachedCombined = CraftingSourceScope.Combine(own, sources);
        return _cachedCombined;
    }

    // A station's window asks for every recipe in a row; the providers answer once per station and frame.
    private static List<CraftingSource> Sources(CraftingBase station)
    {
        if (_cachedSources != null && _cachedStation == station && _cachedFrame == Time.frameCount)
            return _cachedSources;

        var info = CraftingStations.Describe(station);
        var storage = station.InputOutputStorage;
        var found = new List<CraftingSource>();
        var seen = new HashSet<Storage>();
        foreach (var provider in Providers.ToArray())
        {
            var given = provider.Ask(info);
            if (given == null)
                continue;
            foreach (var extra in given)
            {
                if (extra == null || extra == storage || extra == station.OutputStorage || extra.storageSlots == null || extra.storageSlots.Length == 0)
                    continue;
                if (seen.Add(extra))
                    found.Add(new CraftingSource(provider, extra));
            }
        }

        _cachedStation = station;
        _cachedFrame = Time.frameCount;
        _cachedSources = found;
        _cachedOwn = null;
        _cachedCombined = null;
        return found;
    }
}
