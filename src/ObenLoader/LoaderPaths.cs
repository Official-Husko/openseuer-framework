using System.IO;
using UnityEngine;

namespace ObenLoader;

/// <summary>Folder layout used by the loader. All paths are absolute.</summary>
public static class LoaderPaths
{
    /// <summary>The game's install folder (the one containing Obenseuer.exe).</summary>
    public static string GameDir { get; private set; }

    /// <summary>&lt;game&gt;/Mods, one sub-folder per mod.</summary>
    public static string ModsDir { get; private set; }

    /// <summary>&lt;game&gt;/ObenLoader, loader settings and logs.</summary>
    public static string DataDir { get; private set; }

    public static string LogsDir { get; private set; }

    /// <summary>The game's exe, named after its data folder ("Obenseuer_Data" goes with "Obenseuer.exe").</summary>
    public static string GameExe { get; private set; }

    internal static void Init()
    {
        GameDir = Path.GetDirectoryName(Application.dataPath);
        ModsDir = Path.Combine(GameDir, "Mods");
        DataDir = Path.Combine(GameDir, "ObenLoader");
        LogsDir = Path.Combine(DataDir, "logs");
        string data = Path.GetFileName(Application.dataPath);
        GameExe = Path.Combine(GameDir, (data.EndsWith("_Data") ? data.Substring(0, data.Length - 5) : "Obenseuer") + ".exe");

        Directory.CreateDirectory(ModsDir);
        Directory.CreateDirectory(LogsDir);
    }
}
