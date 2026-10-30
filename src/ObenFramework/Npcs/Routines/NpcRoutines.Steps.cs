// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
using System;
using UnityEngine;

namespace ObenFramework.Npcs;

// What a routine does each time it is due: pick the step, then spawn, keep or send away the NPC.
public static partial class NpcRoutines
{
    private static void Evaluate(NpcRoutineEntry entry, int now, string scene)
    {
        int window = NpcRoutineTable.ActiveIndex(entry.Windows, now);
        int index = window < 0 ? -1 : entry.StepOf[window];
        bool changed = index != entry.Index;
        var before = entry.Index == NpcRoutineEntry.Unset ? NpcStepKind.Away : entry.KindOf(entry.Index);
        string previousScene = entry.Scene;
        var step = index < 0 ? null : entry.Steps[index];
        entry.Index = index;
        entry.Scene = scene;
        entry.Current = step;
        entry.Retries = 0;

        var npc = entry.Npc;
        if (changed && npc != null)
            NpcOrders.EndStep(npc);
        if (npc == null)
            entry.Spawned = false;
        if (npc == null || entry.Spawned)
            Act(entry, step, scene, changed && previousScene == scene, npc);

        if (changed && StepChanged.HasSubscribers)
        {
            var live = entry.Npc;
            StepChanged.Raise(new NpcStepChange(entry.OwnerId, entry.Name, before, step != null ? step.Kind : NpcStepKind.Away,
                step?.Scene, live != null ? live.CharacterId : 0));
        }
    }

    private static void Act(NpcRoutineEntry entry, NpcRoutineStep step, string scene, bool walkIn, ModNpc npc)
    {
        switch (NpcRoutineTable.Presence(step?.Scene, scene, npc != null, entry.Spawned))
        {
            case NpcPresence.Spawn:
                Spawn(entry, step, scene, walkIn);
                break;
            case NpcPresence.Keep:
                if (!NpcOrders.HasOrder(npc) && !NpcOrders.HasStep(npc))
                    entry.Retries = StartStep(entry, npc, step) ? 0 : FramesLeft(entry);
                break;
            case NpcPresence.Leave:
                Leave(entry, npc, scene);
                break;
        }
    }

    private static void Spawn(NpcRoutineEntry entry, NpcRoutineStep step, string scene, bool walkIn)
    {
        string problem = BodyProblem(entry.Spec);
        if (problem != null)
        {
            NpcsLog.Warn($"{entry.OwnerId}: the routine for {entry.Name} was dropped: {problem}");
            Entries.Remove(entry);
            return;
        }
        var at = step.Points[0];
        if (walkIn && entry.Doors.TryGetValue(scene, out var door))
            at = door;
        entry.Spec.Position = at;
        entry.Spec.Yaw = step.Yaw;
        entry.Spec.Saved = false;
        if (ModNpcs.Spawn(entry.Owner, entry.Name, entry.Spec) == null)
            return;
        entry.Spawned = true;
        entry.SpawnFrame = Time.frameCount;
        entry.Retries = StartFrames;
    }

    private static void Leave(NpcRoutineEntry entry, ModNpc npc, string scene)
    {
        if (NpcOrders.HasOrder(npc) || NpcOrders.IsTalking(npc.Info))
            return;
        if (!entry.Doors.TryGetValue(scene, out var door))
        {
            Release(entry);
            return;
        }
        if (NpcOrders.HasStep(npc))
            return;
        // One that can't get to the door (no path, or it timed out) leaves where it is; one the mod gave an
        // order of its own meanwhile stays for it.
        NpcOrders.Begin(npc, NpcStepKind.Stand, new[] { door }, 0f, 0f, false, true, false, ok =>
        {
            if (Entries.Contains(entry) && (ok || !NpcOrders.HasOrder(npc)))
                Release(entry);
        });
    }

    private static bool StartStep(NpcRoutineEntry entry, ModNpc npc, NpcRoutineStep step) =>
        NpcOrders.Begin(npc, step.Kind, step.Points, step.Yaw, step.Radius, false, true, false, null);

    // A fresh NPC's controller starts a few frames late; keep trying the step until then.
    private static void Retry(NpcRoutineEntry entry)
    {
        entry.Retries--;
        var npc = entry.Npc;
        if (!entry.Spawned || entry.Current == null || npc == null || NpcOrders.HasOrder(npc) || NpcOrders.HasStep(npc))
            entry.Retries = 0;
        else if (StartStep(entry, npc, entry.Current))
            entry.Retries = 0;
    }

    private static int FramesLeft(NpcRoutineEntry entry) => Math.Max(0, entry.SpawnFrame + StartFrames - Time.frameCount);

    private static void Release(NpcRoutineEntry entry)
    {
        ModNpcs.Despawn(entry.OwnerId, entry.Name);
        entry.Spawned = false;
        entry.Retries = 0;
    }
}
