// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
using System;
using System.Collections.Generic;
using HarmonyLib;
using ObenFramework.Events;
using ObenFramework.Game;
using ObenFramework.Patching;
using ObenFramework.Saves;
using ObenLoader;
using ObenLoader.Runtime;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ObenFramework.Transport;

/// <summary>
/// Entrypoints (the named spots players arrive at when they enter a level), teleporting to them, the fast
/// travel map and the minitrain. Mods can add their own entrypoints to any scene, so
/// <see cref="Levels.ChangeTo"/> can send the player to a spot of the mod's choosing.
/// </summary>
public static class Travel
{
    private static readonly List<Spot> Spots = new List<Spot>();

    /// <summary>True when the entrypoint hook is in place, so mod entrypoints exist before an arriving player is moved.</summary>
    public static bool IsAvailable => PatchAreas.IsAvailable(PatchAreas.Framework, "Transport");

    /// <summary>The names of the entrypoints of the current level, the game's and the mods'.</summary>
    public static List<string> Entrypoints()
    {
        var names = new List<string>();
        var points = PlayerLevelEntrypoints.instance;
        if (points == null || points.Entrypoints == null)
            return names;
        foreach (var entry in points.Entrypoints)
        {
            if (entry != null && !string.IsNullOrEmpty(entry.Name))
                names.Add(entry.Name);
        }
        return names;
    }

    /// <summary>
    /// Adds an entrypoint to a scene for as long as <paramref name="owner"/> is loaded: whenever the scene loads
    /// (and now, if it is the current one) the spot exists, so the player can be sent there with
    /// <c>Levels.ChangeTo(scene, name)</c> or <see cref="TeleportTo"/>. A name the scene already uses is refused.
    /// Dispose the result or call <see cref="RemoveEntrypoint"/> to take it back sooner.
    /// </summary>
    public static IDisposable AddEntrypoint(ITracker owner, string scene, string name, Vector3 position, float yaw = 0f)
    {
        if (owner == null)
            throw new ArgumentNullException(nameof(owner));
        if (string.IsNullOrEmpty(scene))
            throw new ArgumentException("No scene given.", nameof(scene));
        if (string.IsNullOrEmpty(name) || name == "NONE")
            throw new ArgumentException("An entrypoint needs a name other than NONE.", nameof(name));
        if (Spots.Exists(s => s.Scene == scene && s.Name == name))
            throw new ArgumentException($"A mod already added the entrypoint '{name}' to {scene}.", nameof(name));

        var spot = new Spot { Owner = owner, Scene = scene, Name = name, Position = position, Yaw = yaw };
        Spots.Add(spot);
        Apply(spot);
        spot.Registration = Disposable.Create(() =>
        {
            Spots.Remove(spot);
            Withdraw(spot);
        });
        owner.Track(spot.Registration);
        return spot.Registration;
    }

    /// <summary>
    /// Takes back an entrypoint <paramref name="owner"/> added to <paramref name="scene"/>, like disposing what
    /// <see cref="AddEntrypoint"/> returned. False when it added none by that name there; another mod's stay.
    /// </summary>
    public static bool RemoveEntrypoint(ITracker owner, string scene, string name)
    {
        if (owner == null)
            throw new ArgumentNullException(nameof(owner));
        var spot = Spots.Find(s => ReferenceEquals(s.Owner, owner) && s.Scene == scene && s.Name == name);
        if (spot == null)
            return false;
        spot.Registration.Dispose();
        return true;
    }

    /// <summary>Moves the player to an entrypoint of the current level the way arriving there does. False when there is none.</summary>
    public static bool TeleportTo(string name)
    {
        if (string.IsNullOrEmpty(name) || !WorldState.IsReady || SaveGate.IsBusy || PlayerLocator.instance == null)
            return false;
        var points = PlayerLevelEntrypoints.instance;
        var entry = points != null ? points.GetEntrypoint(name) : null;
        if (entry == null || entry.Location == null)
            return false;
        entry.TeleportPlayer();
        return true;
    }

    /// <summary>Opens the game's fast travel map. False when it is already open or the level has none.</summary>
    public static bool OpenFastTravelMenu()
    {
        var ui = GameUIController.instance;
        var fastTravel = FastTravelController.instance;
        if (ui == null || fastTravel == null || (ui.IsMenuVisible && ui.currentPanel == MenuPanels.FastTravel))
            return false;
        fastTravel.ToggleMenu();
        return true;
    }

    /// <summary>The player is driving a minitrain.</summary>
    public static bool UsingMinitrain => TrainController.instance != null && TrainController.instance.UsingMiniTrain();

    internal static void Init(ObenMod framework)
    {
        GameEvents.SceneLoaded.Subscribe(framework, _ => ApplyAll());
        framework.Track(Disposable.Create(() =>
        {
            foreach (var spot in Spots)
                Withdraw(spot);
            Spots.Clear();
        }));
    }

    // Also called right before the game moves an arriving player, in case the scene event came too late.
    internal static void ApplyAll()
    {
        foreach (var spot in Spots)
            Apply(spot);
    }

    private static void Apply(Spot spot)
    {
        var points = PlayerLevelEntrypoints.instance;
        if (points == null || points.Entrypoints == null || WorldState.SceneName != spot.Scene)
            return;
        if (spot.Entry != null && points.Entrypoints.Contains(spot.Entry) && spot.Marker != null)
            return;

        if (points.GetEntrypoint(spot.Name) != null)
        {
            TransportModule.Log.Warn("entrypoint " + spot.Scene + "/" + spot.Name, "the scene already has an entrypoint with this name; the mod's one is not added");
            return;
        }
        spot.Marker = new GameObject("Mod entrypoint " + spot.Name);
        spot.Marker.transform.SetPositionAndRotation(spot.Position, Quaternion.Euler(0f, spot.Yaw, 0f));
        spot.Entry = new PlayerLevelEntrypoints.Entrypoint(spot.Name, spot.Marker.transform) { dontUseAsDefaultSpawn = true };
        points.Entrypoints.Add(spot.Entry);
    }

    private static void Withdraw(Spot spot)
    {
        var points = PlayerLevelEntrypoints.instance;
        if (spot.Entry != null && points != null && points.Entrypoints != null)
            points.Entrypoints.Remove(spot.Entry);
        if (spot.Marker != null)
            Object.Destroy(spot.Marker);
        spot.Entry = null;
        spot.Marker = null;
    }

    private sealed class Spot
    {
        public ITracker Owner;
        public IDisposable Registration;
        public string Scene;
        public string Name;
        public Vector3 Position;
        public float Yaw;
        public GameObject Marker;
        public PlayerLevelEntrypoints.Entrypoint Entry;
    }
}

[HarmonyPatch(typeof(SaveController), nameof(SaveController.MovePlayerToEntrypoint))]
internal static class EntrypointMovePatch
{
    [HarmonyPrefix]
    private static void Before()
    {
        try { Travel.ApplyAll(); }
        catch (Exception e) { TransportModule.Log.Error("Entrypoint hook", e); }
    }
}
