// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
using System;
using System.Collections.Generic;

namespace ObenFramework.World;

/// <summary>One mod landmark on one map, as kept in the save.</summary>
public sealed class LandmarkEntry
{
    public string Mod;
    public string Id;
    public string Title = "";

    /// <summary>The map it is on: the map's task item id or image name, as the game keys its map lists.</summary>
    public string Map;

    public float X;
    public float Y;
    public float Z;

    /// <summary>
    /// Where the icon comes from: <see cref="LandmarkStore.DotIcon"/>, a path relative to the mod's folder,
    /// or empty for a sprite the mod hands over at runtime (it must create the landmark again to show it).
    /// </summary>
    public string Icon = "";

    public int Priority;
    public int Width;
    public int Height;
}

/// <summary>Every mod landmark of one save, in the framework's own save file.</summary>
public sealed class LandmarkFile
{
    public int Version = 1;
    public List<LandmarkEntry> Entries = new List<LandmarkEntry>();
}

/// <summary>Limits and list operations for mod landmarks. No Unity types, covered by unit tests.</summary>
public static class LandmarkStore
{
    public const int MaxPerMod = 128;
    public const int MaxEntries = 1024;
    public const int MaxIdLength = 64;
    public const int MaxTitleLength = 128;
    public const string DotIcon = "builtin:dot";

    /// <summary>The id the game sees for a mod landmark: <c>modId:id</c>, so it never clashes with the game's.</summary>
    public static string FullId(string mod, string id) => mod + ":" + id;

    public static bool IdAllowed(string id)
    {
        if (string.IsNullOrEmpty(id) || id.Length > MaxIdLength)
            return false;
        foreach (char c in id)
        {
            if (!(c >= 'a' && c <= 'z' || c >= 'A' && c <= 'Z' || c >= '0' && c <= '9' || c == '.' || c == '_' || c == '-'))
                return false;
        }
        return true;
    }

    public static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);

    public static bool Valid(LandmarkEntry entry) =>
        entry != null && !string.IsNullOrEmpty(entry.Mod) && IdAllowed(entry.Id) && !string.IsNullOrEmpty(entry.Map)
        && Finite(entry.X) && Finite(entry.Y) && Finite(entry.Z) && entry.Width >= 0 && entry.Height >= 0
        && (entry.Title == null || entry.Title.Length <= MaxTitleLength) && IconAllowed(entry.Icon);

    /// <summary>Icons are the dot, nothing, or a relative path that stays inside the mod folder.</summary>
    public static bool IconAllowed(string icon)
    {
        if (string.IsNullOrEmpty(icon) || icon == DotIcon)
            return true;
        if (icon.IndexOf(':') >= 0 || icon.StartsWith("/", StringComparison.Ordinal) || icon.StartsWith("\\", StringComparison.Ordinal))
            return false;
        foreach (string part in icon.Split('/', '\\'))
        {
            if (part == "..")
                return false;
        }
        return true;
    }

    public static LandmarkEntry Find(List<LandmarkEntry> entries, string mod, string id, string map)
    {
        foreach (var entry in entries)
        {
            if (entry.Mod == mod && entry.Id == id && entry.Map == map)
                return entry;
        }
        return null;
    }

    public static int Count(List<LandmarkEntry> entries, string mod)
    {
        int count = 0;
        foreach (var entry in entries)
        {
            if (entry.Mod == mod)
                count++;
        }
        return count;
    }

    /// <summary>A new entry for <paramref name="mod"/> fits under both limits.</summary>
    public static bool HasRoom(List<LandmarkEntry> entries, string mod) =>
        entries.Count < MaxEntries && Count(entries, mod) < MaxPerMod;

    /// <summary>Whether a saved entry belongs on the map now shown, for a mod that is present.</summary>
    public static bool ShouldRestore(LandmarkEntry entry, string map, Func<string, bool> present) =>
        entry != null && entry.Map == map && present(entry.Mod);

    /// <summary>Only valid entries, no duplicates, within the limits.</summary>
    public static void Clean(LandmarkFile file)
    {
        if (file.Entries == null)
        {
            file.Entries = new List<LandmarkEntry>();
            return;
        }
        var kept = new List<LandmarkEntry>();
        foreach (var entry in file.Entries)
        {
            if (!Valid(entry) || !HasRoom(kept, entry.Mod) || Find(kept, entry.Mod, entry.Id, entry.Map) != null)
                continue;
            entry.Title ??= "";
            entry.Icon ??= "";
            kept.Add(entry);
        }
        file.Entries = kept;
    }
}
