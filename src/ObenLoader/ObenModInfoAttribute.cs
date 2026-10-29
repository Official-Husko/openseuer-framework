// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
using System;

namespace ObenLoader;

/// <summary>
/// Declares a mod in code instead of a mod.json: put it on the mod's <see cref="ObenMod"/> class and ship the
/// DLL in a folder of its own under Mods. The loader reads it without loading the DLL. A mod.json in the
/// folder wins over it. Fields mod.json has and this doesn't keep their defaults.
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public sealed class ObenModInfoAttribute : Attribute
{
    public ObenModInfoAttribute(string id, string name = null, string version = "0.0.0")
    {
        Id = id;
        Name = name;
        Version = version;
    }

    public string Id { get; }
    public string Name { get; }
    public string Version { get; }

    public string Author { get; set; }
    public string Description { get; set; }

    /// <summary>The game versions the mod was made for, as mod.json's gameVersion.</summary>
    public string GameVersion { get; set; }

    /// <summary>As mod.json's hotReload.</summary>
    public bool HotReload { get; set; }
}

/// <summary>Another mod this one needs, with the lowest version that works ("*" for any). Repeat it for each.</summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = false)]
public sealed class ObenModDependencyAttribute : Attribute
{
    public ObenModDependencyAttribute(string id, string minVersion = "*")
    {
        Id = id;
        MinVersion = minVersion;
    }

    public string Id { get; }
    public string MinVersion { get; }
}
