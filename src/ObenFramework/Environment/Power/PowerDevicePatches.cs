using System;
using HarmonyLib;
using ObenFramework.World;
using UnityEngine;

namespace ObenFramework.Env;

/// <summary>Raises <see cref="PowerDevices.DeviceChanged"/>. Every hook returns at once while nobody listens.</summary>
[HarmonyPatch]
internal static class PowerDevicePatches
{
    private static bool Listening => PowerDevices.DeviceChanged.HasSubscribers && WorldChanges.Active;

    // ToggleDrain checks the battery's charge (which starts or stops it) before it stores the new switch, so a
    // start or stop inside it waits until the switch is stored.
    private static BatteryPowerSource _draining;
    private static PowerDeviceChangeKind? _heldBattery;

    [HarmonyPrefix]
    [HarmonyPatch(typeof(CraftingBase), nameof(CraftingBase.Toggle))]
    private static void BeforeStation(CraftingBase __instance, out bool __state) => __state = __instance.On;

    [HarmonyPostfix]
    [HarmonyPatch(typeof(CraftingBase), nameof(CraftingBase.Toggle))]
    private static void AfterStation(CraftingBase __instance, bool __state)
    {
        if (__instance.On == __state || !Listening || !PowerDevices.IsDevice(__instance))
            return;
        Raise(__instance, __instance.On ? PowerDeviceChangeKind.SwitchedOn : PowerDeviceChangeKind.SwitchedOff);
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(PowerGridListener), nameof(PowerGridListener.Toggle))]
    private static void BeforeConsumer(PowerGridListener __instance, out bool __state) => __state = __instance.On;

    [HarmonyPostfix]
    [HarmonyPatch(typeof(PowerGridListener), nameof(PowerGridListener.Toggle))]
    private static void AfterConsumer(PowerGridListener __instance, bool __state)
    {
        if (__instance.On == __state || !Listening)
            return;
        Raise(__instance, __instance.On ? PowerDeviceChangeKind.SwitchedOn : PowerDeviceChangeKind.SwitchedOff);
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(BatteryPowerSource), nameof(BatteryPowerSource.ToggleDrain))]
    private static void BeforeDrain(BatteryPowerSource __instance, out bool __state)
    {
        __state = PowerDeviceReader.Draining(__instance);
        _draining = __instance;
        _heldBattery = null;
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(BatteryPowerSource), nameof(BatteryPowerSource.ToggleDrain))]
    private static void AfterDrain(BatteryPowerSource __instance, bool __state)
    {
        var held = _heldBattery;
        _draining = null;
        _heldBattery = null;
        bool now = PowerDeviceReader.Draining(__instance);
        if (now != __state && Listening)
            Raise(__instance, now ? PowerDeviceChangeKind.SwitchedOn : PowerDeviceChangeKind.SwitchedOff);
        if (held.HasValue && Listening)
            Raise(__instance, held.Value);
    }

    [HarmonyFinalizer]
    [HarmonyPatch(typeof(BatteryPowerSource), nameof(BatteryPowerSource.ToggleDrain))]
    private static void DrainDone()
    {
        _draining = null;
        _heldBattery = null;
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(BatteryPowerSource), "PowerON")]
    private static void BeforeBatteryOn(BatteryPowerSource __instance, out bool __state) => __state = PowerDeviceReader.IsPowering(__instance);

    [HarmonyPostfix]
    [HarmonyPatch(typeof(BatteryPowerSource), "PowerON")]
    private static void AfterBatteryOn(BatteryPowerSource __instance, bool __state)
    {
        if (!__state && Listening && PowerDeviceReader.IsPowering(__instance))
            RaiseBattery(__instance, PowerDeviceChangeKind.BatteryStarted);
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(BatteryPowerSource), "PowerOFF")]
    private static void BeforeBatteryOff(BatteryPowerSource __instance, out bool __state) => __state = PowerDeviceReader.IsPowering(__instance);

    [HarmonyPostfix]
    [HarmonyPatch(typeof(BatteryPowerSource), "PowerOFF")]
    private static void AfterBatteryOff(BatteryPowerSource __instance, bool __state)
    {
        if (__state && Listening && !PowerDeviceReader.IsPowering(__instance))
            RaiseBattery(__instance, PowerDeviceChangeKind.BatteryStopped);
    }

    // OnFuelConsumeStopped also runs when the player switches a device off; only an empty counter means it ran out.
    [HarmonyPrefix]
    [HarmonyPatch(typeof(Fuel), nameof(Fuel.OnFuelConsumeStopped))]
    private static void BeforeFuelStop(Fuel __instance, out bool __state) => __state = __instance.fuelCounterOn && __instance.BurnTimeRemaining <= 0;

    [HarmonyPostfix]
    [HarmonyPatch(typeof(Fuel), nameof(Fuel.OnFuelConsumeStopped))]
    private static void AfterFuelStop(Fuel __instance, bool __state)
    {
        if (!__state || !Listening)
            return;
        try
        {
            var parent = __instance.Parent;
            var station = parent != null ? parent.GetComponent<CraftingBase>() : null;
            if (station != null)
                Raise(station, PowerDeviceChangeKind.FuelRanOut);
        }
        catch (Exception e)
        {
            WorldLog.Failed("Power device fuel hook", e);
        }
    }

    private static void RaiseBattery(BatteryPowerSource battery, PowerDeviceChangeKind kind)
    {
        if (ReferenceEquals(battery, _draining))
            _heldBattery = kind;
        else
            Raise(battery, kind);
    }

    private static void Raise(Component device, PowerDeviceChangeKind kind)
    {
        try
        {
            var deviceKind = PowerDevices.KindOf(device);
            if (deviceKind.HasValue)
                PowerDevices.DeviceChanged.Raise(new PowerDeviceChange(device, deviceKind.Value, kind, WorldChanges.Current));
        }
        catch (Exception e)
        {
            WorldLog.Failed("Power device hook", e);
        }
    }
}
