// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
using System;
using System.Collections.Generic;
using HarmonyLib;
using ObenFramework.Items;
using ObenFramework.World;
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

/// <summary>
/// Drones: the flying machines that follow chains of air nodes (deliveries, surveillance). Switch them on and
/// off, send them to a node, change their speed, and hear them pass nodes.
/// </summary>
public static class Drones
{
    private static List<Drone> _known;

    public static readonly WorldEvent<DroneEvent> Changed = new WorldEvent<DroneEvent>(nameof(Changed));

    public static List<Drone> All() => ObjectIds.Active<Drone>();

    public static Drone Find(string id) => ObjectIds.Find<Drone>(id);

    public static string IdOf(Drone drone) => ObjectIds.IdOf(drone);

    /// <summary>The air nodes of the loaded scenes. A scan.</summary>
    public static List<Air_Node> Nodes() => ObjectIds.Active<Air_Node>();

    public static Air_Node FindNode(string id)
    {
        var node = !string.IsNullOrEmpty(id) ? Air_Node.GetNode(id) : null;
        return node != null ? node : ObjectIds.Find<Air_Node>(id);
    }

    public static string IdOf(Air_Node node) => node == null ? null : !string.IsNullOrEmpty(node.GUID) ? node.GUID : ObjectIds.IdOf(node);

    public static DroneInfo Describe(Drone drone)
    {
        if (drone == null)
            throw new ArgumentNullException(nameof(drone));
        var target = drone.targetNode;
        var next = target != null ? target.NextNode : null;
        return new DroneInfo(drone, IdOf(drone), drone.active, drone.maxSpeed, IdOf(target), IdOf(next));
    }

    public static void SetActive(Drone drone, bool active)
    {
        if (drone == null)
            throw new ArgumentNullException(nameof(drone));
        using (WorldChanges.EnterMod())
        {
            if (active)
                drone.TurnOn();
            else
                drone.TurnOff();
        }
    }

    /// <summary>Sends the drone to an air node; it follows the route from there.</summary>
    public static void SetTarget(Drone drone, Air_Node node, bool start = false)
    {
        if (drone == null)
            throw new ArgumentNullException(nameof(drone));
        if (node == null)
            throw new ArgumentNullException(nameof(node));
        using (WorldChanges.EnterMod())
        {
            if (start)
                drone.SetTargetAndStart(node);
            else
                drone.SetTarget(node);
        }
    }

    public static void SetMaxSpeed(Drone drone, float speed)
    {
        if (drone == null)
            throw new ArgumentNullException(nameof(drone));
        if (!(speed > 0f) || float.IsInfinity(speed))
            throw new ArgumentOutOfRangeException(nameof(speed), "The speed must be above 0.");
        drone.SetMaxSpeed(speed);
    }

    // The drone heading for a node that was just passed. Drones are few; the list is made once per scene.
    internal static Drone HeadingFor(Air_Node node)
    {
        if (_known == null)
            _known = All();
        foreach (var drone in _known)
        {
            if (drone != null && drone.targetNode == node)
                return drone;
        }
        return null;
    }

    internal static void Reset() => _known = null;
}

[HarmonyPatch(typeof(Drone))]
internal static class DronePatches
{
    [HarmonyPrefix]
    [HarmonyPatch(nameof(Drone.TurnOn))]
    private static void BeforeOn(Drone __instance, out bool __state) => __state = __instance.active;

    [HarmonyPostfix]
    [HarmonyPatch(nameof(Drone.TurnOn))]
    private static void AfterOn(Drone __instance, bool __state) => Changed(__instance, __state);

    [HarmonyPrefix]
    [HarmonyPatch(nameof(Drone.TurnOff))]
    private static void BeforeOff(Drone __instance, out bool __state) => __state = __instance.active;

    [HarmonyPostfix]
    [HarmonyPatch(nameof(Drone.TurnOff))]
    private static void AfterOff(Drone __instance, bool __state) => Changed(__instance, __state);

    private static void Changed(Drone drone, bool before)
    {
        try
        {
            if (before != drone.active && Drones.Changed.HasSubscribers && WorldChanges.Active)
                Drones.Changed.Raise(new DroneEvent(drone, null, drone.active, false));
        }
        catch (Exception e) { TransportModule.Log.Error("Drone hook", e); }
    }
}

[HarmonyPatch(typeof(Air_Node), nameof(Air_Node.Pass))]
internal static class AirNodePatch
{
    [HarmonyPrefix]
    private static void Before(Air_Node __instance)
    {
        try
        {
            if (!Drones.Changed.HasSubscribers || !WorldChanges.Active)
                return;
            var drone = Drones.HeadingFor(__instance);
            Drones.Changed.Raise(new DroneEvent(drone, __instance, drone != null && drone.active, __instance.NextNode == null));
        }
        catch (Exception e) { TransportModule.Log.Error("Air node hook", e); }
    }
}
