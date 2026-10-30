// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
using System;
using System.Collections.Generic;
using HarmonyLib;
using ObenFramework.Saves;
using UnityEngine;
using ObenFramework.Patching;

namespace ObenFramework.World;

/// <summary>A landmark the game (or a mod) knows, and whether the player has it on a map.</summary>
public readonly struct LandmarkInfo
{
    public LandmarkInfo(string id, string title, bool owned, bool custom)
    {
        Id = id;
        Title = title;
        Owned = owned;
        Custom = custom;
    }

    /// <summary>The landmark's id; mod landmarks are <c>modId:id</c>.</summary>
    public string Id { get; }

    public string Title { get; }

    /// <summary>It is on one of the player's maps.</summary>
    public bool Owned { get; }

    /// <summary>A mod created it.</summary>
    public bool Custom { get; }
}

/// <summary>Raised by <see cref="Landmarks.Added"/>.</summary>
public readonly struct LandmarkAdded
{
    public LandmarkAdded(string id, string title)
    {
        Id = id;
        Title = title;
    }

    public string Id { get; }
    public string Title { get; }
}

/// <summary>
/// Map landmarks: the places the player discovers and that show on the in-game map. The game's own can
/// be listed, checked and handed out; mods can add their own (see the other half of this class), which
/// are kept in the framework's save file rather than the game's, so a save stays loadable without them.
/// </summary>
public static partial class Landmarks
{
    private static readonly AccessTools.FieldRef<MapController, MapController.MapInfo> CurrentMap =
        AccessTools.FieldRefAccess<MapController, MapController.MapInfo>("currentSceneMapInfo");

    private static readonly Dictionary<string, Landmark> Found = new Dictionary<string, Landmark>();
    private static readonly HashSet<string> Absent = new HashSet<string>();

    internal static readonly OnceLog Log = new OnceLog("Landmarks");

    /// <summary>A landmark was added to the current map (discovered, handed out or placed by a mod).</summary>
    public static readonly WorldEvent<LandmarkAdded> Added = new WorldEvent<LandmarkAdded>(nameof(Added));

    /// <summary>
    /// False when the framework's landmark hooks failed to apply after a game update: <see cref="Added"/> stays
    /// quiet, and mod marks may be saved into the game's own save or not come back.
    /// </summary>
    public static bool IsAvailable => PatchAreas.IsAvailable(PatchAreas.Framework, "World");

    /// <summary>Every landmark loaded in the game right now, the game's and the mods'.</summary>
    public static List<LandmarkInfo> All()
    {
        var list = new List<LandmarkInfo>();
        var seen = new HashSet<string>();
        foreach (var landmark in Resources.FindObjectsOfTypeAll<Landmark>())
        {
            if (landmark != null && landmark.id != null && seen.Add(landmark.id))
                list.Add(new LandmarkInfo(landmark.id, landmark.title, Owns(landmark.id), IsCustom(landmark)));
        }
        return list;
    }

    /// <summary>The player has the landmark on one of their maps.</summary>
    public static bool Has(string id) => Owns(id);

    /// <summary>Hands out one of the game's landmarks at its own position, as the game does when it is discovered.</summary>
    public static bool Unlock(string id)
    {
        var landmark = FindGame(id);
        if (landmark == null || MapController.instance == null || SaveGate.IsBusy)
            return false;
        try
        {
            landmark.GiveLandmark();
            return OnCurrentMap(id);
        }
        catch (Exception e)
        {
            Log.Error("Unlock", e);
            return false;
        }
    }

    /// <summary>Puts one of the game's landmarks on the current map at a position of your choosing.</summary>
    public static bool Place(string id, Vector3 position, bool showNotification = true)
    {
        if (SaveGate.IsBusy || !LandmarkStore.Finite(position.x) || !LandmarkStore.Finite(position.y) || !LandmarkStore.Finite(position.z))
            return false;
        var landmark = FindGame(id);
        var controller = MapController.instance;
        if (landmark == null || controller == null || info_map.instance == null || info_map.instance.mapImage == null)
            return false;
        try { return controller.AddLandMark(landmark, position, showNotification); }
        catch (Exception e)
        {
            Log.Error("Place", e);
            return false;
        }
    }

    internal static void RaiseAdded(Landmark landmark)
    {
        Absent.Remove(landmark.id);
        Added.Raise(new LandmarkAdded(landmark.id, landmark.title));
    }

    internal static void ForgetScene()
    {
        Absent.Clear();
        Markers.Clear();
    }

    private static bool OnCurrentMap(string id)
    {
        var controller = MapController.instance;
        if (controller == null)
            return false;
        var map = CurrentMap(controller);
        return map != null && map.Contains(id);
    }

    private static bool Owns(string id)
    {
        var controller = MapController.instance;
        if (controller == null || string.IsNullOrEmpty(id) || controller.mapInfos == null)
            return false;
        foreach (var map in controller.mapInfos)
        {
            if (map != null && map.Contains(id))
                return true;
        }
        return false;
    }

    // The game's landmarks are assets, loaded with the scenes that use them; a miss is cached per scene.
    private static Landmark FindGame(string id)
    {
        if (string.IsNullOrEmpty(id))
            return null;
        if (Found.TryGetValue(id, out var cached) && cached != null)
            return cached;
        if (Absent.Contains(id))
            return null;
        foreach (var landmark in Resources.FindObjectsOfTypeAll<Landmark>())
        {
            if (landmark == null || landmark.id != id || IsCustom(landmark))
                continue;
            Found[id] = landmark;
            return landmark;
        }
        Absent.Add(id);
        return null;
    }
}
