using System;
using System.IO;
using System.Text.RegularExpressions;

namespace ObenLoader.Runtime;

/// <summary>
/// The game's Steam build id, read from Steam's app manifest (<c>steamapps/appmanifest_951240.acf</c>, two
/// folders above the game, or a copy of it kept in the game folder or one of its folders, as depot downloads
/// keep it). Copies from other stores have none; <see cref="GameFiles"/> identifies those by their code.
/// </summary>
internal static class SteamBuild
{
    public const string AppId = "951240";

    private static readonly Regex BuildIdField = new Regex("\"buildid\"\\s+\"(\\d+)\"", RegexOptions.IgnoreCase);
    private static readonly Regex InstallDirField = new Regex("\"installdir\"\\s+\"([^\"]*)\"", RegexOptions.IgnoreCase);

    /// <summary>The build id of the game in <paramref name="gameDir"/>, or null when there is no Steam manifest for it.</summary>
    public static string Read(string gameDir)
    {
        try
        {
            string kept = KeptCopy(gameDir);
            if (kept != null)
                return Parse(File.ReadAllText(kept), null);

            var common = Directory.GetParent(gameDir);
            var steamApps = common?.Parent;
            if (steamApps == null || !steamApps.Exists)
                return null;

            string own = Path.Combine(steamApps.FullName, "appmanifest_" + AppId + ".acf");
            if (File.Exists(own))
                return Parse(File.ReadAllText(own), null);

            // A different app id (a test branch build): find the manifest that installs into this folder.
            string folder = Path.GetFileName(gameDir.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
            foreach (string manifest in Directory.GetFiles(steamApps.FullName, "appmanifest_*.acf"))
            {
                string id = Parse(File.ReadAllText(manifest), folder);
                if (id != null)
                    return id;
            }
        }
        catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
        {
        }
        return null;
    }

    // A manifest kept with the game (in its folder or one folder down) beats guessing at a library.
    private static string KeptCopy(string gameDir)
    {
        string name = "appmanifest_" + AppId + ".acf";
        string direct = Path.Combine(gameDir, name);
        if (File.Exists(direct))
            return direct;
        foreach (string sub in Directory.GetDirectories(gameDir))
        {
            string inside = Path.Combine(sub, name);
            if (File.Exists(inside))
                return inside;
        }
        return null;
    }

    /// <summary>
    /// The build id in an app manifest's text, or null. With <paramref name="installDir"/> the manifest must
    /// install into a folder of that name.
    /// </summary>
    public static string Parse(string manifest, string installDir)
    {
        if (string.IsNullOrEmpty(manifest))
            return null;
        if (installDir != null)
        {
            var dir = InstallDirField.Match(manifest);
            if (!dir.Success || !string.Equals(dir.Groups[1].Value, installDir, StringComparison.OrdinalIgnoreCase))
                return null;
        }
        var build = BuildIdField.Match(manifest);
        return build.Success ? build.Groups[1].Value : null;
    }
}
