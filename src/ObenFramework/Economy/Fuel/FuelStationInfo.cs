using ObenFramework.World.Addressing;
using Currency = Money.Currency;

namespace ObenFramework.Economy;

/// <summary>A snapshot of one fuel station (the game's <c>GasStation</c>, where minitrains are filled up).</summary>
public sealed class FuelStationInfo
{
    internal FuelStationInfo(GasStation station)
    {
        Station = station;
        Id = ObjectAddress.Of(station).ToString();
        Guid = string.IsNullOrEmpty(station.GUID) ? null : station.GUID;
        Name = FuelFields.Name(station) ?? station.name;
        Scene = station.gameObject.scene.name;
        Price = FuelFields.Price(station);
        MinPrice = FuelFields.MinPrice(station);
        MaxPrice = FuelFields.MaxPrice(station);
        Currency = FuelFields.Currency(station);
        Pumps = FuelFields.Pumps(station)?.Length ?? 0;
        Litres = FuelFields.Litres(station);
        ToPayCoins = station.priceToPayOc;
        ToPayRealMoney = station.priceToPayRm;
    }

    /// <summary>The game component. Unity-null once the scene is gone.</summary>
    public GasStation Station { get; }

    /// <summary>The station's address; <see cref="FuelStations.Find"/> takes it back.</summary>
    public string Id { get; }

    public string Guid { get; }

    /// <summary>The name printed on receipts.</summary>
    public string Name { get; }

    public string Scene { get; }

    /// <summary>Price per litre now, in <see cref="Currency"/>.</summary>
    public int Price { get; }

    /// <summary>The range the game picks a new price from now and then.</summary>
    public int MinPrice { get; }

    public int MaxPrice { get; }

    public Currency Currency { get; }

    public int Pumps { get; }

    /// <summary>Litres pumped since the last payment.</summary>
    public float Litres { get; }

    /// <summary>What is owed for fuel pumped and not yet paid, in coins.</summary>
    public int ToPayCoins { get; }

    /// <summary>What is owed in Real Money at a Real Money station.</summary>
    public int ToPayRealMoney { get; }

    public override string ToString() => $"{Name}: {Price} {Currency}/l";
}

/// <summary>A paid tank of fuel, raised by <see cref="FuelStations.FuelBought"/>.</summary>
public readonly struct FuelPurchase
{
    internal FuelPurchase(FuelStationInfo station, float litres, int cost, Currency currency)
    {
        Station = station;
        Litres = litres;
        Cost = cost;
        Currency = currency;
    }

    public FuelStationInfo Station { get; }

    public float Litres { get; }

    public int Cost { get; }

    public Currency Currency { get; }
}
