namespace ObenTool.Checks;

/// <summary>Finds the mod DLLs to check: the repo's builds, or what is installed in the game's Mods folder.</summary>
public static class ModDlls
{
    // Libraries that sit next to mods but aren't mods themselves.
    private static readonly string[] Skipped =
    {
        "0Harmony", "Mono.", "Newtonsoft.", "System.", "Microsoft.", "UnityEngine", "Unity.", "Assembly-CSharp", "netstandard",
    };

    public static string? FindRepoRoot()
    {
        foreach (string start in new[] { Environment.CurrentDirectory, AppContext.BaseDirectory })
        {
            for (var dir = new DirectoryInfo(start); dir != null; dir = dir.Parent)
            {
                if (File.Exists(Path.Combine(dir.FullName, "ObenseuerExtended.slnx")))
                    return dir.FullName;
            }
        }
        return null;
    }

    /// <summary>Release builds of the loader, the framework and every mod: src/*/bin/Release and mods/*/bin/Release.</summary>
    public static List<string> FromRepo(string repoRoot)
    {
        var found = new List<string>();
        foreach (string group in new[] { "src", "mods" })
        {
            string root = Path.Combine(repoRoot, group);
            if (!Directory.Exists(root))
                continue;
            foreach (string project in Directory.GetDirectories(root))
            {
                string release = Path.Combine(project, "bin", "Release");
                if (Directory.Exists(release))
                    found.AddRange(Directory.GetFiles(release, "*.dll", SearchOption.TopDirectoryOnly));
            }
        }
        return Filter(found);
    }

    /// <summary>The DLLs in each mod folder under the game's Mods folder.</summary>
    public static List<string> FromGame(string modsDir)
    {
        var found = new List<string>();
        if (Directory.Exists(modsDir))
        {
            foreach (string mod in Directory.GetDirectories(modsDir))
                found.AddRange(Directory.GetFiles(mod, "*.dll", SearchOption.TopDirectoryOnly));
        }
        return Filter(found);
    }

    private static List<string> Filter(IEnumerable<string> paths) => paths
        .Where(p => !Skipped.Any(s => Path.GetFileName(p).StartsWith(s, StringComparison.OrdinalIgnoreCase)))
        .OrderBy(p => p, StringComparer.Ordinal)
        .ToList();
}
