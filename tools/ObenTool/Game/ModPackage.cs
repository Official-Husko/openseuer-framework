namespace ObenTool.Game;

/// <summary>Copies mod folders (each with a mod.json) from a release package into the game's Mods folder.</summary>
public static class ModPackage
{
    /// <summary>
    /// Copies every file of every mod folder in <paramref name="source"/> over the installed one.
    /// Files that only exist in the game (settings, saved choices) are left alone.
    /// </summary>
    public static IEnumerable<string> Install(GameInstall game, string source)
    {
        if (!Directory.Exists(source))
            throw new DirectoryNotFoundException($"No mods folder at {source}.");
        var mods = Directory.GetDirectories(source).Where(d => File.Exists(Path.Combine(d, "mod.json"))).ToList();
        if (mods.Count == 0)
            throw new InvalidOperationException($"{source} has no mod folders (folders with a mod.json).");

        Directory.CreateDirectory(game.ModsDir);
        foreach (string mod in mods)
        {
            string target = Path.Combine(game.ModsDir, Path.GetFileName(mod));
            foreach (string file in Directory.GetFiles(mod, "*", SearchOption.AllDirectories))
            {
                string destination = Path.Combine(target, Path.GetRelativePath(mod, file));
                Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
                File.Copy(file, destination, overwrite: true);
            }
            yield return $"Installed {Path.GetFileName(mod)}";
        }
    }
}
