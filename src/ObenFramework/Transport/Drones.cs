// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
using System;
using System.Collections.Generic;
using HarmonyLib;
using ObenFramework.Items;
using ObenFramework.Patching;
using ObenFramework.Saves;
using ObenFramework.World;
using UnityEngine;

namespace ObenFramework.Transport;

/// <summary>
/// Drones: the flying machines that follow chains of air nodes (deliveries, surveillance). Switch them on and
/// off, send them to a node, change their speed, and hear them pass nodes. Changes are refused (false) while a
/// save is written or loaded.
/// </summary>
public static class Drones
{
    public static readonly WorldEvent<DroneEvent> Changed = new WorldEvent<DroneEvent>(nameof(Changed));

    /// <summary>True when the drone hooks are in place, so <see cref="Changed"/> is raised.</summary>
    public static bool IsAvailable => PatchAreas.IsAvailable(PatchAreas.Framework, "Transport");

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

    public static AirNodeInfo DescribeNode(Air_Node node)
    {
        if (node == null)
            throw new ArgumentNullException(nameof(node));
        var links = new List<string>();
        foreach (var next in node.nextNodes)
            links.Add(IdOf(next));
        return new AirNodeInfo(node, IdOf(node), node.transform.position, node.range, node.maxSpeed, IdOf(node.NextNode), links);
    }

    public static DroneInfo Describe(Drone drone)
    {
        if (drone == null)
            throw new ArgumentNullException(nameof(drone));
        var target = drone.targetNode;
        var next = target != null ? target.NextNode : null;
        return new DroneInfo(drone, IdOf(drone), drone.active, drone.maxSpeed, IdOf(target), IdOf(next));
    }

    public static bool SetActive(Drone drone, bool active)
    {
        if (drone == null)
            throw new ArgumentNullException(nameof(drone));
        if (SaveGate.IsBusy)
            return false;
        using (WorldChanges.EnterMod())
        {
            if (active)
                drone.TurnOn();
            else
                drone.TurnOff();
        }
        return drone.active == active;
    }

    /// <summary>Sends the drone to an air node; it follows the route from there.</summary>
    public static bool SetTarget(Drone drone, Air_Node node, bool start = false)
    {
        if (drone == null)
            throw new ArgumentNullException(nameof(drone));
        if (node == null)
            throw new ArgumentNullException(nameof(node));
        if (SaveGate.IsBusy)
            return false;
        using (WorldChanges.EnterMod())
        {
            if (start)
                drone.SetTargetAndStart(node);
            else
                drone.SetTarget(node);
        }
        return drone.targetNode == node;
    }

    public static bool SetMaxSpeed(Drone drone, float speed)
    {
        if (drone == null)
            throw new ArgumentNullException(nameof(drone));
        if (!(speed > 0f) || float.IsInfinity(speed))
            throw new ArgumentOutOfRangeException(nameof(speed), "The speed must be above 0.");
        if (SaveGate.IsBusy)
            return false;
        drone.SetMaxSpeed(speed);
        return true;
    }

    // The drone passing a node: only a flying drone heading for it and within its range calls Pass, so the
    // nearest such drone is the one (a scan, but passes are seconds apart).
    internal static Drone Passing(Air_Node node)
    {
        Drone best = null;
        float bestDistance = float.MaxValue;
        var at = node.transform.position;
        foreach (var drone in All())
        {
            if (drone == null || !drone.active || drone.targetNode != node)
                continue;
            float distance = Vector3.Distance(drone.transform.position, at);
            if (distance <= node.range && distance < bestDistance)
            {
                best = drone;
                bestDistance = distance;
            }
        }
        return best;
    }
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
            var drone = Drones.Passing(__instance);
            Drones.Changed.Raise(new DroneEvent(drone, __instance, drone != null && drone.active, __instance.NextNode == null));
        }
        catch (Exception e) { TransportModule.Log.Error("Air node hook", e); }
    }
}
