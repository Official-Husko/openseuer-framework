#nullable enable
namespace Oben.Reports;

/// <summary>
/// What goes into a bug report beyond the part that is always there (the game, ObenseuerExtended and mod versions,
/// the operating system and the logs). Everything about the PC itself is the player's choice.
/// </summary>
public sealed class ReportOptions
{
    /// <summary>The processor's name, cores and speed.</summary>
    public bool Cpu;

    /// <summary>The graphics card, its memory, the graphics API and the screen mode.</summary>
    public bool Gpu;

    /// <summary>How much memory the PC has.</summary>
    public bool Ram;

    /// <summary>The save being played (or the newest one at the main menu), every file of it.</summary>
    public bool Save;

    /// <summary>
    /// Unity's record of the last crash, when it is from the last week: its crash.dmp and the Player.log it kept. On
    /// unless the player turns it off. The dump is binary, so names in it can't be hidden.
    /// </summary>
    public bool Crash = true;

    /// <summary>
    /// Replaces user names in folder paths, Steam ids and IP addresses with placeholders in every text file of the
    /// report. On unless the player turns it off.
    /// </summary>
    public bool HidePersonal = true;

    /// <summary>Everything ticked, personal info still hidden.</summary>
    public static ReportOptions All() => new ReportOptions { Cpu = true, Gpu = true, Ram = true, Save = true };
}
