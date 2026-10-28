using HarmonyLib;
using Currency = Money.Currency;

namespace ObenFramework.Economy;

/// <summary>The private fields of the game's fuel station and pumps, bound once.</summary>
internal static class FuelFields
{
    private static readonly AccessTools.FieldRef<GasStation, string> NameRef = AccessTools.FieldRefAccess<GasStation, string>("gastationName");
    private static readonly AccessTools.FieldRef<GasStation, int> PriceRef = AccessTools.FieldRefAccess<GasStation, int>("currentFuelPrice");
    private static readonly AccessTools.FieldRef<GasStation, int> MinRef = AccessTools.FieldRefAccess<GasStation, int>("minFuelPrice");
    private static readonly AccessTools.FieldRef<GasStation, int> MaxRef = AccessTools.FieldRefAccess<GasStation, int>("maxFuelPrice");
    private static readonly AccessTools.FieldRef<GasStation, Currency> CurrencyRef = AccessTools.FieldRefAccess<GasStation, Currency>("currency");
    private static readonly AccessTools.FieldRef<GasStation, FuelPump[]> PumpsRef = AccessTools.FieldRefAccess<GasStation, FuelPump[]>("fuelPumps");
    private static readonly AccessTools.FieldRef<FuelPump, float> FilledRef = AccessTools.FieldRefAccess<FuelPump, float>("filledAmount");

    public static string Name(GasStation station)
    {
        string name = NameRef(station);
        return string.IsNullOrEmpty(name) ? null : name;
    }

    public static int Price(GasStation station) => PriceRef(station);

    public static void SetPrice(GasStation station, int price) => PriceRef(station) = price;

    public static int MinPrice(GasStation station) => MinRef(station);

    public static int MaxPrice(GasStation station) => MaxRef(station);

    public static Currency Currency(GasStation station) => CurrencyRef(station);

    public static FuelPump[] Pumps(GasStation station) => PumpsRef(station);

    /// <summary>Litres pumped at the station's pumps since the last payment.</summary>
    public static float Litres(GasStation station)
    {
        var pumps = PumpsRef(station);
        float total = 0f;
        if (pumps == null)
            return total;
        foreach (var pump in pumps)
        {
            if (pump != null)
                total += FilledRef(pump);
        }
        return total;
    }
}
