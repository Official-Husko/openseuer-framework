using System.Collections.Generic;
using ObenLoader;
using UnityEngine;

namespace ObenFramework.Npcs;

/// <summary>One mod NPC's routine as <see cref="NpcRoutines"/> runs it.</summary>
internal sealed class NpcRoutineEntry
{
    public const int Unset = -2;

    public readonly ObenMod Owner;
    public readonly string Name;
    public readonly ModNpcSpec Spec;
    public readonly NpcRoutineStep[] Steps;
    public readonly Dictionary<string, Vector3> Doors;

    // All steps' windows in one list, and for each the step it belongs to.
    public readonly List<NpcWindow> Windows = new List<NpcWindow>();
    public readonly List<int> StepOf = new List<int>();

    /// <summary>The step running now, -1 for none, <see cref="Unset"/> before the first evaluation.</summary>
    public int Index = Unset;

    /// <summary>The scene the routine was last evaluated in.</summary>
    public string Scene;

    public NpcRoutineStep Current;

    /// <summary>The live NPC was put here by the routine (so the routine may take it away again).</summary>
    public bool Spawned;

    public int SpawnFrame;

    /// <summary>Frames left to retry starting the step's order on a freshly spawned NPC (its controller starts late).</summary>
    public int Retries;

    public NpcRoutineEntry(ObenMod owner, string name, ModNpcSpec spec, NpcRoutine routine)
    {
        Owner = owner;
        Name = name;
        Spec = spec;
        Steps = routine.Steps();
        Doors = routine.Doors();
        for (int i = 0; i < Steps.Length; i++)
        {
            foreach (var window in Steps[i].Windows)
            {
                Windows.Add(window);
                StepOf.Add(i);
            }
        }
    }

    public string OwnerId => Owner.Info.Id;

    public ModNpc Npc => ModNpcs.Get(OwnerId, Name);

    public NpcStepKind KindOf(int index) => index < 0 ? NpcStepKind.Away : Steps[index].Kind;
}
