using System.Diagnostics;

namespace ObenTool.Changelog;

/// <summary>Runs git and returns its output.</summary>
public static class Git
{
    public static string Run(string workDir, params string[] args)
    {
        var psi = new ProcessStartInfo("git")
        {
            WorkingDirectory = workDir,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        foreach (string a in args)
            psi.ArgumentList.Add(a);

        using var process = Process.Start(psi) ?? throw new InvalidOperationException("Couldn't start git.");
        // Read both pipes at once so a chatty one can't fill up and stall the other.
        var error = process.StandardError.ReadToEndAsync();
        string output = process.StandardOutput.ReadToEnd();
        process.WaitForExit();
        if (process.ExitCode != 0)
            throw new InvalidOperationException($"git {string.Join(' ', args)} failed: {error.Result.Trim()}");
        return output;
    }

    public static string RepoRoot(string from) => Run(from, "rev-parse", "--show-toplevel").Trim();
}
