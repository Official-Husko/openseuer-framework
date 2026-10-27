using System.Text.RegularExpressions;

namespace ObenTool.Game;

/// <summary>Finds Steam's library folders (each one's steamapps directory) from libraryfolders.vdf.</summary>
public static partial class SteamLibraries
{
    public static IEnumerable<string> Folders()
    {
        foreach (string root in Roots())
        {
            string vdf = Path.Combine(root, "steamapps", "libraryfolders.vdf");
            if (!File.Exists(vdf))
                continue;
            yield return Path.Combine(root, "steamapps");
            foreach (Match m in PathLine().Matches(File.ReadAllText(vdf)))
                yield return Path.Combine(m.Groups[1].Value.Replace(@"\\", @"\"), "steamapps");
        }
    }

    private static IEnumerable<string> Roots()
    {
        if (OperatingSystem.IsWindows())
        {
            yield return @"C:\Program Files (x86)\Steam";
            yield break;
        }
        string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        yield return Path.Combine(home, ".local/share/Steam");
        yield return Path.Combine(home, ".steam/steam");
        yield return Path.Combine(home, ".var/app/com.valvesoftware.Steam/.local/share/Steam");
    }

    [GeneratedRegex("\"path\"\\s+\"([^\"]+)\"")]
    private static partial Regex PathLine();
}
