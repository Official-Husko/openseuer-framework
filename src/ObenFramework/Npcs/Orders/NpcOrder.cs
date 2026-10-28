using System;
using NPC;
using UnityEngine;

namespace ObenFramework.Npcs;

/// <summary>What a mod NPC is doing on orders or in its routine.</summary>
public enum NpcStepKind
{
    /// <summary>Nothing (or, in a routine, not in any scene right now).</summary>
    Away,

    /// <summary>Walks to a point and stands there facing a direction.</summary>
    Stand,

    /// <summary>Strolls between random points around a centre, pausing at each.</summary>
    Wander,

    /// <summary>Walks a list of points round and round.</summary>
    Patrol,

    /// <summary>Sits on the nearest free chair to a point (within 1.5 m).</summary>
    Sit,

    /// <summary>Lies down in the nearest free bed to a point (within 2 m).</summary>
    Sleep,
}

/// <summary>One running order of one mod NPC (see <see cref="NpcOrders"/>).</summary>
internal sealed class NpcOrder
{
    public ModNpc Npc;
    public NpcStepKind Kind;
    public Vector3[] Points;
    public float Yaw;
    public float Radius;
    public bool Run;

    /// <summary>Started by a routine step rather than by the mod directly.</summary>
    public bool Routine;

    /// <summary>Ends after arriving (a plain walk), instead of staying on the step.</summary>
    public bool Once;

    public Action<bool> Done;

    /// <summary>The game activity a sit or sleep order runs, or null for walking orders.</summary>
    public Activity Activity;

    public Vector3 Target;
    public int Index;
    public bool Walking;
    public bool Arrived;
    public float Timer;

    // Built once per order so starting a walk does not allocate a new delegate each time.
    public Action OnArrive;

    public NpcOrder() => OnArrive = () => Arrived = true;
}
