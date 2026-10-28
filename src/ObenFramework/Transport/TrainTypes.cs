using System.Collections.Generic;
using UnityEngine;

namespace ObenFramework.Transport;

/// <summary>A snapshot of one rail vehicle (a locomotive or a wagon).</summary>
public readonly struct TrainInfo
{
    public TrainInfo(TrackTrain train, float speed, float targetSpeed, bool engineOn, bool braking, string frontCoupledTo,
        string backCoupledTo, string wagonType, string cargoId, bool minitrain)
    {
        Train = train;
        Speed = speed;
        TargetSpeed = targetSpeed;
        EngineOn = engineOn;
        Braking = braking;
        FrontCoupledTo = frontCoupledTo;
        BackCoupledTo = backCoupledTo;
        WagonType = wagonType;
        CargoId = cargoId;
        IsMinitrain = minitrain;
    }

    public TrackTrain Train { get; }

    /// <summary>The game's GUID of the vehicle, its id everywhere.</summary>
    public string Id => Train != null ? Train.GUID : null;

    public float Speed { get; }

    public float TargetSpeed { get; }

    public bool EngineOn { get; }

    public bool Braking { get; }

    public Vector3 Position => Train != null ? Train.transform.position : default;

    /// <summary>The vehicle coupled at the front, or null.</summary>
    public string FrontCoupledTo { get; }

    public string BackCoupledTo { get; }

    /// <summary>None for a locomotive, else Orewagon, Flatbed, Tanker or Passanger (the game's spelling).</summary>
    public string WagonType { get; }

    /// <summary>The id of the wagon's storage, for <c>Containers</c>, or null.</summary>
    public string CargoId { get; }

    /// <summary>A player-driven minitrain cab.</summary>
    public bool IsMinitrain { get; }
}

/// <summary>The minitrain cab: fuel and whether it runs.</summary>
public readonly struct MinitrainInfo
{
    public MinitrainInfo(Minitrain cab, float fuel, float capacity, bool running, bool playerDriving, float speed)
    {
        Cab = cab;
        Fuel = fuel;
        Capacity = capacity;
        Running = running;
        PlayerDriving = playerDriving;
        Speed = speed;
    }

    public Minitrain Cab { get; }
    public float Fuel { get; }
    public float Capacity { get; }
    public bool Running { get; }
    public bool PlayerDriving { get; }
    public float Speed { get; }
}

/// <summary>One stretch of track and where it branches.</summary>
public readonly struct TrackSegmentInfo
{
    public TrackSegmentInfo(string id, bool occupied, string occupiedBy, int activeBranch, string previous, List<string> next)
    {
        Id = id;
        Occupied = occupied;
        OccupiedBy = occupiedBy;
        ActiveBranch = activeBranch;
        Previous = previous;
        Next = next;
    }

    public string Id { get; }
    public bool Occupied { get; }

    /// <summary>The vehicle on it, or null.</summary>
    public string OccupiedBy { get; }

    public int ActiveBranch { get; }
    public string Previous { get; }

    /// <summary>The segment each branch leads to (null for a dead end).</summary>
    public List<string> Next { get; }
}

/// <summary>What a train did.</summary>
public enum TrainEventKind
{
    /// <summary>It started moving.</summary>
    Departed,

    /// <summary>It came to a stop.</summary>
    Stopped,

    /// <summary>It hit something (another train or a buffer) hard enough to stop.</summary>
    Crashed,
}

public readonly struct TrainEvent
{
    public TrainEvent(TrackTrain train, TrainEventKind kind, float speed, string otherId)
    {
        Train = train;
        Kind = kind;
        Speed = speed;
        OtherId = otherId;
    }

    public TrackTrain Train { get; }
    public TrainEventKind Kind { get; }

    /// <summary>Its speed when it departed, or before it stopped or crashed.</summary>
    public float Speed { get; }

    /// <summary>For a crash: the other vehicle, or null.</summary>
    public string OtherId { get; }

    public string Id => Train != null ? Train.GUID : null;
}

/// <summary>Which end of a vehicle.</summary>
public enum TrainEnd
{
    Front,
    Back,
}
