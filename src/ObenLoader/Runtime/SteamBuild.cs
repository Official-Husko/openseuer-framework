using System;
using System.IO;
using System.Text.RegularExpressions;

namespace ObenLoader.Runtime;

/// <summary>
/// The game's Steam build id, read from Steam's app manifest (<c>steamapps/appmanifest_951240.acf</c>, two
/// folders above the game). Copies installed some other way (a sideloaded or DRM-free install) have none.
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
