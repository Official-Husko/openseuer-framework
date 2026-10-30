#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Oben.Reports;

/// <summary>
/// Which files go into a bug report and under what names, for the game and for tools outside it alike: they pass
/// the folders, this decides the rest.
/// </summary>
public static class ReportFiles
{
    /// <summary>A longer log keeps only its newest part, so the zip stays under GitHub's limit.</summary>
    public const long MaxLogBytes = 8L * 1024 * 1024;

    /// <summary>
    /// The loader's logs of this session and the one before (<paramref name="suffix"/> is "-2" for a second copy of
    /// the game), every mod's log, and Unity's Player.log with the one before it.
    /// </summary>
    public static IEnumerable<ReportEntry> Logs(string logsDir, string suffix, string? playerLog)
    {
        foreach (string name in new[] { "latest", "previous", "errors", "errors-previous", "runner" })
            yield return Log(Path.Combine(logsDir, name + suffix + ".log"), "logs/" + name + ".log");

        string mods = Path.Combine(logsDir, "mods");
        if (Directory.Exists(mods))
        {
            foreach (string file in Directory.GetFiles(mods, "*.log").OrderBy(f => f, StringComparer.OrdinalIgnoreCase))
                yield return Log(file, "logs/mods/" + Path.GetFileName(file));
        }

        if (!string.IsNullOrEmpty(playerLog))
        {
            yield return Log(playerLog!, "logs/Player.log");
            yield return Log(Path.Combine(Path.GetDirectoryName(playerLog) ?? "", "Player-prev.log"), "logs/Player-prev.log");
        }
    }

    /// <summary>
    /// The loader's settings and every mod's own settings files. Not mod.json, not the changelog, and not identity
    /// files (ObenCoop's player id, which would let someone pose as the player in a co-op game).
    /// </summary>
    public static IEnumerable<ReportEntry> Settings(string loaderDataDir, string modsDir)
    {
        foreach (string name in new[] { "settings.json", "game-check.json" })
            yield return new ReportEntry { Name = "settings/ObenLoader/" + name, Source = Path.Combine(loaderDataDir, name) };
        if (!Directory.Exists(modsDir))
            yield break;
        foreach (string dir in Directory.GetDirectories(modsDir).OrderBy(d => d, StringComparer.OrdinalIgnoreCase))
        {
            foreach (string file in Directory.GetFiles(dir, "*.json").OrderBy(f => f, StringComparer.OrdinalIgnoreCase))
            {
                string name = Path.GetFileName(file);
                if (!name.Equals("mod.json", StringComparison.OrdinalIgnoreCase) && !name.Equals("changelog.json", StringComparison.OrdinalIgnoreCase)
                    && !name.StartsWith("identity", StringComparison.OrdinalIgnoreCase))
                    yield return new ReportEntry { Name = "settings/Mods/" + Path.GetFileName(dir) + "/" + name, Source = file };
            }
        }
    }

    /// <summary>Every file of a save folder, as it is. Its name ("Esko_Virtanen/Slot2") comes back in <paramref name="name"/>.</summary>
    public static List<ReportEntry> Save(string? folder, out string? name)
    {
        name = null;
        if (folder == null || !Directory.Exists(folder))
            return new List<ReportEntry>();
        folder = folder.TrimEnd('/', '\\');
        name = Path.GetFileName(Path.GetDirectoryName(folder)) + "/" + Path.GetFileName(folder);
        string prefix = "save/" + name + "/";
        return Directory.GetFiles(folder, "*", SearchOption.AllDirectories)
            .OrderBy(f => f, StringComparer.OrdinalIgnoreCase)
            .Select(f => new ReportEntry { Name = prefix + f.Substring(folder.Length + 1).Replace('\\', '/'), Source = f, IsText = false })
            .ToList();
    }

    /// <summary>How old a crash can be and still go into a report.</summary>
    public static readonly TimeSpan CrashAge = TimeSpan.FromDays(7);

    /// <summary>
    /// The newest crash Unity's crash handler recorded in <paramref name="crashesDir"/> (one Crash_&lt;UTC time&gt; folder
    /// per crash with crash.dmp and a copy of Player.log), when it is younger than <see cref="CrashAge"/>. Its folder
    /// name comes back in <paramref name="name"/>.
    /// </summary>
    public static List<ReportEntry> Crash(string? crashesDir, DateTime nowUtc, out string? name)
    {
        name = null;
        var files = new List<ReportEntry>();
        if (crashesDir == null || !Directory.Exists(crashesDir))
            return files;
        string? newest = Directory.GetDirectories(crashesDir, "Crash_*")
            .OrderByDescending(Directory.GetLastWriteTimeUtc)
            .FirstOrDefault();
        if (newest == null || nowUtc - Directory.GetLastWriteTimeUtc(newest) > CrashAge)
            return files;
        name = Path.GetFileName(newest);
        foreach (string file in Directory.GetFiles(newest).OrderBy(f => f, StringComparer.OrdinalIgnoreCase))
        {
            bool log = file.EndsWith(".log", StringComparison.OrdinalIgnoreCase);
            files.Add(new ReportEntry { Name = "crash/" + name + "/" + Path.GetFileName(file), Source = file, IsText = log, MaxBytes = log ? MaxLogBytes : 0 });
        }
        return files;
    }

    /// <summary>
    /// The save written last, of any character: Saves/&lt;character&gt;/&lt;slot&gt;/Info.tnmt marks a used slot.
    /// Null when there is none.
    /// </summary>
    public static string? NewestSave(string savesDir)
    {
        if (!Directory.Exists(savesDir))
            return null;
        return Directory.GetDirectories(savesDir)
            .SelectMany(Directory.GetDirectories)
            .Where(slot => File.Exists(Path.Combine(slot, "Info.tnmt")))
            .OrderByDescending(slot => File.GetLastWriteTimeUtc(Path.Combine(slot, "Info.tnmt")))
            .FirstOrDefault();
    }

    private static ReportEntry Log(string path, string name) => new ReportEntry { Name = name, Source = path, MaxBytes = MaxLogBytes };
}
