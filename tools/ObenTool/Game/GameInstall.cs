namespace ObenTool.Game;

/// <summary>An Obenseuer install on disk.</summary>
public sealed class GameInstall
{
    public const string FolderName = "Obenseuer";
    public const string ExeName = "Obenseuer.exe";

    /// <summary>Where Unity keeps Player.log and settings, relative to the Windows user folder.</summary>
    public const string PersistentRelative = "AppData/LocalLow/Loiste Interactive/Obenseuer";

    public string Dir { get; }
    public string DataDir => Path.Combine(Dir, "Obenseuer_Data");
    public string ManagedDir => Path.Combine(DataDir, "Managed");
    public string ModsDir => Path.Combine(Dir, "Mods");
    public string LoaderDataDir => Path.Combine(Dir, "ObenLoader");
    public string LogsDir => Path.Combine(LoaderDataDir, "logs");
    public string Exe => Path.Combine(Dir, ExeName);

    /// <summary>"" for the first instance, "-2" for the second: what the loader adds to its file names.</summary>
    public static string Suffix(int instance) => instance <= 1 ? "" : "-" + instance;

    public string LoaderLogFile(int instance = 1) => Path.Combine(LogsDir, "latest" + Suffix(instance) + ".log");

    /// <summary>The loader's error report: what went wrong this session, for bug reports. Missing when nothing did.</summary>
    public string ErrorLogFile(int instance = 1) => Path.Combine(LogsDir, "errors" + Suffix(instance) + ".log");

    /// <summary>Unity's log. Extra instances write theirs next to the loader logs (see <c>launch --instance</c>).</summary>
    public string PlayerLogFile(int instance = 1) => instance <= 1
        ? Path.Combine(PersistentDataDir() ?? "", "Player.log")
        : Path.Combine(LogsDir, $"player{Suffix(instance)}.log");

    private GameInstall(string dir)
    {
        Dir = Path.GetFullPath(dir);
    }

    /// <summary>Uses --game, then OBENSEUER_DIR, the repo's build/Local.props, Heroic and every Steam library.</summary>
    public static GameInstall Locate(string? explicitDir)
    {
        foreach (string? candidate in Candidates(explicitDir))
        {
            if (candidate != null && File.Exists(Path.Combine(candidate, ExeName)))
                return new GameInstall(candidate);
        }
        throw new InvalidOperationException(
            "Couldn't find Obenseuer. Pass --game <folder> or set OBENSEUER_DIR.");
    }

    private static IEnumerable<string?> Candidates(string? explicitDir)
    {
        yield return explicitDir;
        yield return Environment.GetEnvironmentVariable("OBENSEUER_DIR");
        yield return RepoSettings.GameDir();
        foreach (var app in Heroic.SideloadedApps())
            if (app.Executable.EndsWith(ExeName, StringComparison.OrdinalIgnoreCase))
                yield return Path.GetDirectoryName(app.Executable);
        foreach (string lib in SteamLibraries.Folders())
            yield return Path.Combine(lib, "common", FolderName);
    }

    /// <summary>Folder holding Player.log and the game's settings (inside the Wine prefix on Linux).</summary>
    public string? PersistentDataDir()
    {
        if (OperatingSystem.IsWindows())
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), PersistentRelative);

        string? prefix = LaunchProfile.Resolve(this).Prefix;
        return prefix is null ? null : Path.Combine(prefix, "drive_c/users/steamuser", PersistentRelative);
    }
}
