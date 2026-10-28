using System;
using System.Collections.Generic;
using ObenFramework.Saves;
using ObenFramework.World;

namespace ObenFramework.Items;

/// <summary>
/// Recounts the player's inventory after something changed a stack and raises <see cref="InventoryEvents"/>
/// for the difference. The Harmony hooks only set a flag; the recount walks the slot arrays directly
/// (no <c>Inventory.AllSlots</c>, which allocates) and reuses its dictionaries, so a frame without changes
/// costs one bool check and a recount without differences allocates nothing.
/// </summary>
internal static class InventoryWatch
{
    private const int RightHand = 6;
    private const int LeftHand = 7;

    private static readonly ItemTally Tally = new ItemTally();
    private static readonly List<KeyValuePair<int, int>> Changes = new List<KeyValuePair<int, int>>();

    private static bool _dirty;
    private static bool _checking;
    private static ChangeSource _source;
    private static Inventory _counted;
    private static int _right = -1;
    private static int _left = -1;

    /// <summary>A stack somewhere may have changed. Called from the hooks, so it does nothing else.</summary>
    public static void MarkDirty()
    {
        _dirty = true;
        var current = WorldChanges.Current;
        // Mod beats LocalPlayer beats Game: a frame with any mod change is reported as the mod's.
        if (current > _source)
            _source = current;
    }

    /// <summary>Once per frame from the framework.</summary>
    public static void Update()
    {
        if (_dirty)
            Check();
    }

    /// <summary>Forgets the baseline, so the next count starts over without events. Called when the scene changes.</summary>
    public static void Reset()
    {
        Tally.Forget();
        _counted = null;
        _dirty = true;
        _source = ChangeSource.Game;
        _right = _left = -1;
    }

    /// <summary>Recounts now. A handler that changes the inventory again is reported on the next frame.</summary>
    public static void Check()
    {
        if (_checking)
            return;
        _dirty = false;
        var source = _source;
        _source = ChangeSource.Game;

        var inventory = Inventory.instance;
        if (inventory == null || inventory.Slots == null || inventory.CharacterSlots == null)
        {
            if (Tally.HasBaseline)
                Reset();
            _dirty = false;
            return;
        }

        _checking = true;
        try
        {
            Count(inventory);
            bool settled = WorldChanges.Active && !SaveGate.IsLoading;
            if (!settled || !Tally.HasBaseline || !ReferenceEquals(inventory, _counted))
            {
                // A new scene, a load or a first count: this is the starting point, not a change.
                Tally.Commit();
                _counted = inventory;
                _right = HandItem(inventory, RightHand);
                _left = HandItem(inventory, LeftHand);
                return;
            }

            Tally.Diff(Changes);
            Tally.Commit();
            RaiseCounts(source);
            RaiseHeld(inventory);
        }
        catch (Exception e)
        {
            ItemsLog.Error("Counting the player's inventory failed", e);
        }
        finally
        {
            _checking = false;
        }
    }

    private static void Count(Inventory inventory)
    {
        Tally.Begin();
        AddSlots(inventory.Slots);
        AddSlots(inventory.CharacterSlots);
        AddSlots(PlayerSlots.BagSlots(inventory));
    }

    private static void AddSlots(SlotController[] slots)
    {
        if (slots == null)
            return;
        for (int i = 0; i < slots.Length; i++)
        {
            var stack = slots[i] != null ? slots[i].itemStack : null;
            if (stack != null)
                Tally.Add(stack.itemId, stack.itemAmount);
        }
    }

    private static void RaiseCounts(ChangeSource source)
    {
        if (Changes.Count == 0)
            return;
        bool added = InventoryEvents.ItemAdded.HasSubscribers;
        bool removed = InventoryEvents.ItemRemoved.HasSubscribers;
        if (!added && !removed)
            return;

        // Handlers can't nest into this list: a change they make is counted on the next frame.
        for (int i = 0; i < Changes.Count; i++)
        {
            var change = Changes[i];
            var info = new InventoryChange(change.Key, Math.Abs(change.Value), Tally.Committed(change.Key), source);
            if (change.Value > 0 && added)
                InventoryEvents.ItemAdded.Raise(info);
            else if (change.Value < 0 && removed)
                InventoryEvents.ItemRemoved.Raise(info);
        }
    }

    private static void RaiseHeld(Inventory inventory)
    {
        int right = HandItem(inventory, RightHand);
        int left = HandItem(inventory, LeftHand);
        int oldRight = _right, oldLeft = _left;
        _right = right;
        _left = left;
        if (!InventoryEvents.HeldChanged.HasSubscribers)
            return;
        if (right != oldRight)
            InventoryEvents.HeldChanged.Raise(new HeldChange(false, right, oldRight));
        if (left != oldLeft)
            InventoryEvents.HeldChanged.Raise(new HeldChange(true, left, oldLeft));
    }

    internal static int HandItem(Inventory inventory, int index)
    {
        var slots = inventory.CharacterSlots;
        if (slots == null || index >= slots.Length || slots[index] == null)
            return -1;
        var stack = slots[index].itemStack;
        return stack != null && stack.itemId >= 0 && stack.itemAmount > 0 ? stack.itemId : -1;
    }
}
