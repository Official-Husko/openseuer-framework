using System.Diagnostics;

namespace ObenTool.Game;

/// <summary>
/// Finds running copies of the game. Under Wine the process's name is the exe's file name. Which
/// folder it runs from comes from its working directory, since Proton may map the game's drive to
/// any letter (S:, Z:, ...); the copy is told apart by OBEN_INSTANCE.
/// </summary>
internal static class GameProcesses
{
    private const string ProcessName = "Obenseuer";
    private const string InstanceVariable = "OBEN_INSTANCE=";

    // With exe, only processes started from that Obenseuer.exe count.
    public static IEnumerable<int> Find(int? instance, string? exe = null)
    {
        // Windows can't tell instances apart from outside the process, so the instance filter is ignored there.
        if (OperatingSystem.IsWindows())
            return Process.GetProcessesByName(ProcessName).Where(p => exe is null || RunsFrom(p, exe)).Select(p => p.Id);

        var pids = new List<int>();
        foreach (string dir in Directory.EnumerateDirectories("/proc"))
        {
            if (!int.TryParse(Path.GetFileName(dir), out int pid))
                continue;
            try
            {
                if (File.ReadAllText(Path.Combine(dir, "comm")).Trim() != ProcessName + ".exe")
                    continue;
                if ((instance is null || InstanceOf(dir) == instance) && (exe is null || RunsIn(dir, exe)))
                    pids.Add(pid);
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }
        return pids;
    }

    /// <summary>The OBEN_INSTANCE of a process, 1 when it has none, null when it can't be read.</summary>
    private static int? InstanceOf(string procDir)
    {
        try
        {
            foreach (string entry in File.ReadAllText(Path.Combine(procDir, "environ")).Split('\0'))
            {
                if (entry.StartsWith(InstanceVariable, StringComparison.Ordinal))
                    return int.TryParse(entry.AsSpan(InstanceVariable.Length), out int n) && n > 1 ? n : 1;
            }
            return 1;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    // Wine starts the game with its own folder as the working directory. One that can't be read
    // counts as a match, so nothing gets changed under a running game.
    private static bool RunsIn(string procDir, string exe)
    {
        try
        {
            string? cwd = new DirectoryInfo(Path.Combine(procDir, "cwd")).LinkTarget;
            return cwd is null || string.Equals(cwd.TrimEnd('/'), Path.GetDirectoryName(exe), StringComparison.Ordinal);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return true;
        }
    }

    // A process whose path can't be read counts as a match, so nothing gets changed under a running game.
    private static bool RunsFrom(Process process, string exe)
    {
        try
        {
            string? path = process.MainModule?.FileName;
            return path is null || string.Equals(Path.GetFullPath(path), exe, StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception e) when (e is System.ComponentModel.Win32Exception or InvalidOperationException or NotSupportedException)
        {
            return true;
        }
    }
}
