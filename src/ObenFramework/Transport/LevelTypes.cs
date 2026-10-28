using ObenFramework.World;
using UnityEngine;

namespace ObenFramework.Transport;

/// <summary>
/// One way out of the current level: a door that leads to another scene (<c>DoorChangelevel</c>) or a
/// plain trigger or sign (<c>Changelevel</c>).
/// </summary>
public readonly struct LevelExit
{
    public LevelExit(Changelevel component, string id, string title, string scene, string entrypoint, string name,
        bool isDoor, bool locked, Vector3 position, Vector3? spawn, Vector3? stand)
    {
        Component = component;
        Id = id;
        Title = title;
        Scene = scene;
        Entrypoint = entrypoint;
        Name = name;
        IsDoor = isDoor;
        Locked = locked;
        Position = position;
        Spawn = spawn;
        Stand = stand;
    }

    public Changelevel Component { get; }

    /// <summary>The id <see cref="Levels.Find"/> takes: the door's GUID where it has one, else its address.</summary>
    public string Id { get; }

    /// <summary>What the use prompt calls it.</summary>
    public string Title { get; }

    /// <summary>The scene it leads to.</summary>
    public string Scene { get; }

    /// <summary>The entrypoint in <see cref="Scene"/> the player arrives at.</summary>
    public string Entrypoint { get; }

    /// <summary>This side's own entrypoint, where players coming the other way arrive.</summary>
    public string Name { get; }

    /// <summary>A door with a lock (<c>DoorChangelevel</c>), rather than a trigger.</summary>
    public bool IsDoor { get; }

    public bool Locked { get; }

    public Vector3 Position { get; }

    /// <summary>Where players coming from <see cref="Scene"/> appear, if the level set a spot.</summary>
    public Vector3? Spawn { get; }

    /// <summary>Where characters stand to use the door (the NPC waypoint in front of it), if there is one.</summary>
    public Vector3? Stand { get; }
}

/// <summary>A trip from one level to another, as <see cref="Levels.Leaving"/> and <see cref="Levels.Arrived"/> report it.</summary>
public readonly struct LevelTrip
{
    public LevelTrip(string from, string to, string entrypoint, string exitId, ChangeSource source)
    {
        From = from;
        To = to;
        Entrypoint = entrypoint;
        ExitId = exitId;
        Source = source;
    }

    /// <summary>The scene the player left.</summary>
    public string From { get; }

    /// <summary>The scene the player goes to.</summary>
    public string To { get; }

    /// <summary>The entrypoint asked for, or "NONE" for the level's default spawn.</summary>
    public string Entrypoint { get; }

    /// <summary>The <see cref="LevelExit.Id"/> of the door or trigger used, or null when it was code, fast travel or the console.</summary>
    public string ExitId { get; }

    /// <summary>The player used an exit, a mod asked for the trip, or the game sent the player (a story event, fast travel).</summary>
    public ChangeSource Source { get; }

    public override string ToString() => From + " -> " + To + (Entrypoint != null && Entrypoint != "NONE" ? " (" + Entrypoint + ")" : "");
}
