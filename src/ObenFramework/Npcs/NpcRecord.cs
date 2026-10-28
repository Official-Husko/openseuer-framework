using NPC;
using UnityEngine;

namespace ObenFramework.Npcs;

/// <summary>
/// A copy of what is known about one NPC at the moment it was read. An NPC is known from the game's
/// schedule database (<c>NPCData</c>, which also covers NPCs that are not in the scene you are in) and,
/// when it is in the scene, from its game object. Nothing here updates; read again for fresh values.
/// </summary>
public sealed class NpcRecord
{
    /// <summary>The character id the game uses everywhere (Lua, mail, the console).</summary>
    public int Id { get; }

    /// <summary>First name, for example "Pentti".</summary>
    public string Name { get; }

    /// <summary>First and last name, for example "Pentti Penttilä".</summary>
    public string FullName { get; }

    /// <summary>The character's job, or an empty string.</summary>
    public string Job { get; }

    /// <summary>
    /// The scene the NPC is in. For an NPC in the scene you are in this is that scene; for others it is
    /// where the game's simulation last put them, and empty before the simulation has placed them.
    /// </summary>
    public string Scene { get; }

    /// <summary>
    /// Where the NPC is: its real position when it is in the scene, else the last position the game
    /// stored for it (only meaningful together with <see cref="Scene"/>).
    /// </summary>
    public Vector3 Position { get; }

    /// <summary>The NPC exists as an object in the scene you are in (it may still be switched off).</summary>
    public bool Loaded { get; }

    /// <summary>The NPC is loaded and its object is active.</summary>
    public bool Active { get; }

    /// <summary>The NPC's state: Any, Default, Sleeping or Dead. Empty when it is not loaded.</summary>
    public string State { get; }

    public bool Dead => State == nameof(NPCInfo.State.Dead);

    /// <summary>Name of the activity running right now (standing, patrolling, sleeping...), or null.</summary>
    public string Activity { get; }

    /// <summary>Name of the schedule entry that started it, or null.</summary>
    public string ScheduleEntry { get; }

    /// <summary>The NPC has a timetable or random activities in the schedule database.</summary>
    public bool HasSchedule { get; }

    /// <summary>The game's per-NPC record, or null for an NPC that only exists as an object.</summary>
    public NPCData Data { get; }

    /// <summary>The NPC's object in the scene, or null when it is not loaded. Check for Unity null before use.</summary>
    public NPCInfo Live { get; }

    internal NpcRecord(NPCData data, NPCInfo live)
    {
        Data = data;
        Live = live;
        var character = live != null && live.character != null ? live.character : data?.character;
        Id = character != null ? character.ID : data != null ? data.ID : -1;
        Name = character != null ? character.Name : string.Empty;
        FullName = character != null ? character.FullName : string.Empty;
        Job = character != null && character.Job != null ? character.Job : string.Empty;

        var state = data?.state;
        Loaded = live != null;
        Active = live != null && live.gameObject.activeInHierarchy;
        State = live != null ? live.state.ToString() : string.Empty;
        Position = live != null ? live.transform.position : state != null ? state.currentPosition : Vector3.zero;
        Scene = !string.IsNullOrEmpty(state?.currentScene) ? state.currentScene : live != null ? live.gameObject.scene.name : string.Empty;

        Activity = NpcNames.Of(state?.CurrentActivity);
        var scheduler = data?.scheduler;
        ScheduleEntry = scheduler?.currentScheduledAction?.name;
        HasSchedule = scheduler != null && ((scheduler.Timetable?.Count ?? 0) > 0 || (scheduler.RandomActivities?.Count ?? 0) > 0);
    }

    public override string ToString() => $"{FullName} ({Id})";
}
