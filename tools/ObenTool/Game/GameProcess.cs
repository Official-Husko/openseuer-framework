using System.Diagnostics;

namespace ObenTool.Game;

/// <summary>Starting, finding and stopping the game.</summary>
public static class GameProcess
{
    public static readonly string[] WindowedArgs =
        { "-screen-fullscreen", "0", "-screen-width", "1600", "-screen-height", "900" };

    /// <summary>Is the game running? With an instance, only that copy counts (1 is the normal one).</summary>
    public static bool IsRunning(int? instance = null) => GameProcesses.Find(instance).Any();

    /// <summary>Is the game in this folder running? A copy elsewhere doesn't count.</summary>
    public static bool IsRunning(GameInstall game) => GameProcesses.Find(null, game.Exe).Any();

    /// <summary>
    /// Starts one copy of the game and returns a short description of how. Extra instances write
    /// their own Unity log so two processes never share Player.log.
    /// </summary>
    public static string Launch(GameInstall game, int instance, IEnumerable<string> gameArgs)
    {
        var args = gameArgs.ToList();
        var profile = LaunchProfile.Resolve(game);
        if (profile.Problem() is { } problem)
            throw new InvalidOperationException(problem);

        if (instance > 1)
        {
            Directory.CreateDirectory(game.LogsDir);
            args.Add("-logFile");
            args.Add(profile.Native ? game.PlayerLogFile(instance) : "Z:" + game.PlayerLogFile(instance).Replace('/', '\\'));
        }

        var psi = profile.Native ? Native(game) : ThroughUmu(game, profile);
        foreach (string a in args)
            psi.ArgumentList.Add(a);
        if (instance > 1)
        {
            psi.Environment["OBEN_INSTANCE"] = instance.ToString();
            // Proton's default verb waits for whatever already runs in the prefix to exit first,
            // so a second copy in the same prefix would hang until the first one closes.
            psi.Environment["PROTON_VERB"] = "run";
        }

        // umu writes a lot; keep it in a file next to the loader logs instead of the terminal.
        Directory.CreateDirectory(game.LogsDir);
        string runnerLog = Path.Combine(game.LogsDir, "runner" + GameInstall.Suffix(instance) + ".log");
        StartLogged(psi, runnerLog);

        return profile.Native
            ? "Started the game"
            : $"Started through umu with {Path.GetFileName(profile.ProtonDir)} (runner output in {runnerLog})";
    }

    /// <summary>Kills the game process (one instance, or all of them). Prefer asking the game to quit through the bridge first.</summary>
    public static int Kill(int? instance = null)
    {
        int killed = 0;
        foreach (int pid in GameProcesses.Find(instance))
        {
            try
            {
                Process.GetProcessById(pid).Kill();
                killed++;
            }
            catch (ArgumentException)
            {
                // Already gone.
            }
        }
        return killed;
    }

    private static ProcessStartInfo Native(GameInstall game) => new(game.Exe)
    {
        WorkingDirectory = game.Dir,
        UseShellExecute = false,
        RedirectStandardOutput = true,
        RedirectStandardError = true,
    };

    private static ProcessStartInfo ThroughUmu(GameInstall game, LaunchProfile profile)
    {
        var psi = new ProcessStartInfo(profile.UmuRun!)
        {
            WorkingDirectory = game.Dir,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        psi.Environment["WINEPREFIX"] = profile.Prefix;
        psi.Environment["PROTONPATH"] = profile.ProtonDir;
        psi.Environment["GAMEID"] = "umu-default";
        psi.ArgumentList.Add(game.Exe);
        return psi;
    }

    private static void StartLogged(ProcessStartInfo psi, string logPath)
    {
        var process = Process.Start(psi) ?? throw new InvalidOperationException("The game didn't start.");
        var log = new StreamWriter(logPath, append: false) { AutoFlush = true };
        var gate = new object();
        void Write(string? line)
        {
            if (line is null)
                return;
            lock (gate)
                log.WriteLine(line);
        }
        process.OutputDataReceived += (_, e) => Write(e.Data);
        process.ErrorDataReceived += (_, e) => Write(e.Data);
        process.Exited += (_, _) => { lock (gate) log.Dispose(); };
        process.EnableRaisingEvents = true;
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
    }
}
