// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
using System.Collections.Generic;
using UnityEngine;

namespace ObenFramework.Transport;

/// <summary>A snapshot of one drone.</summary>
public readonly struct DroneInfo
{
    public DroneInfo(Drone drone, string id, bool active, float maxSpeed, string target, string next)
    {
        Drone = drone;
        Id = id;
        Active = active;
        MaxSpeed = maxSpeed;
        TargetNode = target;
        NextNode = next;
    }

    public Drone Drone { get; }
    public string Id { get; }
    public bool Active { get; }
    public Vector3 Position => Drone != null ? Drone.transform.position : default;
    public float MaxSpeed { get; }

    /// <summary>The air node it flies to, or null.</summary>
    public string TargetNode { get; }

    /// <summary>The node after that, or null at the end of its route.</summary>
    public string NextNode { get; }
}

/// <summary>A drone was switched on or off, or passed one of its route's air nodes.</summary>
public readonly struct DroneEvent
{
    public DroneEvent(Drone drone, Air_Node node, bool active, bool routeEnd)
    {
        Drone = drone;
        Node = node;
        Active = active;
        RouteEnd = routeEnd;
    }

    public Drone Drone { get; }

    /// <summary>The node passed, or null for an on/off change.</summary>
    public Air_Node Node { get; }

    public bool Active { get; }

    /// <summary>The node passed was the last of the route.</summary>
    public bool RouteEnd { get; }

    public string Id => Drones.IdOf(Drone);
}

/// <summary>A snapshot of one air node, a waypoint of the drones' routes.</summary>
public readonly struct AirNodeInfo
{
    public AirNodeInfo(Air_Node node, string id, Vector3 position, float range, float maxSpeed, string next, List<string> links)
    {
        Node = node;
        Id = id;
        Position = position;
        Range = range;
        MaxSpeed = maxSpeed;
        Next = next;
        Links = links;
    }

    public Air_Node Node { get; }

    public string Id { get; }

    public Vector3 Position { get; }

    /// <summary>How close a drone has to come for the node to count as passed.</summary>
    public float Range { get; }

    /// <summary>The speed drones fly on towards it, or 0 or less when they keep their own.</summary>
    public float MaxSpeed { get; }

    /// <summary>The node drones go on to, or null at the end of a route.</summary>
    public string Next { get; }

    /// <summary>Every node it links on to; <see cref="Next"/> is the one in use.</summary>
    public List<string> Links { get; }
}
