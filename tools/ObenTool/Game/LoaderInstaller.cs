namespace ObenTool.Game;

/// <summary>Puts the loader into a game install and takes it out again.</summary>
public sealed class LoaderInstaller
{
    private static readonly string[] PayloadFiles = { "ObenLoader.dll", "ObenLoader.pdb", "0Harmony.dll", "Mono.Cecil.dll" };

    private readonly GameInstall _game;
    private readonly UnityManifests _manifests;

    public LoaderInstaller(GameInstall game)
    {
        _game = game;
        _manifests = new UnityManifests(game);
    }

    public static string PayloadDir => Path.Combine(AppContext.BaseDirectory, "payload");

    public bool FilesPresent => File.Exists(Path.Combine(_game.ManagedDir, "ObenLoader.dll"))
                                && File.Exists(Path.Combine(_game.ManagedDir, "0Harmony.dll"));

    public bool ManifestsPatched => _manifests.IsPatched;

    public IEnumerable<string> Install()
    {
        foreach (string file in PayloadFiles)
        {
            string source = Path.Combine(PayloadDir, file);
            if (!File.Exists(source))
            {
                if (file.EndsWith(".pdb"))
                    continue;
                throw new FileNotFoundException($"Missing {file} next to the tool. Build the solution first.", source);
            }
            File.Copy(source, Path.Combine(_game.ManagedDir, file), overwrite: true);
            yield return $"Copied {file}";
        }

        _manifests.Patch();
        yield return "Registered the loader with Unity";

        Directory.CreateDirectory(_game.ModsDir);
        yield return $"Mods folder: {_game.ModsDir}";
    }

    public IEnumerable<string> Uninstall(bool removeMods)
    {
        _manifests.Unpatch();
        yield return "Restored Unity's startup files";

        foreach (string file in PayloadFiles)
        {
            string path = Path.Combine(_game.ManagedDir, file);
            if (!File.Exists(path))
                continue;
            File.Delete(path);
            yield return $"Removed {file}";
        }

        if (removeMods)
        {
            foreach (string dir in new[] { _game.ModsDir, _game.LoaderDataDir })
            {
                if (!Directory.Exists(dir))
                    continue;
                Directory.Delete(dir, recursive: true);
                yield return $"Deleted {dir}";
            }
        }
    }
}
