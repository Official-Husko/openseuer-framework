using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using ObenLoader;
using ObenLoader.Config;

namespace ObenFramework.Diagnostics;

/// <summary>A game check result as kept on disk, with the fingerprint of what it was made from.</summary>
internal sealed class GameCheckRecord
{
    public string Fingerprint;
    public DateTime CheckedAt;
    public string Game;
    public int Mods;
    public int Checked;
    public int Dynamic;
    public double Seconds;
    public List<GameCheckProblemRecord> Problems = new List<GameCheckProblemRecord>();
}

internal sealed class GameCheckProblemRecord
{
    public string Mod;
    public string Class;
    public string Hook;
    public string Message;
    public bool Ambiguous;
}

/// <summary>
/// Keeps the last game check in ObenLoader/game-check.json. The result only changes when the game's code or
/// one of the checked DLLs does (the framework's own DLL, which holds the checks, is one of them), so a start
/// with the same files reuses it instead of reading every assembly again.
/// </summary>
internal static class GameCheckCache
{
    private static string FilePath => Path.Combine(LoaderPaths.DataDir, "game-check.json");

    /// <summary>The game's code hash and every checked DLL's mod, name, size and write time.</summary>
    public static string Fingerprint(string codeHash, List<KeyValuePair<string, string>> targets)
    {
        var sorted = new List<KeyValuePair<string, string>>(targets);
        sorted.Sort((a, b) => string.CompareOrdinal(a.Key + "/" + a.Value, b.Key + "/" + b.Value));
        var text = new StringBuilder(codeHash ?? "unknown");
        foreach (var target in sorted)
        {
            var file = new FileInfo(target.Value);
            text.Append('|').Append(target.Key).Append(':').Append(file.Name).Append(':');
            text.Append(file.Exists ? file.Length + ":" + file.LastWriteTimeUtc.Ticks : "missing");
        }
        return text.ToString();
    }

    /// <summary>The kept result when it was made from exactly these files, else null.</summary>
    public static GameCheckRecord Read(string fingerprint)
    {
        if (!File.Exists(FilePath) || !JsonFile.TryRead(FilePath, out GameCheckRecord record, out _))
            return null;
        return record != null && record.Fingerprint == fingerprint ? record : null;
    }

    public static void Write(GameCheckRecord record)
    {
        try
        {
            JsonFile.Write(FilePath, record);
        }
        catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
        {
            // Only a cache: the next start checks again.
        }
    }
}
