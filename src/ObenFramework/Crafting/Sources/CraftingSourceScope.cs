// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
using System;
using System.Collections.Generic;
using ObenFramework.Items;
using OS.Items;

namespace ObenFramework.Crafting;

/// <summary>A container offered by a provider for one station.</summary>
internal readonly struct CraftingSource
{
    public CraftingSource(CraftingSourceProvider provider, Storage storage)
    {
        Provider = provider;
        Storage = storage;
    }

    public CraftingSourceProvider Provider { get; }

    public Storage Storage { get; }
}

/// <summary>
/// One station call that sees the extra sources: the station's own input slots are swapped for the own plus
/// the extra slots, and swapped back when the call ends. For a call that takes ingredients the extra slots are
/// snapshotted first, so what was taken can be reported.
/// </summary>
internal sealed class CraftingSourceScope
{
    private List<Snapshot> _snapshots;

    public CraftingSourceScope(CraftingBase station, bool consume, List<CraftingSource> sources, StorageSlot[] own, StorageSlot[] combined)
    {
        Station = station;
        Consume = consume;
        Sources = sources;
        Own = own;
        Combined = combined;
        if (consume)
            _snapshots = Capture(sources);
    }

    public CraftingBase Station { get; }

    public bool Consume { get; }

    public List<CraftingSource> Sources { get; }

    public StorageSlot[] Own { get; }

    public StorageSlot[] Combined { get; }

    public bool Closed { get; set; }

    /// <summary>The own slots followed by each source's slots; slots a filter refuses become empty stand-ins.</summary>
    public static StorageSlot[] Combine(StorageSlot[] own, List<CraftingSource> sources)
    {
        var result = new List<StorageSlot>(own);
        var seen = new List<StorageSlot[]> { own };
        foreach (var source in sources)
        {
            var extra = source.Storage.storageSlots;
            if (extra == null || extra.Length == 0 || seen.Contains(extra))
                continue;
            seen.Add(extra);
            for (int i = 0; i < extra.Length; i++)
                result.Add(extra[i] != null && source.Provider.Allows(source.Storage, i, extra[i]) ? extra[i] : new StorageSlot());
        }
        return result.ToArray();
    }

    /// <summary>What the call took out of the extra slots, per slot.</summary>
    public List<CraftingTaken> Taken()
    {
        var taken = new List<CraftingTaken>();
        if (_snapshots == null)
            return taken;
        foreach (var before in _snapshots)
        {
            if (before.ItemId == -1 || before.Amount <= 0)
                continue;
            int left = before.Slot.itemId == before.ItemId ? before.Slot.itemAmount : 0;
            int gone = before.Amount - left;
            float poured = LiquidGone(before);
            if (gone <= 0 && poured <= 0f)
                continue;
            taken.Add(new CraftingTaken(before.Owner, before.Storage, before.Index, before.ItemId, Math.Max(gone, 0),
                poured > 0f ? before.LiquidItemId : -1, Math.Max(poured, 0f), Station));
        }
        return taken;
    }

    /// <summary>Lets the extra containers' windows show what was taken.</summary>
    public void Refresh()
    {
        foreach (var source in Sources)
        {
            try
            {
                if (source.Storage != null && source.Storage.Slots != null)
                    source.Storage.UpdateSlots();
            }
            catch (Exception e)
            {
                ItemsLog.Failed("Refreshing an extra crafting source", e);
            }
        }
    }

    private static List<Snapshot> Capture(List<CraftingSource> sources)
    {
        var snapshots = new List<Snapshot>();
        foreach (var source in sources)
        {
            var slots = source.Storage.storageSlots;
            if (slots == null)
                continue;
            for (int i = 0; i < slots.Length; i++)
            {
                var slot = slots[i];
                if (slot == null || slot.itemId == -1)
                    continue;
                var liquid = slot.GetMetaOfType<ItemLiquidData>();
                bool hasLiquid = liquid != null && liquid.liquidItem != null;
                snapshots.Add(new Snapshot(source.Provider.OwnerName, source.Storage, slot, i, slot.itemId, slot.itemAmount,
                    hasLiquid ? liquid.liquidItem.ID : -1, hasLiquid ? liquid.GetLiquidAmount(slot.itemAmount) : 0f));
            }
        }
        return snapshots;
    }

    private static float LiquidGone(Snapshot before)
    {
        if (before.LiquidItemId == -1 || before.LiquidAmount <= 0f)
            return 0f;
        var liquid = before.Slot.GetMetaOfType<ItemLiquidData>();
        float left = liquid != null && liquid.liquidItem != null && liquid.liquidItem.ID == before.LiquidItemId
            ? liquid.GetLiquidAmount(before.Slot.itemAmount)
            : 0f;
        return ItemLiquidData.IsGreaterThan(before.LiquidAmount, left) ? before.LiquidAmount - left : 0f;
    }

    private readonly struct Snapshot
    {
        public Snapshot(string owner, Storage storage, StorageSlot slot, int index, int itemId, int amount, int liquidItemId, float liquidAmount)
        {
            Owner = owner;
            Storage = storage;
            Slot = slot;
            Index = index;
            ItemId = itemId;
            Amount = amount;
            LiquidItemId = liquidItemId;
            LiquidAmount = liquidAmount;
        }

        public string Owner { get; }
        public Storage Storage { get; }
        public StorageSlot Slot { get; }
        public int Index { get; }
        public int ItemId { get; }
        public int Amount { get; }
        public int LiquidItemId { get; }
        public float LiquidAmount { get; }
    }
}
