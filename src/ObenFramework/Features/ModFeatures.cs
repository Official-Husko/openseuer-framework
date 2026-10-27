using System;
using System.Collections.Generic;
using System.IO;
using ObenLoader;
using ObenLoader.Config;
using ObenLoader.Logging;
using ObenLoader.Mods;
using ObenLoader.Runtime;

namespace ObenFramework.Features;

/// <summary>
/// Every feature of every loaded mod, with the player's on/off choices saved in
/// Mods/ObenFramework/features.json (keyed "modId/featureId"). Something else can decide for the
/// player for a while (a co-op host, say): <see cref="Override"/> switches a feature without
/// touching the player's choice, and <see cref="ClearOverrides"/> gives the choices back.
/// </summary>
public static class ModFeatures
{
    private static readonly List<ModFeature> All = new List<ModFeature>();
    private static readonly List<ModFeature> Updating = new List<ModFeature>();
    private static Dictionary<string, bool> _choices = new Dictionary<string, bool>();
    private static readonly Dictionary<string, bool> Overrides = new Dictionary<string, bool>();
    private static string _path;
    private static ModLogger _log;

    public static IReadOnlyList<ModFeature> Features => All;

    /// <summary>After a feature switched on or off, for any reason (a choice, an override, its mod loading).</summary>
    public static event Action<ModFeature> Changed;

    /// <summary>A feature by its "modId/featureId" key, or null.</summary>
    public static ModFeature Find(string key) => All.Find(f => string.Equals(Key(f), key, StringComparison.OrdinalIgnoreCase));

    /// <summary>Features of one mod, in registration order.</summary>
    public static List<ModFeature> Of(ModInfo mod) => All.FindAll(f => f.Owner.Info == mod);

    /// <summary>Adds a feature and switches it on if the player (or its default) wants it on.</summary>
    public static void Register(ObenMod owner, ModFeature feature)
    {
        feature.Owner = owner;
        All.Add(feature);
        if (feature.OverridesUpdate)
            Updating.Add(feature);

        owner.Track(Disposable.Create(() =>
        {
            if (feature.Enabled)
                Run(feature, feature.Disable, "switching off");
            All.Remove(feature);
            Updating.Remove(feature);
        }));

        if (IsWanted(feature))
            Run(feature, feature.Enable, "switching on");
    }

    public static bool IsWanted(ModFeature feature)
    {
        string key = Key(feature);
        if (Overrides.TryGetValue(key, out bool decided))
            return decided;
        return _choices.TryGetValue(key, out bool on) ? on : feature.EnabledByDefault;
    }

    /// <summary>"modId/featureId", the key choices and overrides use.</summary>
    public static string KeyOf(ModFeature feature) => Key(feature);

    public static bool IsOverridden(ModFeature feature) => Overrides.ContainsKey(Key(feature));

    /// <summary>Switches a feature (by key, loaded or not) the way another game decided, without saving it.</summary>
    public static void Override(string key, bool on)
    {
        if (Overrides.TryGetValue(key, out bool was) && was == on)
            return;
        Overrides[key] = on;
        var feature = All.Find(f => Key(f) == key);
        if (feature != null)
            Apply(feature);
    }

    /// <summary>Drops every override; features go back to the player's own choices.</summary>
    public static void ClearOverrides()
    {
        if (Overrides.Count == 0)
            return;
        Overrides.Clear();
        foreach (var feature in All.ToArray())
            Apply(feature);
    }

    /// <summary>
    /// Remembers the player's choice and switches the feature now, unless another game decides it
    /// for the moment (then the choice counts once that ends).
    /// </summary>
    public static void SetEnabled(ModFeature feature, bool on)
    {
        _choices[Key(feature)] = on;
        Save();
        Apply(feature);
    }

    private static void Apply(ModFeature feature)
    {
        bool on = IsWanted(feature);
        if (on && !feature.Enabled)
            Run(feature, feature.Enable, "switching on");
        else if (!on && feature.Enabled)
            Run(feature, feature.Disable, "switching off");
    }

    internal static void Init(ObenMod framework)
    {
        _log = framework.Log;
        _path = framework.PathTo("features.json");
        _choices = File.Exists(_path) ? JsonFile.Read<Dictionary<string, bool>>(_path) : new Dictionary<string, bool>();
    }

    internal static void Update()
    {
        if (Updating.Count == 0)
            return;
        for (int i = 0; i < Updating.Count; i++)
        {
            var feature = Updating[i];
            if (!feature.Enabled)
                continue;
            try
            {
                feature.OnUpdate();
            }
            catch (Exception e)
            {
                _log.Error($"Feature {Key(feature)} failed and was switched off", e);
                Run(feature, feature.Disable, "switching off");
            }
        }
    }

    private static void Run(ModFeature feature, Action action, string what)
    {
        try
        {
            action();
        }
        catch (Exception e)
        {
            _log.Error($"{what} {Key(feature)} failed", e);
            if (feature.Enabled)
            {
                try { feature.Disable(); } catch { }
            }
        }
        Changed?.Invoke(feature);
    }

    private static string Key(ModFeature f) => f.Owner.Info.Id + "/" + f.Id;

    private static void Save()
    {
        try { JsonFile.Write(_path, _choices); }
        catch (Exception e) { _log.Error("Couldn't save feature choices", e); }
    }
}
