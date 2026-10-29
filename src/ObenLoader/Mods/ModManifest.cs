using System.Collections.Generic;
using Newtonsoft.Json;

namespace ObenLoader.Mods;

/// <summary>Contents of a mod's mod.json.</summary>
public sealed class ModManifest
{
    [JsonProperty("id")] public string Id;
    [JsonProperty("name")] public string Name;
    [JsonProperty("version")] public string Version = "0.0.0";
    [JsonProperty("author")] public string Author;
    [JsonProperty("description")] public string Description;

    /// <summary>DLLs to load, relative to the mod folder. Empty means every *.dll in the folder root.</summary>
    [JsonProperty("assemblies")] public List<string> Assemblies = new List<string>();

    /// <summary>Required mods: id -> minimum version ("*" for any).</summary>
    [JsonProperty("dependencies")] public Dictionary<string, string> Dependencies = new Dictionary<string, string>();

    /// <summary>Mods that should load first if present, but aren't required.</summary>
    [JsonProperty("loadAfter")] public List<string> LoadAfter = new List<string>();

    /// <summary>Apply every [HarmonyPatch] class in the mod's assemblies automatically.</summary>
    [JsonProperty("autoPatch")] public bool AutoPatch = true;

    /// <summary>
    /// Load the DLLs from memory and watch them for changes, so a rebuild reloads the mod
    /// without restarting the game. The mod has to clean up after itself in OnUnload.
    /// </summary>
    [JsonProperty("hotReload")] public bool HotReload;

    /// <summary>
    /// Always on: can't be switched off or unloaded (reloading still works). For the framework
    /// everything else needs. Mods it depends on are kept on too.
    /// </summary>
    [JsonProperty("essential")] public bool Essential;

    /// <summary>
    /// Off: installed but doesn't load until the player switches it on. For mods that change the
    /// game a lot, or tools only developers want.
    /// </summary>
    [JsonProperty("enabledByDefault")] public bool EnabledByDefault = true;

    /// <summary>
    /// The game versions the mod was made for, such as ">=0.5.13 &lt;0.6" or "0.5.x" (see
    /// <see cref="VersionRange"/>). Empty means any. On another version the mod still loads with a
    /// warning, unless <see cref="StrictGameVersion"/> is set.
    /// </summary>
    [JsonProperty("gameVersion")] public string GameVersion;

    /// <summary>Refuse to load on a game version outside <see cref="GameVersion"/> instead of only warning.</summary>
    [JsonProperty("strictGameVersion")] public bool StrictGameVersion;

    /// <summary>
    /// Steam build ids the mod was made for (the "buildid" in Steam's app manifest). Checked like
    /// <see cref="GameVersion"/>, only when the game was installed through Steam and the build is known.
    /// </summary>
    [JsonProperty("gameBuilds")] public List<string> GameBuilds = new List<string>();

    public string DisplayName => string.IsNullOrEmpty(Name) ? Id : Name;
}
