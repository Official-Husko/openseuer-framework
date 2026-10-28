// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
using System;
using System.Collections.Generic;
using System.IO;

namespace ObenFramework.World;

/// <summary>
/// The rules behind <see cref="WorldLocation.VisitedScenes"/>: merging the sources of "where has the player
/// been" and reading the level files of a save folder. No Unity types, covered by unit tests.
/// </summary>
public static class VisitedSceneRules
{
    public static readonly StringComparer NameComparer = StringComparer.OrdinalIgnoreCase;

    /// <summary>Scenes that are not places: the menu and the intro.</summary>
    public static readonly HashSet<string> NonGameplay = new HashSet<string>(NameComparer) { "Main Menu", "Open Sewer Intro" };

    /// <summary>Names from every source once each, in first-seen order, without empty names or non-gameplay scenes.</summary>
    public static List<string> Merge(params IEnumerable<string>[] sources)
    {
        var result = new List<string>();
        var seen = new HashSet<string>(NameComparer);
        foreach (var source in sources)
        {
            if (source == null)
                continue;
            foreach (string name in source)
            {
                if (!string.IsNullOrEmpty(name) && !NonGameplay.Contains(name) && seen.Add(name))
                    result.Add(name);
            }
        }
        return result;
    }

    /// <summary>
    /// The levels a save folder has state for: one <c>&lt;Level&gt;.tnmt</c> per level the game saved,
    /// besides <c>Info</c> and <c>Globals</c>. Empty when the folder doesn't exist.
    /// </summary>
    public static HashSet<string> LevelFiles(string folder)
    {
        var names = new HashSet<string>(NameComparer);
        if (string.IsNullOrEmpty(folder) || !Directory.Exists(folder))
            return names;
        foreach (string path in Directory.GetFiles(folder, "*.tnmt"))
        {
            string name = Path.GetFileNameWithoutExtension(path);
            if (!NameComparer.Equals(name, "Globals") && !NameComparer.Equals(name, "Info"))
                names.Add(name);
        }
        return names;
    }
}
