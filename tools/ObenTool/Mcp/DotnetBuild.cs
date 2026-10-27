using System.Diagnostics;
using ObenTool.Game;

namespace ObenTool.Mcp;

/// <summary>Runs dotnet build from the repo root and keeps the useful part of the output.</summary>
public static class DotnetBuild
{
    public static async Task<(int Code, string Output)> RunAsync(string? project, CancellationToken ct)
    {
        string root = RepoRoot() ?? throw new InvalidOperationException("Run the tool from inside the repo to build.");
        var psi = new ProcessStartInfo("dotnet")
        {
            WorkingDirectory = root,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        psi.ArgumentList.Add("build");
        if (project != null)
            psi.ArgumentList.Add(project);
        psi.ArgumentList.Add("-c");
        psi.ArgumentList.Add("Release");
        psi.ArgumentList.Add("-nologo");
        psi.ArgumentList.Add("-v:q");

        using var process = Process.Start(psi)!;
        string stdout = await process.StandardOutput.ReadToEndAsync(ct);
        string stderr = await process.StandardError.ReadToEndAsync(ct);
        await process.WaitForExitAsync(ct);

        var useful = (stdout + stderr).Split('\n')
            .Where(l => l.Contains("error") || l.Contains("warn") || l.Contains("->") || l.Contains("Build succeeded"))
            .Select(l => l.TrimEnd());
        return (process.ExitCode, string.Join('\n', useful));
    }

    /// <summary>The folder with ObenseuerExtended.slnx above the current directory or the tool.</summary>
    public static string? RepoRoot()
    {
        foreach (string start in new[] { Environment.CurrentDirectory, AppContext.BaseDirectory })
        {
            for (var dir = new DirectoryInfo(start); dir != null; dir = dir.Parent)
            {
                if (File.Exists(Path.Combine(dir.FullName, "ObenseuerExtended.slnx")))
                    return dir.FullName;
            }
        }
        return null;
    }
}
