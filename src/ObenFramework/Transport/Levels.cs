// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
using System;
using System.Collections.Generic;
using ObenFramework.Game;
using ObenFramework.Items;
using ObenFramework.Patching;
using ObenFramework.Saves;
using ObenFramework.World;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ObenFramework.Transport;

/// <summary>
/// The ways between levels: list the exits of the current level (doors to other scenes and level triggers),
/// lock and unlock them, use one, or send the player to any scene and entrypoint. <see cref="Leaving"/> and
/// <see cref="Arrived"/> report every trip, whichever way it was started.
/// </summary>
public static class Levels
{
    private static List<string> _scenes;
    private static LevelTrip? _pending;

    /// <summary>
    /// The player is about to leave for another scene: raised as the game starts the change, before it saves.
    /// Covers exits, fast travel, the console and <see cref="ChangeTo"/>.
    /// </summary>
    public static readonly WorldEvent<LevelTrip> Leaving = new WorldEvent<LevelTrip>(nameof(Leaving));

    /// <summary>The trip announced by <see cref="Leaving"/> ended and the new level is ready (after GameEvents.WorldReady).</summary>
    public static readonly WorldEvent<LevelTrip> Arrived = new WorldEvent<LevelTrip>(nameof(Arrived));

    /// <summary>True when the level change hooks are in place, so <see cref="Leaving"/> and <see cref="Arrived"/> are raised.</summary>
    public static bool IsAvailable => PatchAreas.IsAvailable(PatchAreas.Framework, "Transport");

    /// <summary>Every scene in the game's build, in build order ("Main Menu" first).</summary>
    public static IReadOnlyList<string> Scenes
    {
        get
        {
            if (_scenes == null)
            {
                var names = new List<string>(SceneManager.sceneCountInBuildSettings);
                for (int i = 0; i < SceneManager.sceneCountInBuildSettings; i++)
                    names.Add(System.IO.Path.GetFileNameWithoutExtension(SceneUtility.GetScenePathByBuildIndex(i)));
                _scenes = names;
            }
            return _scenes;
        }
    }

    /// <summary>True when the game can load a scene by this name.</summary>
    public static bool Exists(string scene) => !string.IsNullOrEmpty(scene) && SaveController.CheckIfLevelExists(scene);

    /// <summary>The exits of the loaded scenes, active ones only. A scan, so not for per-frame use.</summary>
    public static List<Changelevel> All() => ObjectIds.Active<Changelevel>();

    /// <summary>Every exit described, with where characters stand to use the doors.</summary>
    public static List<LevelExit> List()
    {
        var stands = StandPositions();
        var list = new List<LevelExit>();
        foreach (var exit in All())
            list.Add(Describe(exit, stands));
        return list;
    }

    /// <summary>Finds an exit by the id <see cref="IdOf"/> gives, an address, a path or a GUID. Null when there is none.</summary>
    public static Changelevel Find(string id) => ObjectIds.Find<Changelevel>(id);

    public static string IdOf(Changelevel exit) => ObjectIds.IdOf(exit);

    public static LevelExit Describe(Changelevel exit) => Describe(exit, StandPositions());

    /// <summary>
    /// Can the player be sent somewhere now? False while a save loads or saves, during another level change,
    /// while waiting or sleeping, and outside a playable level.
    /// </summary>
    public static bool CanTravel(out string reason)
    {
        reason = null;
        if (!WorldState.InWorld)
            reason = "not in a level";
        else if (SaveGate.IsBusy || SaveController.Loading)
            reason = "the game is saving or loading";
        else if (Changelevel.changeLevelDelayActive)
            reason = "another level change is under way";
        else if (WaitingController.instance != null && WaitingController.instance.waitingType != WaitingController.Type.None)
            reason = "the player is waiting or sleeping";
        else if (SaveController.instance == null)
            reason = "the save controller is missing";
        return reason == null;
    }

    /// <summary>Locks or unlocks a door to another level. False for triggers, which have no lock, and while a save is written or loaded.</summary>
    public static bool SetLocked(Changelevel exit, bool locked)
    {
        if (exit == null)
            throw new ArgumentNullException(nameof(exit));
        if (!(exit is DoorChangelevel door))
            return false;
        if (door._Lock.Locked == locked)
            return true;
        if (SaveGate.IsBusy)
            return false;
        using (WorldChanges.EnterMod())
        {
            if (locked)
                door.Lock();
            else
                door.UnLock();
        }
        return door._Lock.Locked == locked;
    }

    /// <summary>
    /// Uses an exit as if the player had: a door plays its animation and sound and the level changes after its
    /// delay. A locked door opens with the player's key as when they use it; without one it stays shut, unless
    /// <paramref name="ignoreLock"/>. Returns whether the trip started: false when it can't start (see
    /// <see cref="CanTravel"/>), the target scene is missing, or the door stays shut (also while a key that takes
    /// a moment unlocks it; the door is unlocked then, so use it again).
    /// </summary>
    public static bool Use(Changelevel exit, bool ignoreLock = false)
    {
        if (exit == null)
            throw new ArgumentNullException(nameof(exit));
        if (!CanTravel(out _) || !Exists(exit.OtherLevel))
            return false;

        using (WorldChanges.EnterMod())
        {
            if (exit is DoorChangelevel door)
            {
                if (!door._Lock.Locked || ignoreLock)
                    door.OpenDoor();
                else if (door._Lock.keyItem != null && door._Lock.PlayerHasKey())
                    door.UseDoor();
                else
                    return false;
            }
            else
            {
                exit.ChangeLevel();
            }
        }
        // A door with a delay holds the change for its animation; the rest start it at once.
        return Changelevel.changeLevelDelayActive || SaveController.Loading;
    }

    /// <summary>
    /// Sends the player to a scene, arriving at <paramref name="entrypoint"/> (or the level's default spawn when
    /// null). The game saves first, like any level change. False when the trip can't start or the scene is missing.
    /// </summary>
    public static bool ChangeTo(string scene, string entrypoint = null)
    {
        if (!CanTravel(out _) || !Exists(scene))
            return false;
        using (WorldChanges.EnterMod())
            SaveController.ChangeLevel(scene, string.IsNullOrEmpty(entrypoint) ? "NONE" : entrypoint);
        return SaveController.Loading;
    }

    // Called from the patch on SaveController.ChangeLevel, before the game saves and leaves.
    internal static void OnLeaving(string scene, string entrypoint)
    {
        var exit = LevelWatch.TakeExit(out var source);
        var trip = new LevelTrip(WorldState.SceneName, scene, entrypoint, exit, exit != null ? source : WorldChanges.Current);
        _pending = trip;
        if (WorldState.IsReady)
            Leaving.Raise(trip);
    }

    internal static void OnWorldReady(string scene)
    {
        var trip = _pending;
        _pending = null;
        if (trip.HasValue && string.Equals(trip.Value.To, scene, StringComparison.Ordinal))
            Arrived.Raise(trip.Value);
    }

    internal static void Reset()
    {
        _pending = null;
        LevelWatch.Reset();
    }

    private static LevelExit Describe(Changelevel exit, Dictionary<DoorChangelevel, Vector3> stands)
    {
        var door = exit as DoorChangelevel;
        var entry = exit.ThisEntrypoint;
        Vector3? spawn = entry != null && entry.Location != null ? entry.Location.position : (Vector3?)null;
        Vector3? stand = door != null && stands.TryGetValue(door, out var at) ? at : (Vector3?)null;
        return new LevelExit(exit, IdOf(exit), exit.title, exit.OtherLevel, exit.OtherEntrypoint, entry != null ? entry.Name : null,
            door != null, door != null && door._Lock.Locked, exit.transform.position, spawn, stand);
    }

    private static Dictionary<DoorChangelevel, Vector3> StandPositions()
    {
        var stands = new Dictionary<DoorChangelevel, Vector3>();
        foreach (var waypoint in ObjectIds.Active<NPC.Waypoint_LevelChange>())
        {
            if (waypoint.LinkedDoor != null && !stands.ContainsKey(waypoint.LinkedDoor))
                stands.Add(waypoint.LinkedDoor, waypoint.transform.position);
        }
        return stands;
    }
}
