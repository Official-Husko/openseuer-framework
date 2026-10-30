#nullable enable
using System.Collections.Generic;

namespace Oben.Reports;

/// <summary>
/// What a bug report says about the player's machine and install; the content of system.json. The game writes a
/// fresh one to ObenLoader/system.json at every start, so a report can also be put together from outside while the
/// game won't run (oben report, the companion app). Unknown values stay null or 0. Other programs read this file:
/// add fields, never rename or remove them.
/// </summary>
public sealed class SystemSnapshot
{
    public const int CurrentFormat = 1;

    public int Format = CurrentFormat;

    /// <summary>Local time it was taken, "2026-10-30T01:20:00".</summary>
    public string? Written;

    /// <summary>ObenseuerExtended as the pause menu shows it: "0.1.0 (a185d27)".</summary>
    public string? Build;

    public string? Loader;
    public string? Game;

    /// <summary>The Steam build id, when the game came from Steam.</summary>
    public string? GameBuild;

    /// <summary>SHA-256 of the game's code (Assembly-CSharp.dll), which tells builds from any store apart.</summary>
    public string? GameCodeHash;

    /// <summary>SHA-256 of the game's exe.</summary>
    public string? GameExeHash;

    public string? Unity;

    /// <summary>The system as the game sees it: "Windows 10 (10.0.19045) 64bit", also under Proton.</summary>
    public string? Os;

    /// <summary>The Wine version when the game runs through Wine or Proton; null on Windows.</summary>
    public string? Wine;

    /// <summary>The real system under Wine: "Linux 6.8.0-45-generic".</summary>
    public string? Host;

    public string? Language;

    public string? Cpu;
    public int CpuCores;
    public int CpuMhz;

    public string? Gpu;
    public string? GpuVendor;

    /// <summary>The graphics API and driver as Unity reports them: "Direct3D 11.0 [level 11.1]".</summary>
    public string? GpuApi;

    public int GpuMemoryMb;
    public int RamMb;

    /// <summary>"2560x1440, borderless fullscreen, 144 Hz".</summary>
    public string? Display;

    public List<ModEntry> Mods = new List<ModEntry>();

    /// <summary>BepInEx in the game folder, null when it isn't there.</summary>
    public BepInExInfo? BepInEx;

    /// <summary>The operating system in one line: "Windows 10 (10.0.19045) 64bit through Wine 9.0 on Linux 6.8.0".</summary>
    public string OsLine()
    {
        string os = Os ?? "unknown system";
        if (Wine == null)
            return os;
        return os + " through Wine " + Wine + (Host == null ? "" : " on " + Host);
    }

    /// <summary>A copy with only the parts the options ask for: the processor, graphics and memory are left out unless ticked.</summary>
    public SystemSnapshot For(ReportOptions options)
    {
        var copy = (SystemSnapshot)MemberwiseClone();
        copy.Mods = new List<ModEntry>(Mods);
        if (!options.Cpu)
        {
            copy.Cpu = null;
            copy.CpuCores = 0;
            copy.CpuMhz = 0;
        }
        if (!options.Gpu)
        {
            copy.Gpu = null;
            copy.GpuVendor = null;
            copy.GpuApi = null;
            copy.GpuMemoryMb = 0;
            copy.Display = null;
        }
        if (!options.Ram)
            copy.RamMb = 0;
        return copy;
    }
}

/// <summary>One installed mod in a <see cref="SystemSnapshot"/>.</summary>
public sealed class ModEntry
{
    public string? Id;
    public string? Name;
    public string? Version;

    /// <summary>"Loaded", "Disabled", "Failed" and so on.</summary>
    public string? State;
}
