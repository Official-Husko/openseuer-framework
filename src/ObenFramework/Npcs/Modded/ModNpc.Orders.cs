// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
using System;
using System.Collections.Generic;
using UnityEngine;

namespace ObenFramework.Npcs;

public sealed partial class ModNpc
{
    private const float MinRadius = 0.5f;
    private const float MaxRadius = 50f;
    private const int MinPatrolPoints = 2;
    private const int MaxPatrolPoints = 32;

    /// <summary>What the NPC is doing on orders or its routine step, or Away.</summary>
    public NpcStepKind Order => NpcOrders.Current(this);

    /// <summary>
    /// Walks (or runs) to a point with the game's NPC path finding, then turns to <paramref name="yaw"/>.
    /// <paramref name="done"/> gets true on arrival and false when the walk is replaced, stopped, stuck for
    /// too long, or the NPC dies or leaves. Orders replace each other and override a routine step until they
    /// end. False when the NPC is gone, paused by a claim or frozen.
    /// </summary>
    public bool WalkTo(Vector3 point, float yaw = 0f, bool run = false, Action<bool> done = null)
    {
        Require(point, nameof(point));
        Require(yaw, nameof(yaw));
        return NpcOrders.Begin(this, NpcStepKind.Stand, new[] { point }, yaw, 0f, run, false, true, done);
    }

    /// <summary>Strolls between random reachable points within <paramref name="radius"/> metres (0.5 to 50) of a centre until stopped.</summary>
    public bool Wander(Vector3 center, float radius)
    {
        Require(center, nameof(center));
        if (!(radius >= MinRadius && radius <= MaxRadius))
            throw new ArgumentException("The radius must be 0.5 to 50 metres.", nameof(radius));
        return NpcOrders.Begin(this, NpcStepKind.Wander, new[] { center }, 0f, radius, false, false, false, null);
    }

    /// <summary>Walks 2 to 32 points in order, round and round, until stopped.</summary>
    public bool Patrol(IList<Vector3> points, bool run = false) =>
        NpcOrders.Begin(this, NpcStepKind.Patrol, CopyPoints(points), 0f, 0f, run, false, false, null);

    /// <summary>Sits on the nearest free chair within 1.5 m of <paramref name="seat"/>. False when there is none.</summary>
    public bool Sit(Vector3 seat)
    {
        Require(seat, nameof(seat));
        return NpcOrders.Begin(this, NpcStepKind.Sit, new[] { seat }, 0f, 0f, false, false, false, null);
    }

    /// <summary>Lies down in the nearest free bed within 2 m of <paramref name="bed"/>. False when there is none.</summary>
    public bool Sleep(Vector3 bed)
    {
        Require(bed, nameof(bed));
        return NpcOrders.Begin(this, NpcStepKind.Sleep, new[] { bed }, 0f, 0f, false, false, false, null);
    }

    /// <summary>Stops the current order (or routine step) where the NPC stands. False when there was none.</summary>
    public bool StopOrder()
    {
        if (NpcOrders.Current(this) == NpcStepKind.Away)
            return false;
        NpcOrders.End(this, false);
        return true;
    }

    internal static Vector3[] CopyPoints(IList<Vector3> points)
    {
        if (points == null)
            throw new ArgumentNullException(nameof(points));
        if (points.Count < MinPatrolPoints || points.Count > MaxPatrolPoints)
            throw new ArgumentException("A patrol needs 2 to 32 points.", nameof(points));
        var copy = new Vector3[points.Count];
        for (int i = 0; i < copy.Length; i++)
        {
            Require(points[i], nameof(points));
            copy[i] = points[i];
        }
        return copy;
    }

    internal static void Require(Vector3 value, string name)
    {
        if (!NpcWalkMath.Finite(value.x) || !NpcWalkMath.Finite(value.y) || !NpcWalkMath.Finite(value.z))
            throw new ArgumentException($"{name} must be finite.", name);
    }

    internal static void Require(float value, string name)
    {
        if (!NpcWalkMath.Finite(value))
            throw new ArgumentException($"{name} must be finite.", name);
    }
}
