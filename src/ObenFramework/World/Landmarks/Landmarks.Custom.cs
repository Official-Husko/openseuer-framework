// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
using System;
using System.Collections.Generic;
using ObenFramework.Assets;
using ObenFramework.Saves;
using ObenLoader;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ObenFramework.World;

public static partial class Landmarks
{
    // Mod landmark definitions by full id (modId:id), alive while their mod is loaded.
    private static readonly Dictionary<string, Landmark> Custom = new Dictionary<string, Landmark>();
    private static ModSaveData<LandmarkFile> _save;

    private static List<LandmarkEntry> Entries => _save.Value.Entries;

    /// <summary>
    /// Adds a mod landmark to the map now shown, with an icon sprite the mod made. The position is kept per
    /// map in the save; calling again for the same id on the same map keeps the saved position and updates
    /// the title and icon. A runtime sprite can't be saved, so the mod has to call this again in later
    /// sessions for the landmark to show; use the overload with a file path to have it come back by itself.
    /// </summary>
    public static bool Create(ObenMod owner, string id, string title, Vector3 position, Sprite icon,
        int priority = 0, int width = 0, int height = 0, bool showNotification = true)
    {
        if (icon == null)
            throw new ArgumentNullException(nameof(icon));
        return CreateCore(owner, id, title, position, icon, "", priority, width, height, showNotification);
    }

    /// <summary>
    /// Like <see cref="Create(ObenMod,string,string,Vector3,Sprite,int,int,int,bool)"/> with a PNG or JPG
    /// from the mod's folder as the icon. The path is saved, so the landmark returns by itself while the
    /// mod is installed.
    /// </summary>
    public static bool Create(ObenMod owner, string id, string title, Vector3 position, string iconPath,
        int priority = 0, int width = 0, int height = 0, bool showNotification = true)
    {
        if (owner == null) throw new ArgumentNullException(nameof(owner));
        string icon = (iconPath ?? "").Replace('\\', '/');
        if (string.IsNullOrEmpty(icon) || icon == LandmarkStore.DotIcon || !LandmarkStore.IconAllowed(icon))
            throw new ArgumentException("iconPath must be a relative path inside the mod's folder", nameof(iconPath));
        var sprite = LoadIcon(owner, icon);
        return sprite != null && CreateCore(owner, id, title, position, sprite, icon, priority, width, height, showNotification);
    }

    /// <summary>Adds a plain red dot to the map, without the "Discovered" notification unless asked for.</summary>
    public static bool Mark(ObenMod owner, string id, string title, Vector3 position, bool showNotification = false) =>
        CreateCore(owner, id, title, position, DotSprite(), LandmarkStore.DotIcon, 0, DotSize, DotSize, showNotification);

    /// <summary>Removes one of the mod's landmarks from every map and from the save.</summary>
    public static bool Remove(ObenMod owner, string id)
    {
        if (owner == null) throw new ArgumentNullException(nameof(owner));
        if (!WritesAllowed("Remove") || !LandmarkStore.IdAllowed(id))
            return false;
        return RemoveOne(owner.Info.Id, id);
    }

    /// <summary>Removes all of the mod's landmarks. Returns whether there were any.</summary>
    public static bool Clear(ObenMod owner)
    {
        if (owner == null) throw new ArgumentNullException(nameof(owner));
        if (!WritesAllowed("Clear"))
            return false;
        var ids = new HashSet<string>();
        foreach (var entry in Entries)
        {
            if (entry.Mod == owner.Info.Id)
                ids.Add(entry.Id);
        }
        string prefix = owner.Info.Id + ":";
        foreach (string key in Custom.Keys)
        {
            if (key.StartsWith(prefix, StringComparison.Ordinal))
                ids.Add(key.Substring(prefix.Length));
        }
        bool any = false;
        foreach (string id in ids)
            any |= RemoveOne(owner.Info.Id, id);
        return any;
    }

    /// <summary>The mod landmarks recorded in this save, on every map.</summary>
    public static List<LandmarkEntry> Saved(string modId = null)
    {
        var list = new List<LandmarkEntry>();
        if (_save == null)
            return list;
        foreach (var entry in Entries)
        {
            if (modId == null || entry.Mod == modId)
                list.Add(entry);
        }
        return list;
    }

    private static bool CreateCore(ObenMod owner, string id, string title, Vector3 position, Sprite sprite, string icon,
        int priority, int width, int height, bool showNotification)
    {
        if (owner == null) throw new ArgumentNullException(nameof(owner));
        if (!LandmarkStore.IdAllowed(id))
            throw new ArgumentException("id must be 1 to 64 letters, digits, '.', '_' or '-'", nameof(id));
        if (!LandmarkStore.Finite(position.x) || !LandmarkStore.Finite(position.y) || !LandmarkStore.Finite(position.z) || width < 0 || height < 0)
            throw new ArgumentException("position must be finite and the size not negative");
        if (!WritesAllowed("Create"))
            return false;
        string map = CurrentMapId();
        if (map.Length == 0 || MapController.instance == null)
        {
            Log.Warn("Create no map", "this scene has no map to put a landmark on");
            return false;
        }

        string mod = owner.Info.Id;
        string full = LandmarkStore.FullId(mod, id);
        var entry = LandmarkStore.Find(Entries, mod, id, map);
        bool fresh = entry == null;
        if (fresh)
        {
            if (!LandmarkStore.HasRoom(Entries, mod))
            {
                Log.Warn("Create cap " + mod, $"a mod keeps at most {LandmarkStore.MaxPerMod} landmarks");
                return false;
            }
            entry = new LandmarkEntry { Mod = mod, Id = id, Map = map, X = position.x, Y = position.y, Z = position.z };
            Entries.Add(entry);
        }
        entry.Title = Trim(title);
        entry.Icon = icon;
        entry.Priority = priority;
        entry.Width = width;
        entry.Height = height;

        try
        {
            var landmark = Define(full, entry.Title, priority, sprite, width, height);
            // Right after a load the map fills its lists a moment later; the restore that runs then adds it.
            if (!MapReady())
                return true;
            if (AddToMap(landmark, new Vector3(entry.X, entry.Y, entry.Z), showNotification, false) || OnCurrentMap(full))
                return true;
        }
        catch (Exception e)
        {
            Log.Error("Create", e);
        }
        if (fresh)
            Entries.Remove(entry);
        return false;
    }

    private static Landmark Define(string full, string title, int priority, Sprite sprite, int width, int height)
    {
        if (!Custom.TryGetValue(full, out var landmark) || landmark == null)
        {
            landmark = ScriptableObject.CreateInstance<Landmark>();
            landmark.hideFlags = HideFlags.HideAndDontSave;
            landmark.name = full;
            landmark.id = full;
            Custom[full] = landmark;
        }
        landmark.title = title ?? "";
        landmark.priority = priority;
        landmark.image = sprite;
        landmark.width = width;
        landmark.height = height;
        return landmark;
    }

    private static bool RemoveOne(string mod, string id)
    {
        bool any = Entries.RemoveAll(e => e.Mod == mod && e.Id == id) > 0;
        return Withdraw(LandmarkStore.FullId(mod, id)) || any;
    }

    // Takes a mod landmark off every map and destroys its definition; the saved entries stay.
    private static bool Withdraw(string full)
    {
        if (!Custom.TryGetValue(full, out var landmark))
            return false;
        Custom.Remove(full);
        var controller = MapController.instance;
        if (controller != null && controller.mapInfos != null)
        {
            foreach (var map in controller.mapInfos)
                map?.landmarkInfos?.RemoveAll(i => i != null && ReferenceEquals(i.landmark, landmark));
        }
        HideMarker(full);
        if (landmark != null)
            Object.Destroy(landmark);
        return true;
    }

    private static void WithdrawMod(string mod)
    {
        string prefix = mod + ":";
        foreach (string full in new List<string>(Custom.Keys))
        {
            if (full.StartsWith(prefix, StringComparison.Ordinal))
                Withdraw(full);
        }
    }

    private static bool WritesAllowed(string call)
    {
        if (_save == null)
            return false;
        if (SaveGate.IsBusy || LandmarkPatches.Detached)
        {
            Log.Warn(call + " busy", "the game is saving or loading, try again once it is done");
            return false;
        }
        return true;
    }

    internal static bool IsCustom(Landmark landmark) =>
        !ReferenceEquals(landmark, null) && landmark.id != null && Custom.TryGetValue(landmark.id, out var known) && ReferenceEquals(known, landmark);

    private static Sprite LoadIcon(ObenMod owner, string path)
    {
        try { return ModAssets.Sprite(owner, path); }
        catch (Exception e)
        {
            Log.Warn("icon " + owner.Info.Id + " " + path, "the icon could not be loaded: " + e.Message);
            return null;
        }
    }

    private static string Trim(string title)
    {
        title ??= "";
        return title.Length > LandmarkStore.MaxTitleLength ? title.Substring(0, LandmarkStore.MaxTitleLength) : title;
    }
}
