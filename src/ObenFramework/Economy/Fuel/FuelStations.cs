// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
using System;
using System.Collections.Generic;
using ObenFramework.Patching;
using ObenFramework.Saves;
using ObenFramework.World;
using ObenFramework.World.Addressing;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ObenFramework.Economy;

/// <summary>
/// Fuel stations, where the player fills up a minitrain and pays for the litres at the counter (the one in
/// the game is in Open Sewer Mines). The price per litre changes now and then between the station's minimum
/// and maximum. This lists stations, sets a price until the game next changes it, and reports price changes
/// and paid fuel. Call on the main thread.
/// </summary>
public static class FuelStations
{
    /// <summary>True when the fuel hooks are in place, so the station events are raised.</summary>
    public static bool IsAvailable => PatchAreas.IsAvailable(PatchAreas.Framework, "Economy");

    /// <summary>A station's price per litre changed (the game's own change, a mod's, or loading a save).</summary>
    public static readonly WorldEvent<FuelStationInfo> PriceChanged = new WorldEvent<FuelStationInfo>(nameof(PriceChanged));

    /// <summary>The player paid for the fuel pumped.</summary>
    public static readonly WorldEvent<FuelPurchase> FuelBought = new WorldEvent<FuelPurchase>(nameof(FuelBought));

    /// <summary>Every fuel station of the loaded scenes.</summary>
    public static List<FuelStationInfo> All()
    {
        var list = new List<FuelStationInfo>();
        foreach (var station in Object.FindObjectsByType<GasStation>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            list.Add(new FuelStationInfo(station));
        return list;
    }

    /// <summary>The station named by address, GUID, path or its receipt name, or null.</summary>
    public static FuelStationInfo Find(string key)
    {
        var station = Resolve(key);
        return station != null ? new FuelStationInfo(station) : null;
    }

    /// <summary>
    /// Sets the price per litre and updates the pumps' displays, as the game's own price change does. It holds
    /// until the station picks a new random price on its schedule. Fuel already pumped keeps its price.
    /// </summary>
    public static bool SetPrice(string key, int price)
    {
        if (price < 0 || SaveGate.IsBusy || Money.instance == null)
            return false;
        var station = Resolve(key);
        if (station == null)
            return false;
        try
        {
            FuelFields.SetPrice(station, price);
            station.UpdatePrices();
            return true;
        }
        catch (Exception e)
        {
            WorldLog.Failed("FuelStations.SetPrice", e);
            return false;
        }
    }

    internal static GasStation Resolve(string key)
    {
        if (string.IsNullOrEmpty(key))
            return null;
        var found = ObjectResolver.Find<GasStation>(key);
        if (found != null)
            return found;
        foreach (var station in Object.FindObjectsByType<GasStation>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (string.Equals(FuelFields.Name(station), key, StringComparison.OrdinalIgnoreCase))
                return station;
        }
        return null;
    }
}
