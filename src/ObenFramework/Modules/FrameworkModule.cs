using ObenFramework.Patching;

namespace ObenFramework.Modules;

/// <summary>One part of the framework (shops, NPCs, the clock...) and whether it came up.</summary>
public sealed class FrameworkModule
{
    internal FrameworkModule(string name, string patchArea)
    {
        Name = name;
        PatchArea = patchArea;
    }

    /// <summary>Short lowercase name, such as "shops".</summary>
    public string Name { get; }

    /// <summary>The patch area its Harmony patches are in (see <see cref="PatchAreas"/>).</summary>
    public string PatchArea { get; }

    /// <summary>Its setup ran without throwing.</summary>
    public bool Started { get; internal set; }

    /// <summary>Why its setup failed, or null.</summary>
    public string Error { get; internal set; }

    /// <summary>How long its setup took, in milliseconds.</summary>
    public double StartMs { get; internal set; }

    /// <summary>The last error its per-frame update threw, or null. The update keeps running.</summary>
    public string UpdateError { get; internal set; }

    /// <summary>Per-frame work, run while it is started.</summary>
    internal System.Action Tick;

    /// <summary>Its patches stand (none of them failed).</summary>
    public bool PatchesOk => PatchAreas.IsAvailable(PatchAreas.Framework, PatchArea);

    /// <summary>Set up and none of its patches failed: what it offers works.</summary>
    public bool IsAvailable => Started && PatchesOk;

    public override string ToString() =>
        IsAvailable ? Name : Name + " (" + (Error ?? "patches off") + ")";
}
