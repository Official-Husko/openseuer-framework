using ObenFramework.World;
using UnityEngine;

namespace ObenFramework.Env;

/// <summary>What sort of power device a component is.</summary>
public enum PowerDeviceKind
{
    /// <summary>A crafting station or appliance with a power source (stove, oven, grow light, distillery).</summary>
    Appliance,

    /// <summary>A generator: produces power for its grid while it burns fuel (camp fires are generators too).</summary>
    Generator,

    /// <summary>A scene object that draws from a grid (<c>PowerGridListener</c>): building parts, signs, machinery.</summary>
    Consumer,

    /// <summary>A battery or tank-fed source (<c>BatteryPowerSource</c>) that runs its relays while it has charge.</summary>
    Battery,
}

/// <summary>What happened to a power device, as raised by <see cref="PowerDevices.DeviceChanged"/>.</summary>
public enum PowerDeviceChangeKind
{
    SwitchedOn,
    SwitchedOff,

    /// <summary>A fuelled device burnt its last fuel and stopped.</summary>
    FuelRanOut,

    /// <summary>A battery started feeding its outputs (switched on with charge left).</summary>
    BatteryStarted,

    /// <summary>A battery stopped feeding its outputs (switched off or empty).</summary>
    BatteryStopped,
}

/// <summary>A change of one power device.</summary>
public readonly struct PowerDeviceChange
{
    public PowerDeviceChange(Component device, PowerDeviceKind deviceKind, PowerDeviceChangeKind kind, ChangeSource source)
    {
        Device = device;
        DeviceKind = deviceKind;
        Kind = kind;
        Source = source;
    }

    /// <summary>The <c>CraftingBase</c>, <c>PowerGridListener</c> or <c>BatteryPowerSource</c>.</summary>
    public Component Device { get; }

    public PowerDeviceKind DeviceKind { get; }

    public PowerDeviceChangeKind Kind { get; }

    public ChangeSource Source { get; }

    public string Id => PowerDevices.IdOf(Device);

    /// <summary>The device's state right after the change.</summary>
    public PowerDeviceInfo Info => PowerDevices.Describe(Device);
}

/// <summary>A snapshot of one power device.</summary>
public sealed class PowerDeviceInfo
{
    internal PowerDeviceInfo(string id, string name, PowerDeviceKind kind)
    {
        Id = id;
        Name = name;
        Kind = kind;
    }

    public string Id { get; }

    /// <summary>The scene object's name.</summary>
    public string Name { get; }

    public PowerDeviceKind Kind { get; }

    /// <summary>Its own switch (for a battery: the drain switch).</summary>
    public bool IsOn { get; internal set; }

    /// <summary>Switched on and actually running: powered by its grid, burning fuel, or feeding its outputs.</summary>
    public bool IsRunning { get; internal set; }

    /// <summary>Always on, can't be switched (compost, flower pots, kitchen tables).</summary>
    public bool AlwaysOn { get; internal set; }

    /// <summary>Broken and refusing to switch on until repaired.</summary>
    public bool Broken { get; internal set; }

    /// <summary>Watts drawn while on; negative for generators, which add capacity to their grid.</summary>
    public int Watts { get; internal set; }

    /// <summary>The grid it draws from or feeds, or null when it has none.</summary>
    public string GridId { get; internal set; }

    public bool RequiresFuel { get; internal set; }

    /// <summary>Game seconds of fuel already burning; 0 without fuel.</summary>
    public int FuelSeconds { get; internal set; }

    /// <summary>The most burn time the fuel slot holds, in game seconds.</summary>
    public float MaxFuelSeconds { get; internal set; }

    /// <summary>Fuel is burning right now.</summary>
    public bool Burning { get; internal set; }

    /// <summary>A battery's charge (ammo or liquid units in its storage) and the most it holds.</summary>
    public float Charge { get; internal set; }

    public float MaxCharge { get; internal set; }
}
