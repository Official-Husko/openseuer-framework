using System;
using System.Collections.Generic;
using System.IO;
using ObenLoader.Config;
using ObenLoader.Logging;
using ObenLoader.Runtime;

namespace ObenLoader.Mods;

/// <summary>
/// Every mod the loader knows about, the player's on/off switches, and runtime control
/// (load, unload, reload). Runtime operations are queued and run between frames; pass a callback
/// to learn the outcome (null means success, otherwise the reason it failed).
/// </summary>
public static partial class ModRegistry
{
    private static readonly List<ModInfo> All = new List<ModInfo>();
    private static readonly Dictionary<string, ModInfo> ById = new Dictionary<string, ModInfo>(StringComparer.OrdinalIgnoreCase);
    private static ConfigFile<LoaderSettings> _settings;
    private static ModLogger _log;

    public static IReadOnlyList<ModInfo> Mods => All;

    /// <summary>Raised after a mod (re)loads.</summary>
    public static event Action<ModInfo> ModLoaded
    {
        add => ModLifecycle.Loaded += value;
        remove => ModLifecycle.Loaded -= value;
    }

    /// <summary>
    /// Raised just before a mod unloads or reloads. Registries use it to drop whatever that mod
    /// registered, so cleanup happens even if the mod forgets.
    /// </summary>
    public static event Action<ModInfo> ModUnloading
    {
        add => ModLifecycle.Unloading += value;
        remove => ModLifecycle.Unloading -= value;
    }

    public static ModInfo Find(string id) => id != null && ById.TryGetValue(id, out var mod) ? mod : null;

    public static bool IsLoaded(string id) => Find(id)?.State == ModState.Loaded;

    /// <summary>Gets a loaded mod's entry object, e.g. to call into another mod's API.</summary>
    public static T Get<T>() where T : ObenMod
    {
        foreach (var mod in All)
        {
            if (mod.Instance is T match)
                return match;
        }
        return null;
    }

    /// <summary>Finds the loaded mod that owns a type (useful for owner-tracked registrations).</summary>
    public static ModInfo OwnerOf(Type type)
    {
        var assembly = type.Assembly;
        foreach (var mod in All)
        {
            if (mod.State == ModState.Loaded && mod._assemblies.Contains(assembly))
                return mod;
        }
        return null;
    }

    public static void Unload(string id, Action<string> done = null) => Queue(id, done, ModLifecycle.Unload);

    public static void Load(string id, Action<string> done = null) => Queue(id, done, ModLifecycle.Load);

    /// <summary>Looks for mod folders added since startup. New mods are listed but not loaded.</summary>
    public static IReadOnlyList<ModInfo> Rescan()
    {
        var added = new List<ModInfo>();
        foreach (var mod in ModDiscovery.Scan(LoaderPaths.ModsDir, _log))
        {
            if (ById.ContainsKey(mod.Id))
                continue;
            mod.State = ModState.Disabled;
            Add(mod);
            added.Add(mod);
            _log.Info($"Found new mod {mod}");
        }
        return added;
    }

    internal static LoaderSettings Settings => _settings.Value;

    internal static void Init(ModLogger log)
    {
        _log = log;
        _settings = new ConfigFile<LoaderSettings>(Path.Combine(LoaderPaths.DataDir, "settings.json"), log);
        Watcher.Enabled = _settings.Value.AutoReload;
    }

    internal static void Add(ModInfo mod)
    {
        All.Add(mod);
        ById[mod.Id] = mod;
        foreach (string dll in ModDiscovery.AssemblyPaths(mod))
            AssemblyResolver.RegisterPath(dll);
        foreach (string dll in ModDiscovery.LibraryPaths(mod))
            AssemblyResolver.RegisterPath(dll);
    }

    private static void Queue(string id, Action<string> done, Func<ModInfo, string> operation)
    {
        MainThread.Post(() =>
        {
            var mod = Find(id);
            string error;
            try
            {
                error = mod == null ? $"No mod with id '{id}'." : operation(mod);
            }
            catch (Exception e)
            {
                error = e.Message;
                _log.Error($"Operation on {id} failed", e);
            }
            if (error != null)
                _log.Warn(error);
            done?.Invoke(error);
        });
    }
}
