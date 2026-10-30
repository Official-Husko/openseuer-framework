// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
using System;
using System.Collections.Generic;
using ObenFramework.Game;
using ObenFramework.Saves;
using ObenLoader;
using ObenLoader.Mods;
using ObenLoader.Runtime;

namespace ObenFramework.Items;

/// <summary>What the framework keeps per save for <see cref="ItemConsumables.SetForSave"/>: the values in the order they were set.</summary>
public sealed class ConsumableSave
{
    public List<SavedConsumable> Entries = new List<SavedConsumable>();
}

/// <summary>One mod's values for one item in a save.</summary>
public sealed class SavedConsumable
{
    public string Mod;
    public int ItemId;
    public ConsumableValues Values;
}

public static partial class ItemConsumables
{
    private static ModSaveData<ConsumableSave> _saved;

    // The per-save values in effect now, one per mod and item.
    private static readonly List<SavedHold> Held = new List<SavedHold>();

    /// <summary>
    /// Sets an item's values for the loaded save, like <see cref="Set"/> but kept in the save: they come back
    /// each time it loads with <paramref name="owner"/> loaded, and other saves don't get them. A mod has one
    /// such value per item; setting it again replaces it. They leave the item while the mod is unloaded and
    /// stay in the save. False at the main menu, while a save is written or loaded, when the item isn't
    /// consumable or a value is not a finite number.
    /// </summary>
    public static bool SetForSave(ObenMod owner, int itemId, ConsumableValues values)
    {
        if (owner == null)
            throw new ArgumentNullException(nameof(owner));
        if (_saved == null || values == null || !values.IsFinite() || !WorldState.InWorld || SaveGate.IsBusy || Find(itemId) == null)
            return false;

        string mod = owner.Info.Id;
        var entries = _saved.Value.Entries;
        entries.RemoveAll(e => Is(e, mod, itemId));
        entries.Add(new SavedConsumable { Mod = mod, ItemId = itemId, Values = values.Copy() });
        FindHold(mod, itemId)?.Registration.Dispose();
        Hold(owner, itemId, values.Copy());
        return true;
    }

    /// <summary>
    /// Takes the mod's per-save values of an item out of the loaded save; the one under them, or the game's
    /// own, comes back. False when there were none or a save is being written or loaded.
    /// </summary>
    public static bool ClearForSave(ObenMod owner, int itemId)
    {
        if (owner == null)
            throw new ArgumentNullException(nameof(owner));
        if (_saved == null || SaveGate.IsBusy)
            return false;

        string mod = owner.Info.Id;
        bool removed = _saved.Value.Entries.RemoveAll(e => Is(e, mod, itemId)) > 0;
        FindHold(mod, itemId)?.Registration.Dispose();
        return removed;
    }

    /// <summary>
    /// A copy of the values a mod set for an item in the loaded save, whether that mod is loaded or not; null
    /// when it has none or no save is loaded.
    /// </summary>
    public static ConsumableValues SavedValues(string modId, int itemId)
    {
        if (_saved == null)
            return null;
        foreach (var entry in _saved.Value.Entries)
        {
            if (Is(entry, modId, itemId))
                return entry.Values?.Copy();
        }
        return null;
    }

    internal static void Init(ObenMod framework)
    {
        _saved = ModSaves.Register<ConsumableSave>(framework, "consumables");
        _saved.DataLoaded.Subscribe(framework, _ => Refresh());
        ModRegistry.ModLoaded += OnModLoaded;
        framework.Track(Disposable.Create(() =>
        {
            ModRegistry.ModLoaded -= OnModLoaded;
            foreach (var hold in Held.ToArray())
                hold.Registration.Dispose();
            _saved = null;
        }));
        Refresh();
    }

    // Another save's data (or the main menu's empty one): values it doesn't hold leave, the ones it holds
    // unchanged stay where they are in the stack, and the rest are put on top.
    private static void Refresh()
    {
        var entries = _saved?.Value.Entries;
        foreach (var hold in Held.ToArray())
        {
            var entry = entries?.Find(e => Is(e, hold.Mod, hold.ItemId));
            if (entry == null || !hold.Values.SameAs(entry.Values))
                hold.Registration.Dispose();
        }
        if (entries == null)
            return;
        foreach (var entry in entries.ToArray())
            Apply(entry, ModRegistry.Find(entry.Mod));
    }

    private static void OnModLoaded(ModInfo mod)
    {
        try
        {
            foreach (var entry in _saved?.Value.Entries.ToArray() ?? Array.Empty<SavedConsumable>())
            {
                if (string.Equals(entry.Mod, mod.Id, StringComparison.OrdinalIgnoreCase))
                    Apply(entry, mod);
            }
        }
        catch (Exception e)
        {
            ItemsLog.Failed("Applying a mod's saved consumable values", e);
        }
    }

    private static void Apply(SavedConsumable entry, ModInfo mod)
    {
        if (entry?.Values == null || !entry.Values.IsFinite() || mod?.Instance == null || mod.State != ModState.Loaded)
            return;
        if (FindHold(entry.Mod, entry.ItemId) == null)
            Hold(mod.Instance, entry.ItemId, entry.Values.Copy());
    }

    private static void Hold(ObenMod owner, int itemId, ConsumableValues values)
    {
        var hold = new SavedHold { Mod = owner.Info.Id, ItemId = itemId, Values = values };
        hold.Registration = Push(itemId, values, () => Held.Remove(hold));
        if (hold.Registration == null)
            return;
        Held.Add(hold);
        owner.Track(hold.Registration);
    }

    private static SavedHold FindHold(string mod, int itemId) =>
        Held.Find(h => h.ItemId == itemId && string.Equals(h.Mod, mod, StringComparison.OrdinalIgnoreCase));

    private static bool Is(SavedConsumable entry, string mod, int itemId) =>
        entry != null && entry.ItemId == itemId && string.Equals(entry.Mod, mod, StringComparison.OrdinalIgnoreCase);

    private sealed class SavedHold
    {
        public string Mod;
        public int ItemId;
        public ConsumableValues Values;
        public IDisposable Registration;
    }
}
