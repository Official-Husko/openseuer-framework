using System.Linq;
using ObenLoader.Logging;
using ObenLoader.Mods;
using ObenLoader.Runtime;
using UnityEngine;

namespace ObenLoader;

/// <summary>Startup sequence: logging, discovery, then the first load batch.</summary>
public static class Loader
{
    /// <summary>ObenseuerExtended's version, as the build stamped it ("0.1.0").</summary>
    public static readonly string Version = BuildVersion();

    private static readonly ModLogger Log = new ModLogger("ObenLoader");

    /// <summary>The game's version (Application.version), checked against each mod's gameVersion.</summary>
    public static string GameVersion { get; private set; }

    /// <summary>
    /// The game's Steam build id, checked against each mod's gameBuilds, or null when the game wasn't
    /// installed through Steam (no app manifest next to it).
    /// </summary>
    public static string GameBuildId { get; private set; }

    /// <summary>
    /// Lowercase hex SHA-256 of the game's code (Assembly-CSharp.dll), the same for a game version whichever
    /// store it came from; checked against each mod's gameHashes. Null when the file can't be read.
    /// </summary>
    public static string GameCodeHash { get; private set; }

    /// <summary>Lowercase hex SHA-256 of the game's exe, for telling copies apart when hunting a problem.</summary>
    public static string GameExeHash { get; private set; }

    /// <summary>"0.5.13 (build 123456, code 72c44ab48d2e)": the version with whatever else is known.</summary>
    public static string GameBuild
    {
        get
        {
            string build = GameBuildId != null ? $"build {GameBuildId}, " : "";
            return GameCodeHash == null ? GameVersion : $"{GameVersion} ({build}code {GameFiles.Short(GameCodeHash)})";
        }
    }

    internal static void Start()
    {
        GameVersion = Application.version;
        LoaderPaths.Init();
        WindowTitle.Brand(Version);
        StartupStatus.Show("starting...");
        GameBuildId = SteamBuild.Read(LoaderPaths.GameDir);
        string kept = System.IO.Path.Combine(LoaderPaths.DataDir, "game-files.json");
        GameCodeHash = GameFiles.HashKept(System.IO.Path.Combine(Application.dataPath, "Managed", "Assembly-CSharp.dll"), kept);
        GameExeHash = GameFiles.HashKept(LoaderPaths.GameExe, kept);
        LogHub.Init(LoaderPaths.LogsDir);
        ModRegistry.Init(Log);
        // The panel can only be switched off once the settings are read; it opened with the first status.
        if (!ModRegistry.Settings.LoadingPanel)
        {
            StartupStatus.PanelEnabled = false;
            LoadingOverlay.Close();
        }
        if (ModRegistry.Settings.VerboseLog)
            LogHub.MinFileLevel = LogLevel.Debug;
        if (!ModRegistry.Settings.ModLogFiles)
            LogHub.StopModFiles();

        Log.Info($"ObenLoader {Version} on Unity {Application.unityVersion}, game {GameBuild}, instance {Instance.Number}");
        if (ModRegistry.Settings.RefuseOtherGameVersions)
            Log.Info("Mods made for another game version or build are refused (RefuseOtherGameVersions in ObenLoader/settings.json).");
        Log.Info($"Game folder: {LoaderPaths.GameDir} (exe {GameFiles.Short(GameExeHash) ?? "unreadable"})");

        InlineJumps.Use(Log);
        PatchLookups.Use(Log);
        ModLifecycle.Init(ModHost.Create(Log), Log);

        var found = ModDiscovery.Scan(LoaderPaths.ModsDir, Log);
        foreach (var mod in found)
            ModRegistry.Add(mod);
        // Only once every mod is known: a mod an essential mod needs stays on even if switched off.
        foreach (var mod in found)
        {
            if (!ModRegistry.IsEnabled(mod.Id))
                mod.State = ModState.Disabled;
        }

        ModLifecycle.LoadBatch(found.Where(m => m.State == ModState.Pending).ToList(), atRuntime: false);
        // Unity loads the first scene next, still without drawing anything.
        StartupStatus.Show("loading the main menu...", 1, 1);
        UnityEngine.SceneManagement.SceneManager.sceneLoaded += FirstScene;

        foreach (var mod in found.Where(m => m.State == ModState.Failed))
            Log.Error($"{mod}: {mod.Error}");

        Log.Info($"Loaded {found.Count(m => m.State == ModState.Loaded)} of {found.Count} mods");
        LogHub.Flush();
    }

    private static string BuildVersion()
    {
        var stamped = typeof(Loader).Assembly.GetCustomAttributes(typeof(System.Reflection.AssemblyInformationalVersionAttribute), false);
        string version = stamped.Length > 0 ? ((System.Reflection.AssemblyInformationalVersionAttribute)stamped[0]).InformationalVersion : null;
        if (string.IsNullOrEmpty(version))
            return typeof(Loader).Assembly.GetName().Version.ToString(3);
        // "0.1.0+abc1234": the commit is for logs, not for the title.
        int plus = version.IndexOf('+');
        return plus > 0 ? version.Substring(0, plus) : version;
    }

    private static void FirstScene(UnityEngine.SceneManagement.Scene scene, UnityEngine.SceneManagement.LoadSceneMode mode)
    {
        UnityEngine.SceneManagement.SceneManager.sceneLoaded -= FirstScene;
        StartupStatus.Done();
        // In case Unity set its own title again while making the window ready.
        WindowTitle.Brand(Version);
    }
}
