using System.Collections.Generic;
using ObenFramework.Patching;
using ObenFramework.Saves;
using ObenFramework.World.Addressing;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ObenFramework.World;

/// <summary>What happened to a power grid.</summary>
public enum PowerGridChangeKind
{
    /// <summary>The grid was switched on or off (the main breaker, a power meter).</summary>
    Toggled,

    /// <summary>The grid's base capacity was set (an electricity upgrade in the tenement).</summary>
    CapacityChanged,
}

/// <summary>A change of a grid, as raised by <see cref="PowerGrids.GridChanged"/>.</summary>
public readonly struct PowerGridChange
{
    public PowerGridChange(PowerGrid grid, PowerGridChangeKind kind, ChangeSource source)
    {
        Grid = grid;
        Kind = kind;
        Source = source;
    }

    public PowerGrid Grid { get; }

    public PowerGridChangeKind Kind { get; }

    public ChangeSource Source { get; }

    /// <summary>The grid's numbers right after the change.</summary>
    public PowerGridInfo Info => PowerGrids.Describe(Grid);
}

/// <summary>A snapshot of one power grid. Watts, as in the game's power meters; negative balance is consumption.</summary>
public readonly struct PowerGridInfo
{
    public PowerGridInfo(string id, string name, bool enabled, int baseCapacity, int localBalance, int netBalance,
        int totalCapacity, int freeCapacity, bool flickering, bool unlimited, bool unstable, string parentId)
    {
        Id = id;
        Name = name;
        Enabled = enabled;
        BaseCapacity = baseCapacity;
        LocalBalance = localBalance;
        NetBalance = netBalance;
        TotalCapacity = totalCapacity;
        FreeCapacity = freeCapacity;
        Flickering = flickering;
        Unlimited = unlimited;
        Unstable = unstable;
        ParentId = parentId;
    }

    public string Id { get; }

    public string Name { get; }

    /// <summary>The grid's breaker: false means everything on it is without power.</summary>
    public bool Enabled { get; }

    /// <summary>Capacity the grid has by itself, before generators and child grids.</summary>
    public int BaseCapacity { get; }

    /// <summary>Sum of what devices on this grid itself produce (positive) and consume (negative).</summary>
    public int LocalBalance { get; }

    /// <summary>Production minus consumption over this grid and its children.</summary>
    public int NetBalance { get; }

    public int TotalCapacity { get; }

    /// <summary>What can still be switched on before the grid is overloaded.</summary>
    public int FreeCapacity { get; }

    /// <summary>Lights on it flicker: a generator is feeding it, or its supply is unstable.</summary>
    public bool Flickering { get; }

    /// <summary>It, or a grid above it, has no capacity limit.</summary>
    public bool Unlimited { get; }

    public bool Unstable { get; }

    /// <summary>The id of the grid this one hangs on, or null.</summary>
    public string ParentId { get; }
}

/// <summary>
/// Power grids. A grid is a node with a capacity and a set of listeners (lights, appliances, generators)
/// that it switches off, most recently switched on first, when they use more than it offers. Which
/// listener loses power depends on the order they were switched on, so the outcome is only reproducible
/// with the same sequence of events. The devices on a grid are in <c>ObenFramework.Env.PowerDevices</c>.
/// Changes are refused (false) while a save is written or loaded.
/// </summary>
public static class PowerGrids
{
    /// <summary>
    /// A grid was switched or its base capacity set. Raised from the game's <c>Toggle</c> and
    /// <c>SetBaseBalance</c>; changes that follow from devices being switched are not events (read
    /// <see cref="Describe"/> when you need the numbers).
    /// </summary>
    public static readonly WorldEvent<PowerGridChange> GridChanged = new WorldEvent<PowerGridChange>(nameof(GridChanged));

    /// <summary>True when the hooks are in place, so <see cref="GridChanged"/> is raised.</summary>
    public static bool IsAvailable => PatchAreas.IsAvailable(PatchAreas.Framework, "World");

    /// <summary>Every grid in the loaded scenes.</summary>
    public static List<PowerGrid> All(bool includeInactive = false) =>
        new List<PowerGrid>(Object.FindObjectsByType<PowerGrid>(includeInactive ? FindObjectsInactive.Include : FindObjectsInactive.Exclude, FindObjectsSortMode.None));

    public static PowerGrid Find(string id) => ObjectResolver.Find<PowerGrid>(id);

    /// <summary>The tenement's main grid, or null outside the tenement or before it exists.</summary>
    public static PowerGrid TenementMain
    {
        get
        {
            var tenement = TenementController.instance;
            return tenement != null && tenement.powerMain != null ? tenement.powerMain : null;
        }
    }

    /// <summary>GUID where the editor set one, else the full address text.</summary>
    public static string IdOf(PowerGrid grid)
    {
        if (grid == null)
            return null;
        return string.IsNullOrEmpty(grid.GUID) ? ObjectAddress.Of(grid).ToString() : grid.GUID;
    }

    /// <summary>Switches the grid's breaker the way a power meter or the main switch does (<c>Toggle</c>). The grid saves it.</summary>
    public static bool SetEnabled(PowerGrid grid, bool enabled)
    {
        if (grid == null)
            throw new System.ArgumentNullException(nameof(grid));
        if (grid.GridEnabled == enabled)
            return true;
        if (SaveGate.IsBusy)
            return false;
        using (WorldChanges.EnterMod())
            grid.Toggle(enabled);
        return grid.GridEnabled == enabled;
    }

    /// <summary>
    /// Sets the capacity the grid has by itself, in watts (<c>SetBaseBalance</c>). The tenement sets its main
    /// grid's capacity from the electricity upgrade again whenever its utilities update, so a change there
    /// lasts until the next upgrade or level load.
    /// </summary>
    public static bool SetBaseCapacity(PowerGrid grid, int watts)
    {
        if (grid == null)
            throw new System.ArgumentNullException(nameof(grid));
        if (watts < 0)
            throw new System.ArgumentOutOfRangeException(nameof(watts), "Give 0 or more watts.");
        if (SaveGate.IsBusy)
            return false;
        using (WorldChanges.EnterMod())
            grid.SetBaseBalance(watts);
        return grid.BaseCapacity == watts;
    }

    public static PowerGridInfo Describe(PowerGrid grid) => new PowerGridInfo(
        IdOf(grid), grid.GridName, grid.GridEnabled, grid.BaseCapacity, grid.LocalBalance, grid.NetBalance,
        grid.TotalCapacity, grid.FreeCapacity, grid.Flickering, grid.Unlimited, grid.unstableSupply,
        grid.ParentGrid != null ? IdOf(grid.ParentGrid) : null);
}
