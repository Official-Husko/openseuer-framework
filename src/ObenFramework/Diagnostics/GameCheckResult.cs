using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using Oben.Checks;

namespace ObenFramework.Diagnostics;

/// <summary>A hook of a mod that doesn't fit the running game, from <see cref="GameCheck"/>.</summary>
public readonly struct GameCheckProblem
{
    public GameCheckProblem(string mod, string cls, string hook, string message, bool ambiguous)
    {
        Mod = mod;
        Class = cls;
        Hook = hook;
        Message = message;
        Ambiguous = ambiguous;
    }

    /// <summary>The mod's id.</summary>
    public string Mod { get; }

    /// <summary>The class in the mod the hook is in.</summary>
    public string Class { get; }

    /// <summary>The game member it targets.</summary>
    public string Hook { get; }

    public string Message { get; }

    /// <summary>More than one member matches (rather than none).</summary>
    public bool Ambiguous { get; }

    public override string ToString() => $"{Mod}: {Class}: {Hook}: {Message}";
}

/// <summary>What <see cref="GameCheck"/> found.</summary>
public sealed class GameCheckResult
{
    private GameCheckResult(string game, bool verified, string mismatch)
    {
        Game = game;
        VerifiedGame = verified;
        GameMismatch = mismatch;
    }

    /// <summary>The game checked against ("0.5.13 (build 26179069, code 72c44ab48d2e)").</summary>
    public string Game { get; }

    /// <summary>The game is the one the framework was verified on (<see cref="FrameworkInfo.IsVerifiedGame"/>).</summary>
    public bool VerifiedGame { get; }

    public string GameMismatch { get; }

    public int Mods { get; private set; }

    /// <summary>Hooks and member lookups checked.</summary>
    public int Checked { get; private set; }

    /// <summary>Hooks whose target is only known at run time, so they can't be checked.</summary>
    public int Dynamic { get; private set; }

    public IReadOnlyList<GameCheckProblem> Problems { get; private set; } = Array.Empty<GameCheckProblem>();

    public double Seconds { get; private set; }

    /// <summary>Why the check itself couldn't run, or null.</summary>
    public string Failure { get; private set; }

    /// <summary>When the assemblies were read: now, or an earlier start when nothing changed since.</summary>
    public DateTime CheckedAt { get; private set; } = DateTime.Now;

    /// <summary>Taken from an earlier start, since neither the game's code nor any checked DLL changed.</summary>
    public bool FromCache { get; private set; }

    /// <summary>The check ran and every hook fits.</summary>
    public bool AllFit => Failure == null && Problems.Count == 0;

    internal GameCheckRecord ToRecord(string fingerprint)
    {
        var record = new GameCheckRecord
        {
            Fingerprint = fingerprint,
            CheckedAt = CheckedAt,
            Game = Game,
            Mods = Mods,
            Checked = Checked,
            Dynamic = Dynamic,
            Seconds = Seconds,
        };
        foreach (var problem in Problems)
        {
            record.Problems.Add(new GameCheckProblemRecord
            {
                Mod = problem.Mod, Class = problem.Class, Hook = problem.Hook, Message = problem.Message, Ambiguous = problem.Ambiguous,
            });
        }
        return record;
    }

    // Whether the game is the verified one comes from the framework's mod.json now, not from the cache.
    internal static GameCheckResult FromRecord(GameCheckRecord record, bool verified, string mismatch)
    {
        var problems = new List<GameCheckProblem>();
        foreach (var problem in record.Problems ?? new List<GameCheckProblemRecord>())
            problems.Add(new GameCheckProblem(problem.Mod, problem.Class, problem.Hook, problem.Message, problem.Ambiguous));
        return new GameCheckResult(record.Game, verified, mismatch)
        {
            Mods = record.Mods,
            Checked = record.Checked,
            Dynamic = record.Dynamic,
            Seconds = record.Seconds,
            Problems = problems,
            CheckedAt = record.CheckedAt,
            FromCache = true,
        };
    }

    internal static GameCheckResult Failed(string game, bool verified, string mismatch, string why) =>
        new GameCheckResult(game, verified, mismatch) { Failure = why ?? "unknown error" };

    internal static GameCheckResult From(string game, bool verified, string mismatch, List<ModReport> reports,
        Dictionary<string, string> modOfFile, double seconds)
    {
        var problems = new List<GameCheckProblem>();
        var mods = new HashSet<string>();
        int checkedHooks = 0, dynamic = 0;
        foreach (var report in reports)
        {
            string mod = modOfFile.TryGetValue(report.Mod, out var id) ? id : report.Mod;
            mods.Add(mod);
            checkedHooks += report.Checked;
            dynamic += report.Dynamic;
            foreach (var finding in report.Findings)
            {
                if (finding.Severity != Severity.Warning)
                    problems.Add(new GameCheckProblem(mod, finding.Class, finding.Hook, finding.Message, finding.Severity == Severity.Ambiguous));
            }
        }
        return new GameCheckResult(game, verified, mismatch)
        {
            Mods = mods.Count,
            Checked = checkedHooks,
            Dynamic = dynamic,
            Problems = problems,
            Seconds = seconds,
        };
    }
}

/// <summary>The worker-thread part: reads files only, touches nothing of Unity or the game.</summary>
internal static class GameCheckRun
{
    public static GameCheckResult Run(string managedDir, List<KeyValuePair<string, string>> targets, string game, bool verified, string mismatch)
    {
        var clock = Stopwatch.StartNew();
        var modOfFile = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var dlls = new List<string>();
        foreach (var target in targets)
        {
            if (!File.Exists(target.Value))
                continue;
            dlls.Add(target.Value);
            modOfFile[Path.GetFileNameWithoutExtension(target.Value)] = target.Key;
        }
        using var assemblies = new GameAssemblies(managedDir);
        var reports = CheckRunner.Run(assemblies, dlls);
        return GameCheckResult.From(game, verified, mismatch, reports, modOfFile, clock.Elapsed.TotalSeconds);
    }
}
