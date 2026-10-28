// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
using System;
using System.Collections.Generic;
using HarmonyLib;
using ObenFramework.Events;
using ObenFramework.Saves;
using ObenFramework.World;
using ObenFramework.World.Addressing;
using ObenLoader;
using UnityEngine;

namespace ObenFramework.Npcs;

/// <summary>A security camera in the scene, copied when read.</summary>
public sealed class CameraRecord
{
    /// <summary>Stable address of the camera (see <see cref="ObjectAddress"/>), what <see cref="Surveillance.SetCameraOn"/> takes.</summary>
    public string Address { get; }

    public Vector3 Position { get; }

    /// <summary>The camera is switched on and watching.</summary>
    public bool On { get; }

    /// <summary>The player is in its view right now.</summary>
    public bool SeesPlayer { get; }

    /// <summary>How far it sees, in metres.</summary>
    public float Range { get; }

    /// <summary>Its field of view, in degrees.</summary>
    public float FieldOfView { get; }

    internal CameraRecord(SurveilanceCamera camera, bool seesPlayer)
    {
        Address = ObjectAddress.Of(camera).ToString();
        Position = camera.transform.position;
        On = camera.active;
        SeesPlayer = seesPlayer;
        Range = camera.range;
        FieldOfView = camera.FOV;
    }
}

/// <summary>Who saw the player (see <see cref="Surveillance.PlayerSpotted"/>).</summary>
public sealed class Sighting
{
    /// <summary>A security camera saw the player (false: an NPC did).</summary>
    public bool ByCamera { get; }

    /// <summary>Address of the witness object.</summary>
    public string Address { get; }

    /// <summary>The NPC that saw the player, or 0 for a camera.</summary>
    public int NpcId { get; }

    internal Sighting(bool byCamera, string address, int npcId)
    {
        ByCamera = byCamera;
        Address = address;
        NpcId = npcId;
    }
}

/// <summary>
/// Being watched: security cameras, NPC witnesses and areas where the player trespasses. Read whether the
/// player is seen or trespassing, switch cameras, and hear when someone spots the player committing a crime
/// or the player walks into or out of a no-go area.
/// </summary>
public static class Surveillance
{
    private static readonly AccessTools.FieldRef<TrespassingArea, bool> Trespassing = AccessTools.FieldRefAccess<TrespassingArea, bool>("trespassing");
    private static readonly AccessTools.FieldRef<SurveilanceCamera, bool> PlayerInView = AccessTools.FieldRefAccess<SurveilanceCamera, bool>("playerInView");

    private static readonly HashSet<TrespassingArea> Inside = new HashSet<TrespassingArea>();
    private static readonly HashSet<Witness> SpottedThisFrame = new HashSet<Witness>();
    private static int _spottedFrame = -1;

    /// <summary>A camera or NPC saw the player commit a crime (it will be reported). Each handler runs in its own try/catch.</summary>
    public static readonly WorldEvent<Sighting> PlayerSpotted = new WorldEvent<Sighting>(nameof(PlayerSpotted));

    /// <summary>The player walked into a no-go area. The payload is the area's address.</summary>
    public static readonly WorldEvent<string> TrespassStarted = new WorldEvent<string>(nameof(TrespassStarted));

    /// <summary>The player left a no-go area (or the level changed while inside).</summary>
    public static readonly WorldEvent<string> TrespassEnded = new WorldEvent<string>(nameof(TrespassEnded));

    /// <summary>The player is inside a no-go area right now.</summary>
    public static bool IsTrespassing
    {
        get
        {
            foreach (var area in UnityEngine.Object.FindObjectsByType<TrespassingArea>(FindObjectsSortMode.None))
            {
                if (area != null && Trespassing(area))
                    return true;
            }
            return false;
        }
    }

    /// <summary>
    /// Some witness (an NPC or a camera) sees the player right now. With <paramref name="crime"/>, witnesses
    /// that ignore crimes do not count. Nothing is reported to the police.
    /// </summary>
    public static bool IsPlayerSeen(bool crime = true) =>
        PlayerLocator.instance != null && Witness.IsWitnessed(invoke: false, crime ? WitnessType.Crime : WitnessType.Default);

    /// <summary>The security cameras in the scene, switched off ones included.</summary>
    public static List<CameraRecord> Cameras()
    {
        var result = new List<CameraRecord>();
        foreach (var camera in AllCameras())
            result.Add(new CameraRecord(camera, PlayerInView(camera)));
        return result;
    }

    /// <summary>
    /// Switches a camera on or off by its address (see <see cref="CameraRecord.Address"/>), with its lights and
    /// relays, as its own switch does. False when there is no such camera or a save or load runs.
    /// </summary>
    public static bool SetCameraOn(string address, bool on)
    {
        if (string.IsNullOrEmpty(address) || SaveGate.IsBusy)
            return false;
        foreach (var camera in AllCameras())
        {
            if (camera.GetComponent<Witness>() == null || ObjectAddress.Of(camera).ToString() != address)
                continue;
            if (camera.active == on)
                return true;
            try
            {
                if (on)
                    camera.TurnOn();
                else
                    camera.TurnOff();
                return true;
            }
            catch (Exception e)
            {
                NpcsLog.Failed("Surveillance.SetCameraOn", e);
                return false;
            }
        }
        return false;
    }

    internal static void Init(ObenMod framework) => GameEvents.SceneLoaded.Subscribe(framework, _ => LeaveAll());

    // A witness reported the player. One event per witness per frame: the game asks the same witness repeatedly.
    internal static void Spot(Witness witness)
    {
        if (Time.frameCount != _spottedFrame)
        {
            _spottedFrame = Time.frameCount;
            SpottedThisFrame.Clear();
        }
        if (!SpottedThisFrame.Add(witness))
            return;
        bool camera = witness.GetComponent<SurveilanceCamera>() != null;
        var npc = camera ? null : witness.GetComponentInParent<NPCController>();
        PlayerSpotted.Raise(new Sighting(camera, ObjectAddress.Of(witness).ToString(), npc != null ? npc.ID : 0));
    }

    internal static void Stay(TrespassingArea area)
    {
        if (Trespassing(area) && Inside.Add(area))
            TrespassStarted.Raise(ObjectAddress.Of(area).ToString());
    }

    internal static void Exit(TrespassingArea area)
    {
        if (Inside.Remove(area))
            TrespassEnded.Raise(ObjectAddress.Of(area).ToString());
    }

    internal static bool WatchesTrespass => TrespassStarted.HasSubscribers || TrespassEnded.HasSubscribers;

    private static void LeaveAll()
    {
        // The areas are gone with the old level; their addresses can no longer be read.
        if (Inside.Count > 0 && TrespassEnded.HasSubscribers)
            TrespassEnded.Raise(string.Empty);
        Inside.Clear();
        SpottedThisFrame.Clear();
    }

    private static SurveilanceCamera[] AllCameras() =>
        Game.WorldState.InWorld
            ? UnityEngine.Object.FindObjectsByType<SurveilanceCamera>(FindObjectsInactive.Include, FindObjectsSortMode.None)
            : Array.Empty<SurveilanceCamera>();
}
