using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using Newtonsoft.Json;
using ObenLoader;
using ObenLoader.Config;
using ObenLoader.Logging;
using ObenLoader.Runtime;
using ObenFramework.Saves;
using Oben.Reports;
using UnityEngine;

namespace ObenFramework.Diagnostics.Reports;

/// <summary>A bug report zip that was just made.</summary>
public sealed class BugReport
{
    public string Path { get; internal set; }
    public long Bytes { get; internal set; }

    /// <summary>report.md: the versions, the system and what is in the zip, to paste into an issue.</summary>
    public string Summary { get; internal set; }

    /// <summary>A new GitHub bug report with the versions and the ticked PC parts filled in.</summary>
    public string IssueUrl { get; internal set; }

    /// <summary>The save in it ("Esko_Virtanen/Slot2"), or null.</summary>
    public string Save { get; internal set; }

    /// <summary>Files cut or left out, said plainly.</summary>
    public IReadOnlyList<string> Notes { get; internal set; }

    /// <summary>Larger than GitHub takes as an attachment.</summary>
    public bool TooBigForGitHub => Bytes > ReportPackager.GitHubLimit;
}

/// <summary>
/// Bug reports: one zip with the versions, the system, the logs, the mods' settings and, if the player wants, the PC's
/// processor, graphics and memory and the save being played. Personal details are hidden unless the player says
/// otherwise. The same reports come from the MODS menu's Report page, the F1 console ("report") and, while the game
/// won't start, "oben report". The game also writes ObenLoader/system.json at every start for that last one.
/// The formats are in docs/modding/reports.md.
/// </summary>
public static class BugReports
{
    /// <summary>How many reports the folder keeps; older ones are deleted when a new one is made.</summary>
    public const int Kept = 10;

    internal static ModLogger Log;

    /// <summary>ObenLoader/reports.</summary>
    public static string Folder => Path.Combine(LoaderPaths.DataDir, "reports");

    /// <summary>Where Unity's crash handler leaves a folder per crash: %TEMP%\&lt;company&gt;\&lt;product&gt;\Crashes.</summary>
    public static string CrashesFolder => Path.Combine(Path.GetTempPath(), Application.companyName, Application.productName, "Crashes");

    /// <summary>ObenLoader/system.json, written at every start once the mods are loaded.</summary>
    public static string SnapshotPath => Path.Combine(LoaderPaths.DataDir, "system.json");

    /// <summary>The newest report made this session, or null.</summary>
    public static BugReport Last { get; private set; }

    /// <summary>The machine and install as a report describes them, everything included.</summary>
    public static SystemSnapshot Snapshot() => SystemReader.Read();

    /// <summary>A report is being made right now.</summary>
    public static bool IsBusy { get; private set; }

    /// <summary>
    /// Makes a report zip in <see cref="Folder"/>. What Unity has to answer is read now, on the main thread; reading,
    /// cleaning and packing the files happens on a worker, so the game doesn't stop for it. <paramref name="done"/> runs
    /// on the main thread with the report, or with null when it failed (the log says why). Returns false when another
    /// report is still being made.
    /// </summary>
    public static bool Make(ReportOptions options, Action<BugReport> done)
    {
        if (IsBusy)
            return false;
        options ??= new ReportOptions();
        DateTime made = DateTime.Now;
        var system = SystemReader.Read().For(options);

        var files = new List<ReportEntry>();
        files.AddRange(ReportFiles.Logs(LoaderPaths.LogsDir, Instance.Suffix, Application.consoleLogPath));
        files.AddRange(ReportFiles.Settings(LoaderPaths.DataDir, LoaderPaths.ModsDir));
        string save = null;
        if (options.Save)
            files.AddRange(ReportFiles.Save(SaveSlots.CurrentFolder ?? ReportFiles.NewestSave(SaveSlots.SavesFolder), out save));
        string crash = null;
        if (options.Crash)
            files.AddRange(ReportFiles.Crash(CrashesFolder, DateTime.UtcNow, out crash));

        var problems = ErrorReport.Snapshot();
        problems.Reverse();
        var titles = problems.Select(p => p.Source + ": " + p.Title).ToList();
        var entries = ReportAssembly.Entries(system, options, titles, ErrorReport.Count, files, save, made,
            s => JsonConvert.SerializeObject(s, JsonFile.Settings), out string summary, crash);

        var redactor = ReportAssembly.Redactor(options, Environment.UserName);
        string path = Path.Combine(Folder, ReportAssembly.FileName(made));
        string issue = IssueLink.Bug(system);

        IsBusy = true;
        ThreadPool.QueueUserWorkItem(_ =>
        {
            BugReport report = null;
            try
            {
                var outcome = ReportPackager.Write(path, entries, redactor);
                Prune();
                report = new BugReport
                {
                    Path = path,
                    Bytes = outcome.Bytes,
                    Summary = redactor == null ? summary : redactor.Apply(summary),
                    IssueUrl = issue,
                    Save = save,
                    Notes = outcome.Notes,
                };
                Log?.Info($"Made a bug report: {path} ({outcome.Bytes / 1024} KB, {outcome.Files.Count} files)");
            }
            catch (Exception e)
            {
                Log?.Error("Making a bug report failed", e);
            }
            MainThread.Post(() =>
            {
                IsBusy = false;
                if (report != null)
                    Last = report;
                try { done?.Invoke(report); }
                catch (Exception e) { Log?.Error("A bug report's caller failed", e); }
            });
        });
        return true;
    }

    /// <summary>Writes system.json for tools outside the game. Never throws.</summary>
    public static void WriteSnapshot()
    {
        try
        {
            JsonFile.Write(SnapshotPath, SystemReader.Read());
        }
        catch (Exception e)
        {
            Log?.Warn("Couldn't write system.json: " + e.Message);
        }
    }

    /// <summary>Shows the reports folder in the file manager.</summary>
    public static void OpenFolder()
    {
        Directory.CreateDirectory(Folder);
        Application.OpenURL(new Uri(Folder + Path.DirectorySeparatorChar).AbsoluteUri);
    }

    /// <summary>Opens a new GitHub bug report in the browser, filled from <paramref name="report"/> (or the last one).</summary>
    public static void OpenIssue(BugReport report = null)
    {
        report ??= Last;
        Application.OpenURL(report?.IssueUrl ?? IssueLink.Repository + "/issues/new?template=" + IssueLink.Template);
    }

    internal static void Init(ObenMod framework)
    {
        Log = framework.Log;
        BugReportCommand.Register(framework);
    }

    private static void Prune()
    {
        foreach (var old in new DirectoryInfo(Folder).GetFiles("ObenseuerExtended-report-*.zip")
                     .OrderByDescending(f => f.Name, StringComparer.Ordinal).Skip(Kept))
        {
            try { old.Delete(); }
            catch (IOException) { }
        }
    }
}
