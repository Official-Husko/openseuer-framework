// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
using System;
using System.Collections.Generic;
using ObenFramework.Items;
using ObenFramework.World;
using UnityEngine;

namespace ObenFramework.Env;

/// <summary>
/// Things that use or make power: appliances and generators (crafting stations with a power source),
/// grid consumers placed in the scene (<c>PowerGridListener</c>) and batteries (<c>BatteryPowerSource</c>).
/// Read them, switch them, and hear when they switch, run out of fuel or a battery starts or stops.
/// Grids themselves are in <see cref="PowerGrids"/>; light fixtures in <see cref="Lights"/>. Stations that
/// are always on and draw from no grid (flower pots, compost) are not listed. Ids work like the item
/// module's: a GUID, an address, or <c>furniture:{id}:{n}</c> for placed furniture. Lists scan the scene.
/// </summary>
public static class PowerDevices
{
    /// <summary>A device was switched, ran out of fuel, or a battery started or stopped. Not raised while a save loads.</summary>
    public static readonly WorldEvent<PowerDeviceChange> DeviceChanged = new WorldEvent<PowerDeviceChange>(nameof(DeviceChanged));

    /// <summary>Every power device in the loaded scenes.</summary>
    public static List<Component> All()
    {
        var result = new List<Component>();
        foreach (var station in ObjectIds.Active<CraftingBase>())
        {
            if (IsDevice(station))
                result.Add(station);
        }
        result.AddRange(ObjectIds.Active<PowerGridListener>());
        result.AddRange(ObjectIds.Active<BatteryPowerSource>());
        return result;
    }

    /// <summary>The device with this id, or null.</summary>
    public static Component Find(string id)
    {
        if (string.IsNullOrEmpty(id))
            return null;
        var station = ObjectIds.Find<CraftingBase>(id);
        if (station != null && station._Power != null)
            return station;
        var consumer = ObjectIds.Find<PowerGridListener>(id);
        if (consumer != null)
            return consumer;
        return ObjectIds.Find<BatteryPowerSource>(id);
    }

    public static string IdOf(Component device)
    {
        switch (device)
        {
            case CraftingBase station: return ObjectIds.IdOf(station);
            case PowerGridListener consumer: return ObjectIds.IdOf(consumer);
            case BatteryPowerSource battery: return ObjectIds.IdOf(battery);
            default: return null;
        }
    }

    /// <summary>The kind of a device, or null when the component is not one.</summary>
    public static PowerDeviceKind? KindOf(Component device)
    {
        switch (device)
        {
            case Generator _: return PowerDeviceKind.Generator;
            case CraftingBase station when station._Power != null: return PowerDeviceKind.Appliance;
            case PowerGridListener _: return PowerDeviceKind.Consumer;
            case BatteryPowerSource _: return PowerDeviceKind.Battery;
            default: return null;
        }
    }

    /// <summary>A snapshot of the device, or null when the component is not one.</summary>
    public static PowerDeviceInfo Describe(Component device)
    {
        if (device == null)
            return null;
        switch (device)
        {
            case CraftingBase station when station._Power != null: return PowerDeviceReader.Station(station);
            case PowerGridListener consumer: return PowerDeviceReader.Consumer(consumer);
            case BatteryPowerSource battery: return PowerDeviceReader.Battery(battery);
            default: return null;
        }
    }

    public static PowerDeviceInfo Describe(string id) => Describe(Find(id));

    /// <summary>
    /// Switches a device the way its own switch does. An appliance or generator may stay unpowered when its
    /// grid is full or it has no fuel (it is still switched on and starts when it can); always-on and broken
    /// stations refuse. A battery's switch turns its drain on: it feeds its outputs while it has charge.
    /// Returns false when the device refused.
    /// </summary>
    public static bool SetOn(Component device, bool on)
    {
        if (device == null)
            throw new ArgumentNullException(nameof(device));
        using (WorldChanges.EnterMod())
        {
            switch (device)
            {
                case CraftingBase station when station._Power != null:
                    if (station.AlwaysOn || (on && station.broken))
                        return false;
                    if (station.On != on)
                        station.Toggle(on);
                    return station.On == on;
                case PowerGridListener consumer:
                    if (consumer.On != on)
                        consumer.Toggle(on);
                    return consumer.On == on;
                case BatteryPowerSource battery:
                    battery.ToggleDrain(on);
                    return PowerDeviceReader.Draining(battery) == on;
                default:
                    throw new ArgumentException($"{device.GetType().Name} is not a power device.", nameof(device));
            }
        }
    }

    /// <summary>Switches the device with this id. Throws when there is none.</summary>
    public static bool SetOn(string id, bool on)
    {
        var device = Find(id);
        if (device == null)
            throw new ArgumentException($"No power device '{id}'.", nameof(id));
        return SetOn(device, on);
    }

    /// <summary>True while some generator in the loaded scenes runs (switched on and burning or otherwise powered). A scan.</summary>
    public static bool AnyGeneratorRunning
    {
        get
        {
            foreach (var generator in ObjectIds.Active<Generator>())
            {
                if (generator.PoweredAndActive)
                    return true;
            }
            return false;
        }
    }

    /// <summary>Stations that are always on and on no grid don't use power in any way a mod can change.</summary>
    internal static bool IsDevice(CraftingBase station) =>
        station != null && station._Power != null
        && (!station.AlwaysOn || station is Generator || station._Power.RequiresFuel || station._Power.PowerGrid != null);
}
