namespace ObenTool.Game;

/// <summary>
/// How to start the game on this machine. On Linux that's umu-launcher with a Proton build and a
/// Wine prefix, taken from Heroic's settings for the game unless OBEN_UMU, OBEN_PROTON or
/// OBEN_PREFIX say otherwise. On Windows the exe is started directly.
/// </summary>
public sealed record LaunchProfile(string? UmuRun, string? ProtonDir, string? Prefix, string Source)
{
    public bool Native => OperatingSystem.IsWindows();

    public static LaunchProfile Resolve(GameInstall game)
    {
        if (OperatingSystem.IsWindows())
            return new LaunchProfile(null, null, null, "native");

        var heroicApp = Heroic.SideloadedApps().FirstOrDefault(a =>
            string.Equals(Path.GetFullPath(a.Executable), game.Exe, StringComparison.Ordinal));
        var heroic = heroicApp is null ? null : Heroic.SetupFor(heroicApp.AppName);

        string? umu = Env("OBEN_UMU") ?? Heroic.UmuRun() ?? OnPath("umu-run");
        string? proton = Env("OBEN_PROTON") ?? heroic?.ProtonBin;
        string? prefix = Env("OBEN_PREFIX") ?? heroic?.Prefix;

        // Heroic stores the proton script; umu wants the folder it's in.
        if (proton != null && File.Exists(proton))
            proton = Path.GetDirectoryName(proton);

        string source = heroicApp != null ? $"Heroic ({heroicApp.Title})" : "environment";
        return new LaunchProfile(umu, proton, prefix, source);
    }

    /// <summary>Why the game can't be started this way, or null when it can.</summary>
    public string? Problem()
    {
        if (Native)
            return null;
        if (UmuRun is null)
            return "No umu-run found. Install umu-launcher, let Heroic install its copy, or set OBEN_UMU.";
        if (ProtonDir is null || !Directory.Exists(ProtonDir))
            return "No Proton build found. Set one for the game in Heroic, or set OBEN_PROTON.";
        if (Prefix is null)
            return "No Wine prefix found. Set one for the game in Heroic, or set OBEN_PREFIX.";
        return null;
    }

    public override string ToString() => Native
        ? "native"
        : $"umu-run {UmuRun}\n  Proton {ProtonDir}\n  prefix {Prefix}\n  from {Source}";

    private static string? Env(string name)
    {
        string? value = Environment.GetEnvironmentVariable(name);
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }

    private static string? OnPath(string exe) =>
        (Environment.GetEnvironmentVariable("PATH") ?? "")
            .Split(Path.PathSeparator)
            .Select(dir => Path.Combine(dir, exe))
            .FirstOrDefault(File.Exists);
}
