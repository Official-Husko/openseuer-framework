// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
using HarmonyLib;
using ObenFramework.World;
using OS.Items;

namespace ObenFramework.Env;

/// <summary>Reads the three kinds of power device into <see cref="PowerDeviceInfo"/>.</summary>
internal static class PowerDeviceReader
{
    private static readonly AccessTools.FieldRef<PowerGridListener, PowerGrid> ConsumerGrid =
        AccessTools.FieldRefAccess<PowerGridListener, PowerGrid>("powerGrid");
    private static readonly AccessTools.FieldRef<BatteryPowerSource, bool> DrainEnabled =
        AccessTools.FieldRefAccess<BatteryPowerSource, bool>("drainEnabled");
    private static readonly AccessTools.FieldRef<BatteryPowerSource, bool> Powering =
        AccessTools.FieldRefAccess<BatteryPowerSource, bool>("powering");

    public static bool Draining(BatteryPowerSource battery) => DrainEnabled(battery);

    public static bool IsPowering(BatteryPowerSource battery) => Powering(battery);

    public static PowerGrid GridOf(PowerGridListener consumer) => ConsumerGrid(consumer);

    public static PowerDeviceInfo Station(CraftingBase station)
    {
        var power = station._Power;
        var info = new PowerDeviceInfo(PowerDevices.IdOf(station), station.name, station is Generator ? PowerDeviceKind.Generator : PowerDeviceKind.Appliance)
        {
            IsOn = station.On,
            IsRunning = station.PoweredAndActive,
            AlwaysOn = station.AlwaysOn,
            Broken = station.broken,
            Watts = power.powerConsumption,
            GridId = power.PowerGrid != null ? PowerGrids.IdOf(power.PowerGrid) : null,
            RequiresFuel = power.RequiresFuel,
        };
        var fuel = power.RequiresFuel ? power._Fuel : null;
        if (fuel != null)
        {
            info.FuelSeconds = fuel.BurnTimeRemaining;
            info.MaxFuelSeconds = fuel.MaxBurnTime;
            info.Burning = fuel.active;
            info.BurnTimeFactor = fuel.BurnTimeFactor;
            if (fuel.FuelCategories != null)
                info.FuelCategories = (string[])fuel.FuelCategories.Clone();
        }
        return info;
    }

    public static PowerDeviceInfo Consumer(PowerGridListener consumer)
    {
        var grid = ConsumerGrid(consumer);
        return new PowerDeviceInfo(PowerDevices.IdOf(consumer), consumer.name, PowerDeviceKind.Consumer)
        {
            IsOn = consumer.On,
            IsRunning = consumer.On && grid != null && grid.IsPowered(consumer),
            Watts = consumer.PowerConsumption,
            GridId = grid != null ? PowerGrids.IdOf(grid) : null,
        };
    }

    public static PowerDeviceInfo Battery(BatteryPowerSource battery)
    {
        var info = new PowerDeviceInfo(PowerDevices.IdOf(battery), battery.name, PowerDeviceKind.Battery)
        {
            IsOn = DrainEnabled(battery),
            IsRunning = Powering(battery),
        };
        ReadCharge(battery, out float charge, out float max);
        info.Charge = charge;
        info.MaxCharge = max;
        return info;
    }

    // A battery's charge is the ammo (cells) or liquid (fuel tanks) of the items in its storage.
    private static void ReadCharge(BatteryPowerSource battery, out float charge, out float max)
    {
        charge = 0f;
        max = 0f;
        var storage = battery.GetComponent<Storage>();
        if (storage == null || storage.storageSlots == null)
            return;
        bool ammo = (battery.fuelType & BatteryPowerSource.FuelType.ItemAmmoData) != 0;
        bool liquid = (battery.fuelType & BatteryPowerSource.FuelType.ItemLiquidData) != 0;
        foreach (var stack in storage.storageSlots)
        {
            if (stack == null || stack.itemId == -1)
                continue;
            if (ammo)
            {
                var data = stack.GetMetaOfType<ItemAmmodata>();
                if (data != null)
                {
                    charge += data.ammo;
                    max += data.maxAmmo;
                }
            }
            if (liquid)
            {
                var data = stack.GetMetaOfType<ItemLiquidData>();
                if (data != null)
                {
                    charge += data.GetLiquidAmount(stack.itemAmount);
                    max += data.GetLiquidMaxAmount(stack.itemAmount);
                }
            }
        }
    }
}
