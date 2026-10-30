// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
using System;
using System.Collections.Generic;
using HarmonyLib;
using ObenFramework.Saves;
using ObenLoader.Runtime;

namespace ObenFramework.Items;

/// <summary>What the game is checking resources for when it asks <see cref="InventorySources"/>.</summary>
public readonly struct InventorySourceContext
{
    public InventorySourceContext(string kind)
    {
        Kind = kind;
    }

    /// <summary><see cref="InventorySources.FurnitureBlueprint"/> or <see cref="InventorySources.Renovation"/>.</summary>
    public string Kind { get; }
}

/// <summary>
/// Lets containers count as part of the player's inventory where the game checks and takes resources from the
/// inventory alone: crafting furniture from a blueprint at a workshop, and tenement renovations. A mod
/// registers a provider that returns the containers to add (for example the ones near the player); while the
/// game checks or takes resources they count as if the player carried them. The player's own inventory (and,
/// for renovations, the tenement's resource storages) is used first, the extra containers only for what is
/// missing. Crafting stations have their own <see cref="ObenFramework.Crafting.CraftingSources"/>.
/// </summary>
public static class InventorySources
{
    /// <summary>
    /// The framework's item hooks are in place. False when they failed to apply (a game update changed what they
    /// patch); the events then stay quiet and changes the game should pick up may not take.
    /// </summary>
    public static bool IsAvailable => ItemsModule.HooksInPlace;

    public const string FurnitureBlueprint = "FurnitureBlueprint";
    public const string Renovation = "Renovation";

    private static readonly List<Func<InventorySourceContext, IEnumerable<Storage>>> Providers =
        new List<Func<InventorySourceContext, IEnumerable<Storage>>>();

    private static readonly AccessTools.FieldRef<TenementController, List<Storage>> ResourceStorages =
        AccessTools.FieldRefAccess<TenementController, List<Storage>>("tenementResourceStorages");

    private static InventorySourceScope _current;

    /// <summary>True while at least one provider is registered.</summary>
    public static bool Any => Providers.Count > 0;

    /// <summary>
    /// Adds a provider until <paramref name="owner"/> unloads or the returned object is disposed. It is asked
    /// each time the game checks or takes resources, so keep it quick; return an empty list to add nothing.
    /// Containers that haven't started (no slots yet), the equipped bag and duplicates are skipped.
    /// </summary>
    public static IDisposable Register(ITracker owner, Func<InventorySourceContext, IEnumerable<Storage>> provider)
    {
        if (owner == null)
            throw new ArgumentNullException(nameof(owner));
        if (provider == null)
            throw new ArgumentNullException(nameof(provider));

        Providers.Add(provider);
        var registration = Disposable.Create(() => Providers.Remove(provider));
        owner.Track(registration);
        return registration;
    }

    internal static InventorySourceScope Current => _current;

    internal static bool Consuming => _current != null && _current.Consume;

    /// <summary>Opens a scope for one game call, or returns null when there is nothing to add.</summary>
    internal static InventorySourceScope Open(string kind, bool consume)
    {
        if (Providers.Count == 0 || _current != null || SaveGate.IsBusy || Inventory.instance == null)
            return null;

        var storages = Collect(kind);
        if (storages.Count == 0)
            return null;
        _current = new InventorySourceScope(kind, consume, storages);
        return _current;
    }

    internal static void Close(InventorySourceScope scope)
    {
        if (scope == null || scope.Closed)
            return;
        scope.Closed = true;
        if (ReferenceEquals(_current, scope))
            _current = null;
        if (scope.Consume)
            scope.Refresh();
    }

    internal static List<Storage> TenementStorages()
    {
        var tenement = TenementController.instance;
        return tenement != null ? ResourceStorages(tenement) : null;
    }

    private static List<Storage> Collect(string kind)
    {
        var context = new InventorySourceContext(kind);
        var bag = BackpackStorage.instance != null ? BackpackStorage.instance.GetStorage() : null;
        var tenement = kind == Renovation ? TenementStorages() : null;
        var found = new List<Storage>();
        var seen = new HashSet<Storage>();

        foreach (var provider in Providers.ToArray())
        {
            IEnumerable<Storage> given;
            try
            {
                given = provider(context);
            }
            catch (Exception e)
            {
                ItemsLog.Failed("An InventorySources provider", e);
                continue;
            }
            if (given == null)
                continue;

            foreach (var storage in given)
            {
                if (storage == null || storage.Slots == null || storage.Slots.Length == 0 || storage == bag)
                    continue;
                if (tenement != null && tenement.Contains(storage))
                    continue;
                if (seen.Add(storage))
                    found.Add(storage);
            }
        }
        return found;
    }
}
