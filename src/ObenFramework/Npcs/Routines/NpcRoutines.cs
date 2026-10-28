// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
using System;
using System.Collections.Generic;
using ObenFramework.Clock;
using ObenFramework.Events;
using ObenFramework.Game;
using ObenFramework.Saves;
using ObenFramework.World;
using ObenLoader;
using ObenLoader.Runtime;
using UnityEngine;

namespace ObenFramework.Npcs;

/// <summary>
/// Mod NPCs that live by a weekly routine (<see cref="NpcRoutine"/>). The routine decides where the NPC is:
/// when its current step is in the scene you are in, the NPC is spawned there (walking in from the scene's
/// door when the step starts while you watch) and does the step; when the step moves elsewhere, it walks
/// out and is removed. The routine checks once per game minute and when the scene changes. Routine NPCs are
/// not kept in the save: the routine puts them back. A mod's own orders on the NPC win over its routine step
/// until they end. Routines end when their mod unloads.
/// </summary>
public static partial class NpcRoutines
{
    private const int MaxPerMod = 64;
    private const int StartFrames = 300;

    private static readonly List<NpcRoutineEntry> Entries = new List<NpcRoutineEntry>();
    private static readonly HashSet<string> Tracked = new HashSet<string>();
    private static bool _dirty;
    private static int _lastMinute = -1;
    private static int _lastScene;

    /// <summary>A routine NPC moved to another step. Each handler runs in its own try/catch.</summary>
    public static readonly WorldEvent<NpcStepChange> StepChanged = new WorldEvent<NpcStepChange>(nameof(StepChanged));

    /// <summary>
    /// Gives the mod NPC <paramref name="name"/> a routine, replacing one it had. <paramref name="spec"/>
    /// says who it is (template, names, look); its position and <see cref="ModNpcSpec.Saved"/> are not used,
    /// the routine places it. False when the mod already keeps an NPC of that name in the save, has spawned
    /// one by hand, or runs 64 routines.
    /// </summary>
    public static bool Set(ObenMod owner, string name, ModNpcSpec spec, NpcRoutine routine)
    {
        if (owner == null)
            throw new ArgumentNullException(nameof(owner));
        if (spec == null)
            throw new ArgumentNullException(nameof(spec));
        if (routine == null || routine.Count == 0)
            throw new ArgumentException("The routine needs at least one step.", nameof(routine));
        spec.Validate(name);

        var old = Find(owner.Info.Id, name);
        if (old == null && Count(owner.Info.Id) >= MaxPerMod)
            return false;
        if (old == null && ModNpcs.Get(owner, name) != null)
            return false;

        var copy = new ModNpcSpec
        {
            Template = spec.Template, Body = spec.Body, FirstName = spec.FirstName, LastName = spec.LastName,
            KeepDialogue = spec.KeepDialogue, Look = spec.Look != null ? spec.Look.Clone() : null,
        };
        var entry = new NpcRoutineEntry(owner, name, copy, routine);
        if (old != null)
        {
            Entries.Remove(old);
            entry.Spawned = old.Spawned;
            entry.SpawnFrame = old.SpawnFrame;
        }
        Entries.Add(entry);
        _dirty = true;
        Track(owner);
        return true;
    }

    /// <summary>Ends the NPC's routine; an NPC the routine spawned is removed. False when it had none.</summary>
    public static bool Remove(ObenMod owner, string name)
    {
        var entry = owner != null ? Find(owner.Info.Id, name) : null;
        if (entry == null)
            return false;
        Entries.Remove(entry);
        if (entry.Spawned)
            Release(entry);
        return true;
    }

    /// <summary>Ends all of the mod's routines. Returns how many there were.</summary>
    public static int Clear(ObenMod owner) => owner == null ? 0 : ClearOwner(owner.Info.Id);

    /// <summary>The step the routine is on right now (by the clock, wherever the NPC is), or Away.</summary>
    public static NpcStepKind Current(ObenMod owner, string name)
    {
        var entry = owner != null ? Find(owner.Info.Id, name) : null;
        if (entry == null || !GameClock.IsAvailable)
            return NpcStepKind.Away;
        int window = NpcRoutineTable.ActiveIndex(entry.Windows, WeekMinuteNow());
        return window < 0 ? NpcStepKind.Away : entry.Steps[entry.StepOf[window]].Kind;
    }

    /// <summary>The names of the mod's NPCs that have a routine.</summary>
    public static List<string> Names(ObenMod owner)
    {
        var names = new List<string>();
        foreach (var entry in Entries)
        {
            if (owner != null && entry.OwnerId == owner.Info.Id)
                names.Add(entry.Name);
        }
        return names;
    }

    internal static void Init(ObenMod framework)
    {
        GameEvents.SceneLoaded.Subscribe(framework, _ => Restart());
        GameEvents.Loaded.Subscribe(framework, Restart);
        framework.Track(Disposable.Create(() =>
        {
            Entries.Clear();
            Tracked.Clear();
        }));
    }

    // The NPC went away (despawned, level change, mod unload); the routine spawns it again when due.
    internal static void Forget(ModNpc npc)
    {
        foreach (var entry in Entries)
        {
            if (entry.OwnerId == npc.Owner && entry.Name == npc.Name)
            {
                entry.Spawned = false;
                entry.Retries = 0;
            }
        }
    }

    /// <summary>Called every frame by the framework. Works once per game minute, or a few frames after a spawn.</summary>
    internal static void Tick()
    {
        if (Entries.Count == 0 || !WorldState.IsReady || SaveGate.IsBusy || NPCManager.instance == null || !GameClock.IsAvailable)
            return;
        int now = WeekMinuteNow();
        var active = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        bool due = _dirty || now != _lastMinute || active.handle != _lastScene;
        if (!due && !AnyRetries())
            return;

        _dirty = false;
        _lastMinute = now;
        _lastScene = active.handle;
        for (int i = Entries.Count - 1; i >= 0; i--)
        {
            if (i >= Entries.Count)
                continue;
            var entry = Entries[i];
            try
            {
                if (due)
                    Evaluate(entry, now, active.name);
                else if (entry.Retries > 0)
                    Retry(entry);
            }
            catch (Exception e)
            {
                NpcsLog.Failed("Running an NPC routine", e);
            }
        }
    }

    private static bool AnyRetries()
    {
        for (int i = 0; i < Entries.Count; i++)
        {
            if (Entries[i].Retries > 0)
                return true;
        }
        return false;
    }

    private static void Restart()
    {
        _dirty = true;
        foreach (var entry in Entries)
            entry.Scene = null;
    }

    private static void Track(ObenMod owner)
    {
        if (!Tracked.Add(owner.Info.Id))
            return;
        string id = owner.Info.Id;
        owner.Track(Disposable.Create(() =>
        {
            Tracked.Remove(id);
            ClearOwner(id);
        }));
    }

    private static int ClearOwner(string owner)
    {
        int count = 0;
        for (int i = Entries.Count - 1; i >= 0; i--)
        {
            var entry = Entries[i];
            if (entry.OwnerId != owner)
                continue;
            Entries.RemoveAt(i);
            if (entry.Spawned)
                Release(entry);
            count++;
        }
        return count;
    }

    private static int Count(string owner)
    {
        int count = 0;
        foreach (var entry in Entries)
        {
            if (entry.OwnerId == owner)
                count++;
        }
        return count;
    }

    private static NpcRoutineEntry Find(string owner, string name)
    {
        foreach (var entry in Entries)
        {
            if (entry.OwnerId == owner && entry.Name == name)
                return entry;
        }
        return null;
    }

    private static int WeekMinuteNow() => NpcRoutineTable.WeekMinute(GameClock.WeekdayIndex, GameClock.Hour, GameClock.Minute);
}
