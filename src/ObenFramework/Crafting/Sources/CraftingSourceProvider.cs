using System;
using System.Collections.Generic;
using ObenFramework.Items;
using ObenLoader;
using ObenLoader.Runtime;

namespace ObenFramework.Crafting;

/// <summary>One registration with <see cref="CraftingSources"/>: who registered it, what it offers and in what order.</summary>
internal sealed class CraftingSourceProvider
{
    private readonly Func<StationInfo, IEnumerable<Storage>> _provider;
    private Func<Storage, int, StorageSlot, bool> _slotFilter;

    public CraftingSourceProvider(ITracker owner, Func<StationInfo, IEnumerable<Storage>> provider, int priority,
        Func<Storage, int, StorageSlot, bool> slotFilter, int order)
    {
        OwnerName = owner is ObenMod mod && mod.Info != null ? mod.Info.Id : owner.GetType().Name;
        _provider = provider;
        _slotFilter = slotFilter;
        Priority = priority;
        Order = order;
    }

    public string OwnerName { get; }

    public int Priority { get; set; }

    public int Order { get; }

    /// <summary>What <see cref="CraftingSources.Register"/> handed back; it identifies this registration.</summary>
    public IDisposable Handle { get; set; }

    public bool HasFilter => _slotFilter != null;

    public void SetFilter(Func<Storage, int, StorageSlot, bool> slotFilter) => _slotFilter = slotFilter;

    /// <summary>The provider's containers for a station, or null when it threw.</summary>
    public IEnumerable<Storage> Ask(StationInfo station)
    {
        try
        {
            return _provider(station);
        }
        catch (Exception e)
        {
            ItemsLog.Failed($"The crafting source provider of {OwnerName}", e);
            return null;
        }
    }

    /// <summary>Whether the slot may be used. A filter that throws refuses the slot.</summary>
    public bool Allows(Storage storage, int index, StorageSlot slot)
    {
        if (_slotFilter == null)
            return true;
        try
        {
            return _slotFilter(storage, index, slot);
        }
        catch (Exception e)
        {
            ItemsLog.Failed($"The crafting source slot filter of {OwnerName}", e);
            return false;
        }
    }
}
