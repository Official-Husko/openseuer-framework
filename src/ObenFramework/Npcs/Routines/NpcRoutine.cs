// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
using System;
using System.Collections.Generic;
using UnityEngine;

namespace ObenFramework.Npcs;

/// <summary>
/// A weekly timetable for a mod NPC (see <see cref="NpcRoutines.Set"/>): steps that say where it is and what
/// it does on which days between which times. A step whose end is before its start runs past midnight
/// (<c>from 22:00 to 02:00</c>). Where steps overlap, the one added first wins; at times no step covers the
/// NPC is away and not in any scene. Build it with the chained methods:
/// <code>
/// var routine = new NpcRoutine()
///     .Stand(NpcDays.Weekdays, TimeSpan.FromHours(8), TimeSpan.FromHours(16), "Interior Bar", counter, yaw: 90)
///     .Wander(NpcDays.Weekend, TimeSpan.FromHours(12), TimeSpan.FromHours(18), "Exterior Market", square, 6f)
///     .Door("Interior Bar", barDoor);
/// </code>
/// </summary>
public sealed class NpcRoutine
{
    private const int MaxSteps = 64;
    private const float MinRadius = 0.5f;
    private const float MaxRadius = 50f;

    private readonly List<NpcRoutineStep> _steps = new List<NpcRoutineStep>();
    private readonly Dictionary<string, Vector3> _doors = new Dictionary<string, Vector3>();

    /// <summary>How many steps it has.</summary>
    public int Count => _steps.Count;

    /// <summary>Stands at a point facing <paramref name="yaw"/> degrees.</summary>
    public NpcRoutine Stand(NpcDays days, TimeSpan from, TimeSpan to, string scene, Vector3 point, float yaw = 0f)
    {
        ModNpc.Require(point, nameof(point));
        ModNpc.Require(yaw, nameof(yaw));
        return Add(NpcStepKind.Stand, days, from, to, scene, new[] { point }, yaw, 0f);
    }

    /// <summary>Strolls around a centre within <paramref name="radius"/> metres (0.5 to 50).</summary>
    public NpcRoutine Wander(NpcDays days, TimeSpan from, TimeSpan to, string scene, Vector3 center, float radius)
    {
        ModNpc.Require(center, nameof(center));
        if (!(radius >= MinRadius && radius <= MaxRadius))
            throw new ArgumentException("The radius must be 0.5 to 50 metres.", nameof(radius));
        return Add(NpcStepKind.Wander, days, from, to, scene, new[] { center }, 0f, radius);
    }

    /// <summary>Walks 2 to 32 points round and round.</summary>
    public NpcRoutine Patrol(NpcDays days, TimeSpan from, TimeSpan to, string scene, IList<Vector3> points) =>
        Add(NpcStepKind.Patrol, days, from, to, scene, ModNpc.CopyPoints(points), 0f, 0f);

    /// <summary>Sits on the chair nearest to <paramref name="seat"/> (stands there when there is none).</summary>
    public NpcRoutine Sit(NpcDays days, TimeSpan from, TimeSpan to, string scene, Vector3 seat)
    {
        ModNpc.Require(seat, nameof(seat));
        return Add(NpcStepKind.Sit, days, from, to, scene, new[] { seat }, 0f, 0f);
    }

    /// <summary>Sleeps in the bed nearest to <paramref name="bed"/> (stands there when there is none).</summary>
    public NpcRoutine Sleep(NpcDays days, TimeSpan from, TimeSpan to, string scene, Vector3 bed)
    {
        ModNpc.Require(bed, nameof(bed));
        return Add(NpcStepKind.Sleep, days, from, to, scene, new[] { bed }, 0f, 0f);
    }

    /// <summary>
    /// Where the NPC comes in and goes out of a scene. When a step starts in the scene you are in, the NPC
    /// appears at the door and walks to its place; when its step moves elsewhere it walks to the door and
    /// disappears there. Without a door it simply appears and disappears where it is.
    /// </summary>
    public NpcRoutine Door(string scene, Vector3 point)
    {
        RequireScene(scene);
        ModNpc.Require(point, nameof(point));
        _doors[scene] = point;
        return this;
    }

    internal NpcRoutineStep[] Steps() => _steps.ToArray();

    internal Dictionary<string, Vector3> Doors() => new Dictionary<string, Vector3>(_doors);

    private NpcRoutine Add(NpcStepKind kind, NpcDays days, TimeSpan from, TimeSpan to, string scene, Vector3[] points, float yaw, float radius)
    {
        RequireScene(scene);
        if (from.Ticks < 0 || from.Ticks >= TimeSpan.TicksPerDay)
            throw new ArgumentException("from must be 00:00 to 23:59.", nameof(from));
        if (to.Ticks <= 0 || to.Ticks > TimeSpan.TicksPerDay)
            throw new ArgumentException("to must be 00:01 to 24:00.", nameof(to));
        int fromMinute = (int)(from.Ticks / TimeSpan.TicksPerMinute);
        int toMinute = (int)(to.Ticks / TimeSpan.TicksPerMinute);
        string problem = NpcRoutineTable.Check((int)days, fromMinute, toMinute);
        if (problem != null)
            throw new ArgumentException($"A routine step's {problem}.", nameof(days));
        if (_steps.Count >= MaxSteps)
            throw new InvalidOperationException("A routine holds at most 64 steps.");

        _steps.Add(new NpcRoutineStep
        {
            Kind = kind,
            Scene = scene,
            Windows = NpcRoutineTable.Windows((int)days, fromMinute, toMinute),
            Points = points,
            Yaw = yaw,
            Radius = radius,
        });
        return this;
    }

    private static void RequireScene(string scene)
    {
        if (string.IsNullOrEmpty(scene))
            throw new ArgumentException("A scene name is needed.", nameof(scene));
    }
}

internal sealed class NpcRoutineStep
{
    public NpcStepKind Kind;
    public string Scene;
    public NpcWindow[] Windows;
    public Vector3[] Points;
    public float Yaw;
    public float Radius;
}
