// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using ObenFramework.Patching;
using ObenLoader;

namespace ObenFramework.Modules;

/// <summary>
/// The parts of the framework and whether each came up. Every part starts on its own: one whose setup
/// throws is logged (and shows on the Errors page) while the others still start, and one whose patches
/// failed reports <see cref="FrameworkModule.IsAvailable"/> false. Mods can check a part before relying
/// on it, e.g. <c>FrameworkModules.IsAvailable("shops")</c>.
/// </summary>
public static class FrameworkModules
{
    private static readonly List<FrameworkModule> Modules = new List<FrameworkModule>();

    /// <summary>Every part in start order.</summary>
    public static IReadOnlyList<FrameworkModule> All => Modules;

    /// <summary>The part with this name (case-insensitive), or null.</summary>
    public static FrameworkModule Find(string name)
    {
        foreach (var module in Modules)
        {
            if (string.Equals(module.Name, name, StringComparison.OrdinalIgnoreCase))
                return module;
        }
        return null;
    }

    /// <summary>True when the part exists, started and its patches stand.</summary>
    public static bool IsAvailable(string name)
    {
        var module = Find(name);
        return module != null && module.IsAvailable;
    }

    /// <summary>
    /// Runs one part's setup, catching anything it throws so the next part still starts. The part's patch
    /// area comes from the class that declares <paramref name="init"/> (its [PatchArea] or namespace).
    /// </summary>
    internal static FrameworkModule Start(ObenMod framework, string name, Action<ObenMod> init, Action update = null)
    {
        _log = framework.Log;
        string area = PatchAreas.AreaOf(init.Method.DeclaringType, typeof(FrameworkMod).Namespace);
        var module = new FrameworkModule(name, area) { Tick = update };
        Modules.Add(module);

        var clock = Stopwatch.StartNew();
        try
        {
            init(framework);
            module.Started = true;
        }
        catch (Exception e)
        {
            module.Error = e.GetBaseException().Message;
            framework.Log.Error($"Framework part '{name}' failed to start; the other parts still run, but what it offers may not work", e);
        }
        module.StartMs = clock.Elapsed.TotalMilliseconds;
        if (module.Started && !module.PatchesOk)
            framework.Log.Warn($"Framework part '{name}' started, but its '{area}' patches are switched off");
        return module;
    }

    /// <summary>
    /// Runs every started part's per-frame work, each in its own try/catch so one throwing can't keep the
    /// others from running. A failure is logged once per part (every one still counts in errors.log).
    /// </summary>
    internal static void Update()
    {
        for (int i = 0; i < Modules.Count; i++)
        {
            var module = Modules[i];
            if (module.Tick == null || !module.Started)
                continue;
            try
            {
                module.Tick();
            }
            catch (Exception e)
            {
                module.UpdateError = e.GetBaseException().Message;
                _log?.ErrorOnce("update:" + module.Name, $"Framework part '{module.Name}' failed in its update (repeats are not logged)", e);
            }
        }
    }

    /// <summary>"Ready." or what didn't come up, for the log.</summary>
    internal static string Summary()
    {
        var failed = new List<string>();
        foreach (var module in Modules)
        {
            if (!module.IsAvailable)
                failed.Add(module.ToString());
        }
        return failed.Count == 0
            ? $"Ready: {Modules.Count} parts."
            : $"Ready, but {failed.Count} of {Modules.Count} parts aren't fully working: {string.Join(", ", failed)}.";
    }

    /// <summary>Forgets the parts; the framework is unloading.</summary>
    internal static void Clear()
    {
        Modules.Clear();
        _log = null;
    }

    private static ObenLoader.Logging.ModLogger _log;
}
