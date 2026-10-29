// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using ObenFramework.Events;
using ObenLoader;
using ObenLoader.Logging;
using ObenLoader.Mods;
using ObenLoader.Runtime;
using UnityEngine;

namespace ObenFramework.Diagnostics;

/// <summary>
/// Checks every loaded mod against the running game once per game start, like <c>oben check</c> does from
/// outside (the reference framework's access probe, for every mod): each Harmony target and each private
/// member a mod looks up through <c>AccessTools</c> must still be in the game's code. It reads the DLLs with
/// Cecil on a worker thread, so start-up doesn't wait. Problems are logged and go on the Errors page;
/// <see cref="Finished"/> fires on the main thread (ObenUIKit shows the result at the main menu).
/// </summary>
public static class GameCheck
{
    private const string DoneKey = "ObenFramework.GameCheck.Done";
    private const int MaxLoggedProblems = 25;

    private static ModLogger _log;
    private static bool _running;
    private static bool _ranThisSession;

    /// <summary>The check finished; on the main thread.</summary>
    public static readonly GameEvent<GameCheckResult> Finished = new GameEvent<GameCheckResult>(nameof(Finished));

    /// <summary>The last result this framework copy produced, or null (none yet, or it ran before a hot reload).</summary>
    public static GameCheckResult Last { get; private set; }

    public static bool IsRunning => _running;

    internal static void Init(ObenMod framework)
    {
        _log = framework.Log;
        _running = false;
        Last = null;
        ReloadState.Take(DoneKey, out _ranThisSession);
        framework.Track(Disposable.Create(() => ReloadState.Put(DoneKey, _ranThisSession)));
    }

    /// <summary>
    /// Called once the loader has loaded the batch the framework came in with (the mods' DLLs are known only
    /// then). Once per game start: a hot reload of the framework doesn't change the game's code. When neither
    /// the game's code nor any checked DLL changed since the last check, its result is used as it is.
    /// </summary>
    internal static void StartOnce()
    {
        if (!_ranThisSession)
            Start(force: false);
    }

    /// <summary>
    /// Checks again in the background (after mods were added, say), even when nothing changed. False when a
    /// check is running.
    /// </summary>
    public static bool Start() => Start(force: true);

    private static bool Start(bool force)
    {
        if (_running)
            return false;
        _ranThisSession = true;
        var targets = Targets();
        string fingerprint = GameCheckCache.Fingerprint(Loader.GameCodeHash, targets);
        bool verified = FrameworkInfo.IsVerifiedGame;
        string mismatch = FrameworkInfo.GameMismatch;
        var kept = force ? null : GameCheckCache.Read(fingerprint);
        if (kept != null)
        {
            Publish(GameCheckResult.FromRecord(kept, verified, mismatch), null);
            return true;
        }

        _running = true;
        string managed = Path.Combine(Application.dataPath, "Managed");
        string game = Loader.GameBuild;
        Task.Run(() => GameCheckRun.Run(managed, targets, game, verified, mismatch))
            .ContinueWith(task => MainThread.Post(() => Publish(task.IsFaulted
                ? GameCheckResult.Failed(game, verified, mismatch, task.Exception?.GetBaseException().Message)
                : task.Result, fingerprint)));
        return true;
    }

    // Main thread: which files belong to which loaded mod. The worker only reads files.
    private static List<KeyValuePair<string, string>> Targets()
    {
        var targets = new List<KeyValuePair<string, string>>();
        foreach (var mod in ModRegistry.Mods)
        {
            if (mod.State != ModState.Loaded)
                continue;
            foreach (string dll in DllsOf(mod))
                targets.Add(new KeyValuePair<string, string>(mod.Id, dll));
        }
        return targets;
    }

    private static IEnumerable<string> DllsOf(ModInfo mod)
    {
        if (mod.Manifest.Assemblies.Count > 0)
        {
            foreach (string relative in mod.Manifest.Assemblies)
                yield return Path.Combine(mod.Directory, relative);
        }
        else if (Directory.Exists(mod.Directory))
        {
            foreach (string dll in Directory.GetFiles(mod.Directory, "*.dll", SearchOption.TopDirectoryOnly))
                yield return dll;
        }
    }

    // fingerprint is null for a result from the cache, which is kept already.
    private static void Publish(GameCheckResult result, string fingerprint)
    {
        _running = false;
        Last = result;
        if (fingerprint != null && result.Failure == null)
            GameCheckCache.Write(result.ToRecord(fingerprint));
        string when = result.FromCache ? $"unchanged since {result.CheckedAt:yyyy-MM-dd HH:mm}" : $"{result.Seconds:0.0} s";
        if (result.Failure != null)
            _log.Warn($"The game check couldn't run: {result.Failure}");
        else if (result.Problems.Count == 0)
            _log.Info($"Game check: all {result.Checked} hooks of {result.Mods} mods fit {result.Game} ({result.Dynamic} worked out at run time, {when}).");
        else
            Report(result);
        Finished.Raise(result, _log);
    }

    private static void Report(GameCheckResult result)
    {
        _log.Warn($"Game check: {result.Problems.Count} of {result.Checked} hooks don't fit {result.Game}. The features using them may not work.");
        var byMod = new Dictionary<string, List<string>>();
        for (int i = 0; i < result.Problems.Count; i++)
        {
            var problem = result.Problems[i];
            if (i < MaxLoggedProblems)
                _log.Warn($"  {problem}");
            if (!byMod.TryGetValue(problem.Mod, out var lines))
                byMod[problem.Mod] = lines = new List<string>();
            lines.Add(problem.ToString());
        }
        if (result.Problems.Count > MaxLoggedProblems)
            _log.Warn($"  ... and {result.Problems.Count - MaxLoggedProblems} more (game.check over the bridge lists them all)");
        foreach (var pair in byMod)
            ErrorReport.Add(pair.Key, $"{pair.Value.Count} game hook{(pair.Value.Count == 1 ? "" : "s")} don't fit {result.Game}", string.Join("\n", pair.Value));
    }
}
