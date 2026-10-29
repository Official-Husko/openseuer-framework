using System.Collections.Generic;
using System.Reflection;

namespace ObenLoader.Mods;

/// <summary>Everything the loader knows about one mod.</summary>
public sealed class ModInfo
{
    public ModManifest Manifest { get; internal set; }

    public string Directory { get; }

    /// <summary>The mod says it works with every game version (anyGameVersion, or a gameVersion of "*").</summary>
    public bool AnyGameVersion => ModChecks.IsAnyGameVersion(Manifest);
    public ModState State { get; internal set; } = ModState.Pending;
    public string Error { get; internal set; }

    /// <summary>A problem that didn't stop the mod from loading (it was made for another game version), or null.</summary>
    public string Warning { get; internal set; }

    /// <summary>The mod's entry object, if it has an <see cref="ObenMod"/> subclass.</summary>
    public ObenMod Instance { get; internal set; }

    /// <summary>Assemblies from the current load. Old copies from earlier hot reloads are not listed.</summary>
    public IReadOnlyList<Assembly> Assemblies => _assemblies;
    internal readonly List<Assembly> _assemblies = new List<Assembly>();

    /// <summary>How many times this mod has been loaded this session (1 on first load).</summary>
    public int LoadCount { get; internal set; }

    public bool CanHotReload => Manifest.HotReload;

    /// <summary>Newest DLL write time when the mod was last loaded. Used to detect rebuilds.</summary>
    internal System.DateTime LoadedWriteTime { get; set; }

    public string Id => Manifest.Id;

    internal ModInfo(ModManifest manifest, string directory)
    {
        Manifest = manifest;
        Directory = directory;
    }

    internal void Fail(string error)
    {
        State = ModState.Failed;
        Error = error;
    }

    public override string ToString() => $"{Manifest.DisplayName} {Manifest.Version}";
}
