// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
using ObenFramework.World;

namespace ObenFramework.Env;

/// <summary>A snapshot of one liquid container.</summary>
public sealed class TankInfo
{
    internal TankInfo(string id, string name, int liquidItemId, float amount, float capacity, bool locked, bool isSupply)
    {
        Id = id;
        Name = name;
        LiquidItemId = liquidItemId;
        Amount = amount;
        Capacity = capacity;
        Locked = locked;
        IsSupply = isSupply;
    }

    public string Id { get; }

    public string Name { get; }

    /// <summary>The liquid's item id, or 0 when empty.</summary>
    public int LiquidItemId { get; }

    /// <summary>Litres in it.</summary>
    public float Amount { get; }

    /// <summary>Litres it holds; negative for taps and sinks fed from elsewhere, which never run dry by themselves.</summary>
    public float Capacity { get; }

    public bool Locked { get; }

    /// <summary>It is the scene's water supply (see <see cref="WorldWater.Supply"/>).</summary>
    public bool IsSupply { get; }

    public bool IsEndless => Capacity < 0f;
}

/// <summary>
/// What a container held before and after a change, as raised by <see cref="Tanks.TankChanged"/>. A pour or a
/// running shower moves the level in many small steps; they come as one change once the level settles.
/// </summary>
public readonly struct TankChange
{
    public TankChange(LiquidStorage tank, int previousItemId, float previousAmount, int liquidItemId, float amount, ChangeSource source)
    {
        Tank = tank;
        PreviousItemId = previousItemId;
        PreviousAmount = previousAmount;
        LiquidItemId = liquidItemId;
        Amount = amount;
        Source = source;
    }

    public LiquidStorage Tank { get; }

    /// <summary>The liquid before the change, or 0 when it was empty.</summary>
    public int PreviousItemId { get; }

    public float PreviousAmount { get; }

    /// <summary>The liquid now, or 0 when empty.</summary>
    public int LiquidItemId { get; }

    /// <summary>Litres in it now.</summary>
    public float Amount { get; }

    /// <summary>Who started the change: <see cref="ChangeSource.LocalPlayer"/> for a pour from the tank's window.</summary>
    public ChangeSource Source { get; }

    public string Id => Tanks.IdOf(Tank);
}
