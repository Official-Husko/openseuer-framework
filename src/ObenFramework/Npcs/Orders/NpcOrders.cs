// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
using System;
using System.Collections.Generic;
using ObenFramework.Saves;
using NPC;
using Pathfinding;
using UnityEngine;

namespace ObenFramework.Npcs;

/// <summary>
/// Runs mod NPC orders: walks through the game's own NPC path finding (<c>SetTargetPosition</c>, so doors
/// and waypoints work), wandering, patrols, and sitting or sleeping through the game's own activities. One
/// order per NPC; a new one replaces the old. Orders wait while the NPC is talked to, paused by a claim or
/// frozen, and end when the NPC dies, leaves or the level changes. Ticked once per frame by the framework;
/// with no orders it does nothing.
/// </summary>
internal static partial class NpcOrders
{
    private const float ArriveDistance = 0.6f;
    private const float OriginNudge = 0.05f;
    private const float WanderLeash = 1.5f;
    private const int WanderAttempts = 5;

    private static readonly List<NpcOrder> Orders = new List<NpcOrder>();
    private static readonly System.Random Random = new System.Random();

    /// <summary>
    /// Starts an order. An NPC spawned this frame has no running controller yet; its order waits for it and
    /// starts within a few frames (a sit or sleep that then finds no seat ends with false).
    /// </summary>
    public static bool Begin(ModNpc npc, NpcStepKind kind, Vector3[] points, float yaw, float radius, bool run, bool routine, bool once, Action<bool> done)
    {
        if (npc == null || !npc.Alive || SaveGate.IsBusy || AstarPath.active == null)
            return false;
        var controller = npc.Info.npcController;
        if (controller == null || controller.richAI == null)
        {
            npc.WarnCannotMove();
            return false;
        }
        if (NpcControl.Holds(npc.Info, NpcHolds.Freeze))
            return false;
        bool ready = Ready(controller);
        if (ready && controller.activityDisabled)
            return false;

        var previous = Find(npc);
        if (previous != null)
            Finish(previous, false);

        var order = new NpcOrder
        {
            Npc = npc, Kind = kind, Points = points, Yaw = yaw, Radius = radius, Run = run,
            Routine = routine, Once = once, Done = done, Pending = !ready,
        };
        Orders.Add(order);
        if (order.Pending)
            return true;
        if (Start(order))
            return true;
        Orders.Remove(order);
        order.Done = null;
        return false;
    }

    // Sets the order going on a running controller: the sit or sleep activity, or the first walk.
    private static bool Start(NpcOrder order)
    {
        var controller = order.Npc.Info.npcController;
        if (order.Kind == NpcStepKind.Sit || order.Kind == NpcStepKind.Sleep)
        {
            var point = order.Points[0];
            order.Activity = order.Kind == NpcStepKind.Sit ? NpcSeats.SitNear(point) : NpcSeats.SleepNear(point);
            if (order.Activity == null)
            {
                if (!order.Routine)
                    return false;
                // A routine keeps going: the NPC stands at the step's point instead.
                order.Kind = NpcStepKind.Stand;
            }
        }
        try
        {
            if (controller.Data.state.CurrentActivity != null)
                controller.ForceStopActivity();
            if (order.Activity != null)
                controller.ForceStartActivity(order.Activity);
            else if (order.Kind != NpcStepKind.Wander)
                StartWalk(order, order.Points[0]);
            return true;
        }
        catch (Exception e)
        {
            NpcsLog.Failed("Starting an NPC order", e);
            return false;
        }
    }

    private static bool Ready(NPCController controller) => controller.Data != null && controller.Data.state != null;

    /// <summary>The kind of order the NPC is on, or Away.</summary>
    public static NpcStepKind Current(ModNpc npc)
    {
        var order = Find(npc);
        return order != null ? order.Kind : NpcStepKind.Away;
    }

    /// <summary>A mod's own order (not a routine step) is running.</summary>
    public static bool HasOrder(ModNpc npc)
    {
        var order = Find(npc);
        return order != null && !order.Routine;
    }

    /// <summary>A routine step is running.</summary>
    public static bool HasStep(ModNpc npc)
    {
        var order = Find(npc);
        return order != null && order.Routine;
    }

    /// <summary>Ends whatever order the NPC is on; its callback gets <paramref name="success"/>.</summary>
    public static void End(ModNpc npc, bool success)
    {
        var order = Find(npc);
        if (order != null)
            Finish(order, success);
    }

    /// <summary>Ends the NPC's routine step, leaving a mod's own order alone.</summary>
    public static void EndStep(ModNpc npc)
    {
        var order = Find(npc);
        if (order != null && order.Routine)
            Finish(order, false);
    }

    public static bool IsTalking(NPCInfo npc)
    {
        var dialogue = DialogueController.instance;
        return npc != null && npc.interactableTalk != null && dialogue != null && dialogue.currentInteractableTalk == npc.interactableTalk;
    }

    public static void Tick()
    {
        if (Orders.Count == 0 || SaveGate.IsBusy)
            return;
        for (int i = Orders.Count - 1; i >= 0; i--)
        {
            if (i >= Orders.Count)
                continue;
            var order = Orders[i];
            try
            {
                Advance(order);
            }
            catch (Exception e)
            {
                NpcsLog.Failed("Running an NPC order", e);
                Finish(order, false);
            }
        }
    }

    public static void Clear()
    {
        var all = Orders.ToArray();
        Orders.Clear();
        foreach (var order in all)
        {
            DropPose(order);
            Report(order, false);
        }
    }

    private static void Advance(NpcOrder order)
    {
        var npc = order.Npc;
        var controller = npc.Loaded ? npc.Info.npcController : null;
        if (!npc.Alive || controller == null || !npc.Root.activeInHierarchy)
        {
            Finish(order, false);
            return;
        }
        if (order.Pending)
        {
            if (!Ready(controller))
                return;
            order.Pending = false;
            if (controller.activityDisabled || !Start(order))
                Finish(order, false);
            return;
        }
        if (!Ready(controller))
        {
            Finish(order, false);
            return;
        }
        if (controller.activityDisabled || NpcControl.Holds(npc.Info, NpcHolds.Freeze) || IsTalking(npc.Info))
            return;

        var current = controller.Data.state.CurrentActivity;
        if (order.Activity != null)
        {
            if (current != order.Activity || !order.Activity.running)
                Finish(order, false);
            else
                KeepPose(order);
            return;
        }
        if (current != null && current.running)
            return;

        if (order.Walking)
        {
            if (order.Arrived)
            {
                Arrive(order);
                return;
            }
            order.Timer -= Time.deltaTime;
            if (order.Timer <= 0f)
                TimeOut(order);
            return;
        }
        if (order.Kind != NpcStepKind.Wander)
            return;
        order.Timer -= Time.deltaTime;
        if (order.Timer > 0f)
            return;
        if (PickWanderPoint(order, out var next))
            StartWalk(order, next);
        else
            order.Timer = NpcWalkMath.WaitSeconds(Random);
    }

    private static void Finish(NpcOrder order, bool success)
    {
        Orders.Remove(order);
        DropPose(order);
        Stop(order);
        Report(order, success);
    }

    // Stops what the order started: its activity, or the walk where the NPC stands.
    private static void Stop(NpcOrder order)
    {
        var controller = order.Npc.Loaded ? order.Npc.Info.npcController : null;
        if (controller == null || controller.Data == null || controller.Data.state == null)
            return;
        try
        {
            if (order.Activity != null)
            {
                if (controller.Data.state.CurrentActivity == order.Activity)
                    controller.ForceStopActivity();
                return;
            }
            if (controller.richAI == null)
                return;
            SetSpeed(controller, false);
            controller.SetTargetPosition(controller.transform.position, ArriveDistance);
        }
        catch (Exception e)
        {
            NpcsLog.Failed("Stopping an NPC order", e);
        }
    }

    private static void Report(NpcOrder order, bool success)
    {
        var done = order.Done;
        order.Done = null;
        if (done == null)
            return;
        try { done(success); }
        catch (Exception e) { NpcsLog.Failed("An NPC order callback", e); }
    }

    private static NpcOrder Find(ModNpc npc)
    {
        for (int i = 0; i < Orders.Count; i++)
        {
            if (Orders[i].Npc == npc)
                return Orders[i];
        }
        return null;
    }
}
