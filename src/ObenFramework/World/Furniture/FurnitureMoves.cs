using System;
using System.Collections.Generic;

namespace ObenFramework.World;

/// <summary>
/// Bookkeeping behind the furniture events. The build menu's "move" is a take followed by a place of the
/// same piece, possibly many seconds apart while the player aims it, so the take is held as a pending
/// move: the next place of that kind of furniture turns it into <see cref="PlacedFurniture.FurnitureMoved"/>,
/// anything else (closing the menu, taking something else, leaving) reports it as a removal.
/// </summary>
internal static class FurnitureMoves
{
    internal struct Taken
    {
        public bool Valid;
        public bool MoveRoot;
        public FurnitureInfo Before;
    }

    private static readonly List<FurniturePlaceable> Placed = new List<FurniturePlaceable>();
    private static bool _placing;
    private static int _moveDepth;
    private static bool _rootTaken;

    private static bool _pending;
    private static FurnitureInfo _pendingBefore;
    private static Furniture _pendingType;
    private static ChangeSource _pendingSource;

    public static void Reset()
    {
        Placed.Clear();
        _placing = false;
        _moveDepth = 0;
        _rootTaken = false;
        _pending = false;
    }

    public static void BeginMove()
    {
        _moveDepth++;
        if (_moveDepth == 1)
        {
            _rootTaken = false;
            FlushPending();
        }
    }

    public static void EndMove()
    {
        if (_moveDepth > 0)
            _moveDepth--;
    }

    public static void BeginPlace()
    {
        _placing = true;
        Placed.Clear();
    }

    /// <summary>A piece got its furniture info. Inside the build menu's place it is collected until the place is done.</summary>
    public static void OnPlaced(FurniturePlaceable placeable)
    {
        if (!PlacedFurniture.AnySubscribers || !WorldChanges.Active)
            return;

        if (_placing)
            Placed.Add(placeable);
        else
            PlacedFurniture.FurniturePlaced.Raise(new FurnitureChange(PlacedFurniture.Describe(placeable), WorldChanges.Current));
    }

    public static void EndPlace()
    {
        _placing = false;
        try
        {
            if (!PlacedFurniture.AnySubscribers || !WorldChanges.Active)
                return;
            foreach (var placeable in Placed)
            {
                if (placeable != null)
                    Resolve(placeable);
            }
        }
        finally
        {
            Placed.Clear();
        }
    }

    public static Taken BeforeTake(FurniturePlaceable placeable)
    {
        var taken = new Taken();
        if (placeable.cantTake || placeable.furniture == null || !PlacedFurniture.AnySubscribers || !WorldChanges.Active)
            return taken;

        if (_moveDepth == 0)
            FlushPending();
        taken.Valid = true;
        taken.MoveRoot = _moveDepth > 0 && !_rootTaken;
        taken.Before = PlacedFurniture.Describe(placeable);
        if (taken.MoveRoot)
            _rootTaken = true;
        return taken;
    }

    public static void AfterTake(FurniturePlaceable placeable, Taken taken)
    {
        if (!taken.Valid)
            return;

        PlacedFurniture.Forget(placeable);
        if (taken.MoveRoot)
        {
            _pending = true;
            _pendingBefore = taken.Before;
            _pendingType = placeable.furniture;
            _pendingSource = WorldChanges.Current;
        }
        else
        {
            PlacedFurniture.FurnitureRemoved.Raise(new FurnitureChange(taken.Before, WorldChanges.Current));
        }
    }

    /// <summary>Reports a held-back take as a removal. Safe to call any time.</summary>
    public static void FlushPending()
    {
        if (!_pending)
            return;

        _pending = false;
        var before = _pendingBefore;
        var source = _pendingSource;
        try
        {
            if (WorldChanges.Active)
                PlacedFurniture.FurnitureRemoved.Raise(new FurnitureChange(before, source));
        }
        catch (Exception e)
        {
            WorldLog.Failed("Furniture removal", e);
        }
    }

    private static void Resolve(FurniturePlaceable placeable)
    {
        var source = WorldChanges.Current;
        if (_pending && placeable.furniture == _pendingType)
        {
            _pending = false;
            PlacedFurniture.Adopt(placeable, _pendingBefore.Id);
            PlacedFurniture.FurnitureMoved.Raise(new FurnitureMove(_pendingBefore, PlacedFurniture.Describe(placeable), source));
            return;
        }

        FlushPending();
        PlacedFurniture.FurniturePlaced.Raise(new FurnitureChange(PlacedFurniture.Describe(placeable), source));
    }
}
