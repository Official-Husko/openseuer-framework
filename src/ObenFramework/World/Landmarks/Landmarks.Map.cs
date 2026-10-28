// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
using System;
using System.Collections.Generic;
using HarmonyLib;
using ObenFramework.Saves;
using ObenLoader.Mods;
using UnityEngine;

namespace ObenFramework.World;

public static partial class Landmarks
{
    private const int DotSize = 20;

    private static readonly AccessTools.FieldRef<MapController, Map> ControllerMap =
        AccessTools.FieldRefAccess<MapController, Map>("map");
    private static readonly AccessTools.FieldRef<Map, List<GameObject>> MapMarkers =
        AccessTools.FieldRefAccess<Map, List<GameObject>>("landmarks");

    // The marker object the map made for each mod landmark, so removing one can hide it at once.
    private static readonly Dictionary<string, GameObject> Markers = new Dictionary<string, GameObject>();

    private static Sprite _dot;

    /// <summary>True while a mod landmark is being put back, so <see cref="Added"/> isn't raised for it.</summary>
    internal static bool Restoring { get; private set; }

    private static bool AddToMap(Landmark landmark, Vector3 position, bool showNotification, bool silent)
    {
        var controller = MapController.instance;
        var map = ControllerMap(controller);
        var list = map != null ? MapMarkers(map) : null;
        var known = list != null ? new HashSet<GameObject>(list) : null;
        bool added;
        Restoring = silent;
        try { added = controller.AddLandMark(landmark, position, showNotification); }
        finally { Restoring = false; }

        if (added && list != null)
        {
            foreach (var marker in list)
            {
                if (known.Contains(marker))
                    continue;
                Markers[landmark.id] = marker;
                break;
            }
        }
        return added;
    }

    private static void HideMarker(string full)
    {
        if (!Markers.TryGetValue(full, out var marker))
            return;
        Markers.Remove(full);
        if (marker == null)
            return;
        var controller = MapController.instance;
        var map = controller != null ? ControllerMap(controller) : null;
        var ui = marker.GetComponentInChildren<LandmarkUI>(true);
        if (ui != null && map != null && ui.ID >= 0 && ui.ID < map.selectedLandmarks.Count)
            map.selectedLandmarks[ui.ID] = false;
        marker.SetActive(false);
    }

    private static bool MapReady()
    {
        var controller = MapController.instance;
        if (SaveGate.IsBusy || controller == null || controller.loadingMap || info_map.instance == null || info_map.instance.mapImage == null)
            return false;
        return ControllerMap(controller) != null && CurrentMapId().Length > 0;
    }

    // The same key MapController.GetCurrentMapId uses for its per-map lists.
    private static string CurrentMapId()
    {
        var info = info_map.instance;
        if (info == null)
            return "";
        if (info.requiredTaskItem != null)
            return info.requiredTaskItem.Id;
        return info.mapImage != null ? info.mapImage.name : "";
    }

    /// <summary>Puts back the saved landmarks of loaded mods on the current map. Called once the map has loaded.</summary>
    internal static void Restore(Func<string, bool> present = null)
    {
        if (_save == null || Entries.Count == 0 || !MapReady())
            return;
        present ??= ModRegistry.IsLoaded;
        string map = CurrentMapId();
        foreach (var entry in Entries.ToArray())
        {
            if (!LandmarkStore.ShouldRestore(entry, map, present))
                continue;
            string full = LandmarkStore.FullId(entry.Mod, entry.Id);
            if (OnCurrentMap(full))
                continue;
            try
            {
                var landmark = Resolve(entry, full);
                if (landmark != null)
                    AddToMap(landmark, new Vector3(entry.X, entry.Y, entry.Z), false, true);
            }
            catch (Exception e)
            {
                Log.Error("restoring " + full, e);
            }
        }
    }

    // An existing definition, or one rebuilt from the saved icon. Runtime sprites can't be rebuilt.
    private static Landmark Resolve(LandmarkEntry entry, string full)
    {
        if (Custom.TryGetValue(full, out var landmark) && landmark != null)
            return landmark;
        Sprite sprite = null;
        if (entry.Icon == LandmarkStore.DotIcon)
        {
            sprite = DotSprite();
        }
        else if (!string.IsNullOrEmpty(entry.Icon))
        {
            var owner = ModRegistry.Find(entry.Mod)?.Instance;
            if (owner != null)
                sprite = LoadIcon(owner, entry.Icon);
        }
        return sprite != null ? Define(full, entry.Title, entry.Priority, sprite, entry.Width, entry.Height) : null;
    }

    /// <summary>
    /// Takes mod landmarks out of the game's map lists for the length of a save, so the game's save never
    /// holds a landmark that only exists while a mod is loaded. <see cref="Reattach"/> puts them back.
    /// </summary>
    internal static List<DetachedLandmark> Detach()
    {
        var controller = MapController.instance;
        if (controller == null || Custom.Count == 0 || controller.mapInfos == null)
            return null;
        var detached = new List<DetachedLandmark>();
        foreach (var map in controller.mapInfos)
        {
            var list = map?.landmarkInfos;
            if (list == null)
                continue;
            for (int i = list.Count - 1; i >= 0; i--)
            {
                var info = list[i];
                if (info == null || !IsCustom(info.landmark))
                    continue;
                list.RemoveAt(i);
                detached.Add(new DetachedLandmark(list, info, i));
            }
        }
        return detached;
    }

    internal static void Reattach(List<DetachedLandmark> detached)
    {
        if (detached == null)
            return;
        // Put back in reverse order of removal so every saved index is right again.
        for (int i = detached.Count - 1; i >= 0; i--)
        {
            var item = detached[i];
            item.List.Insert(Math.Min(item.Index, item.List.Count), item.Info);
        }
    }

    private static Sprite DotSprite()
    {
        if (_dot != null)
            return _dot;
        const int size = 32;
        var fill = new Color32(220, 40, 40, 255);
        var edge = new Color32(255, 255, 255, 255);
        var clear = new Color32(0, 0, 0, 0);
        var pixels = new Color32[size * size];
        float center = (size - 1) / 2f;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float distance = Mathf.Sqrt((x - center) * (x - center) + (y - center) * (y - center));
                pixels[y * size + x] = distance <= 11f ? fill : distance <= 15f ? edge : clear;
            }
        }
        var texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, hideFlags = HideFlags.HideAndDontSave };
        texture.SetPixels32(pixels);
        texture.Apply();
        _dot = Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
        _dot.hideFlags = HideFlags.HideAndDontSave;
        return _dot;
    }

    private static void DestroyDot()
    {
        if (_dot == null)
            return;
        var texture = _dot.texture;
        UnityEngine.Object.Destroy(_dot);
        if (texture != null)
            UnityEngine.Object.Destroy(texture);
        _dot = null;
    }
}

/// <summary>A mod landmark taken out of a map list for a save, and where it was.</summary>
internal sealed class DetachedLandmark
{
    public DetachedLandmark(List<MapController.LandmarkInfo> list, MapController.LandmarkInfo info, int index)
    {
        List = list;
        Info = info;
        Index = index;
    }

    public List<MapController.LandmarkInfo> List { get; }
    public MapController.LandmarkInfo Info { get; }
    public int Index { get; }
}
