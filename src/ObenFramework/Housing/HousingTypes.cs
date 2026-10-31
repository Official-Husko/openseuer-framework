using ObenFramework.World;

namespace ObenFramework.Housing;

/// <summary>Something a tenant does in the building, as raised by <see cref="Tenement.EventStarted"/>. Values follow <c>TenementEvent.EventType</c>.</summary>
public enum TenantEventKind
{
    None,
    SpawnTrash,
    PlayMusic,
    MakeNoise,
    BreakApartment,
    Die,
    SpawnCrazyTeddies,
    Complain,
}

/// <summary>What happened to a tenancy, as raised by <see cref="Tenement.TenantChanged"/>.</summary>
public enum TenantChangeKind
{
    /// <summary>The tenant signed and is moving in (the move takes some game hours).</summary>
    Rented,

    /// <summary>The move finished; the tenant lives there now and starts paying.</summary>
    MovedIn,

    EvictionStarted,
    EvictionCancelled,

    /// <summary>The tenant is gone (evicted, or replaced by a new tenant).</summary>
    MovedOut,
}

/// <summary>Money as the game counts it: Open Sewer Coin and Real Money.</summary>
public readonly struct RentAmount
{
    public RentAmount(int oc, float rm)
    {
        Oc = oc;
        Rm = rm;
    }

    public int Oc { get; }

    public float Rm { get; }

    public override string ToString() => Oc + " OC, " + Rm + " RM";
}

/// <summary>A tenant: an NPC with a tenancy profile.</summary>
public sealed class TenantInfo
{
    internal TenantInfo(int npcId, string name)
    {
        NpcId = npcId;
        Name = name;
    }

    /// <summary>The NPC's id (the same ids the NPC module uses).</summary>
    public int NpcId { get; }

    public string Name { get; }
}

/// <summary>A snapshot of one tenement apartment.</summary>
public sealed class ApartmentInfo
{
    internal ApartmentInfo(int number, int floor, ApartmentSize size)
    {
        Number = number;
        Floor = floor;
        Size = size;
    }

    /// <summary>The apartment's number; 1 is the player's own.</summary>
    public int Number { get; }

    public int Floor { get; }

    public ApartmentSize Size { get; }

    public bool IsPlayers => Number == 1;

    /// <summary>The tenant living there or moving in, or null.</summary>
    public TenantInfo Tenant { get; internal set; }

    /// <summary>The tenant is still moving in.</summary>
    public bool MovingIn { get; internal set; }

    /// <summary>Game hours until the move finishes.</summary>
    public float MoveInHoursLeft { get; internal set; }

    public bool EvictionPending { get; internal set; }

    /// <summary>Game hours until the eviction finishes.</summary>
    public float EvictionHoursLeft { get; internal set; }

    /// <summary>A former tenant's furniture is still there.</summary>
    public bool FurnitureOnly { get; internal set; }

    /// <summary>The rent it brings in per payment, after the building's modifiers.</summary>
    public RentAmount Rent { get; internal set; }

    /// <summary>The collateral the tenant paid, returned when they leave.</summary>
    public RentAmount Deposit { get; internal set; }

    public bool Locked { get; internal set; }

    /// <summary>GUID of the apartment's furniture manager (see the building module), or null.</summary>
    public string FurnitureManagerGuid { get; internal set; }
}

/// <summary>A tenancy change, as raised by <see cref="Tenement.TenantChanged"/>.</summary>
public readonly struct TenantChange
{
    public TenantChange(int apartment, TenantInfo tenant, TenantChangeKind kind, ChangeSource source)
    {
        Apartment = apartment;
        Tenant = tenant;
        Kind = kind;
        Source = source;
    }

    public int Apartment { get; }

    public TenantInfo Tenant { get; }

    public TenantChangeKind Kind { get; }

    public ChangeSource Source { get; }
}

/// <summary>A tenant event that started, as raised by <see cref="Tenement.EventStarted"/>.</summary>
public readonly struct TenantEvent
{
    public TenantEvent(string id, TenantEventKind kind, int npcId)
        : this(id, kind, npcId, ChangeSource.Game)
    {
    }

    public TenantEvent(string id, TenantEventKind kind, int npcId, ChangeSource source)
    {
        Id = id;
        Kind = kind;
        NpcId = npcId;
        Source = source;
    }

    /// <summary>The event's id in the tenant's event list.</summary>
    public string Id { get; }

    public TenantEventKind Kind { get; }

    public int NpcId { get; }

    /// <summary><see cref="ChangeSource.Mod"/> for <see cref="Tenants.TriggerEvent"/>, otherwise the game.</summary>
    public ChangeSource Source { get; }
}
