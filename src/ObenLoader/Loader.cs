using System.Linq;
using ObenLoader.Logging;
using ObenLoader.Mods;
using ObenLoader.Runtime;
using UnityEngine;

namespace ObenLoader;

/// <summary>Startup sequence: logging, discovery, then the first load batch.</summary>
public static class Loader
{
    public const string Version = "0.1.0";

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
        GameBuildId = SteamBuild.Read(LoaderPaths.GameDir);
        GameCodeHash = GameFiles.Hash(System.IO.Path.Combine(Application.dataPath, "Managed", "Assembly-CSharp.dll"));
        GameExeHash = GameFiles.Hash(LoaderPaths.GameExe);
        LogHub.Init(LoaderPaths.LogsDir);
        ModRegistry.Init(Log);
        if (ModRegistry.Settings.VerboseLog)
            LogHub.MinFileLevel = LogLevel.Debug;

        Log.Info($"ObenLoader {Version} on Unity {Application.unityVersion}, game {GameBuild}, instance {Instance.Number}");
        if (ModRegistry.Settings.RefuseOtherGameVersions)
            Log.Info("Mods made for another game version or build are refused (RefuseOtherGameVersions in ObenLoader/settings.json).");
        Log.Info($"Game folder: {LoaderPaths.GameDir} (exe {GameFiles.Short(GameExeHash) ?? "unreadable"})");

        InlineJumps.Use(Log);
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

        foreach (var mod in found.Where(m => m.State == ModState.Failed))
            Log.Error($"{mod}: {mod.Error}");

        Log.Info($"Loaded {found.Count(m => m.State == ModState.Loaded)} of {found.Count} mods");
        LogHub.Flush();
    }
}
