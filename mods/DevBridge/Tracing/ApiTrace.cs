// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
using System.Collections.Generic;
using System.Reflection;
using ObenFramework.Events;
using ObenFramework.Features;
using ObenLoader;
using ObenLoader.Config;
using ObenLoader.Mods;
using ObenLoader.Runtime;
using UnityEngine;

namespace DevBridge.Tracing;

/// <summary>
/// The API call tracer: while this feature is on, every call into the public API of the chosen mods (the
/// framework by default) is recorded with its arguments, result or exception, time taken, nesting depth,
/// thread and the mod that made it, and scene loads, saves, loads and every patch that goes on or fails are
/// marked in between. With PatchHandlers on, the mods' patch handlers are traced too. trace.start,
/// trace.stop and trace.list drive and read it over the bridge. Off by default: patching the whole framework
/// takes a few seconds, and every traced call costs a little while it is on. Off, nothing is patched.
/// </summary>
internal sealed class ApiTrace : ModFeature
{
    public const string Key = "ApiTrace";
    private const double SweepSeconds = 1d;

    private readonly ConfigFile<TraceSettings> _settings;
    private double _nextSweep;
    private List<Assembly> _assemblies;

    public ApiTrace(ConfigFile<TraceSettings> settings) => _settings = settings;

    /// <summary>The registered feature, for the bridge commands.</summary>
    internal static ApiTrace Instance { get; set; }

    public override string Id => Key;
    public override string Name => "Framework API call tracer";
    public override string Description =>
        "Records calls to the framework's public API with arguments, results and timings, read with trace.list over the bridge. Slows the game while on.";
    public override bool EnabledByDefault => false;

    internal TraceSettings Settings => _settings.Value;

    internal void SaveSettings() => _settings.Save();

    /// <summary>Mods asked for in the settings that aren't loaded.</summary>
    internal List<string> Missing { get; } = new List<string>();

    /// <summary>Methods the filter picked, before the safety limit.</summary>
    internal int Selected { get; private set; }

    protected override void OnEnable()
    {
        var settings = _settings.Value;
        var assemblies = Assemblies(settings);
        _assemblies = assemblies;
        var methods = new List<MethodInfo>();
        foreach (var assembly in assemblies)
            methods.AddRange(TraceTargets.Select(TraceTargets.TypesOf(assembly), settings.Filter));
        if (settings.PatchHandlers)
            methods.AddRange(TraceTargets.Handlers(PatchEvents.InstalledHandlers(), assemblies, settings.Filter));
        Selected = methods.Count;
        if (methods.Count > settings.MaxMethods)
        {
            Owner.Log.Warn($"The trace filter picked {methods.Count} methods; only the first {settings.MaxMethods} are traced. Narrow the filter.");
            methods.RemoveRange(settings.MaxMethods, methods.Count - settings.MaxMethods);
        }

        TraceFormat.Describe = UnityValues.Describe;
        TraceHooks.Frame = Time.frameCount;
        var book = new TraceBook(settings.Capacity, settings.LinesPerWindow, settings.WindowSeconds);
        TraceHooks.Start(methods, assemblies, book, Owner.Log.Warn);
        string summary = $"tracing {TraceHooks.Patched} methods of {string.Join(", ", settings.Mods)}"
            + (settings.PatchHandlers ? " with their patch handlers" : "")
            + (string.IsNullOrWhiteSpace(settings.Filter) ? "" : $" matching '{settings.Filter}'")
            + $", patched in {TraceHooks.PatchMs} ms, {TraceHooks.Refused.Count} refused";
        book.AddMark(summary, TraceHooks.Now, Time.frameCount);
        Owner.Log.Info("API trace on: " + summary + ".");

        GameEvents.SceneLoaded.Subscribe(this, scene => Mark("scene loaded: " + scene));
        GameEvents.Saved.Subscribe(this, folder => Mark("game saved"));
        GameEvents.Loaded.Subscribe(this, () => Mark("save loaded"));
        PatchEvents.Installed += OnPatch;
        Track(Disposable.Create(() => PatchEvents.Installed -= OnPatch));
        _nextSweep = TraceHooks.Now + SweepSeconds;
    }

    // Every patch that goes on or fails while the trace runs is marked; with PatchHandlers on, a traced
    // mod's new handlers join the trace. May come from another thread, so no Unity calls.
    private void OnPatch(PatchReport report)
    {
        if (!TraceHooks.Active || report.Owner == TraceHooks.HarmonyId)
            return;
        TraceHooks.Book?.AddMark(report.Ok ? $"patch on: {report.Owner} {report.Target} ({report.Kinds})"
            : $"patch failed: {report.Owner} {report.Target}: {report.Error}", TraceHooks.Now, TraceHooks.Frame);
        if (report.Ok && _settings.Value.PatchHandlers && _assemblies != null)
            TraceHooks.Add(TraceTargets.Handlers(report.Handlers, _assemblies, _settings.Value.Filter));
    }

    public override void OnUpdate()
    {
        TraceHooks.Frame = Time.frameCount;
        double now = TraceHooks.Now;
        if (now < _nextSweep)
            return;
        _nextSweep = now + SweepSeconds;
        TraceHooks.Book?.Sweep(now);
    }

    protected override void OnDisable()
    {
        int patched = TraceHooks.Patched;
        TraceHooks.Stop();
        TraceHooks.Book?.AddMark("trace stopped", TraceHooks.Now, Time.frameCount);
        TraceFormat.Describe = null;
        var top = TraceHooks.Book?.Top(5);
        var lines = new List<string>();
        if (top != null)
        {
            foreach (var stats in top)
                lines.Add($"{stats.Method} x{stats.Calls}");
        }
        Owner.Log.Info($"API trace off ({patched} methods were patched). Most called: " + (lines.Count == 0 ? "nothing" : string.Join(", ", lines)) + ".");
    }

    private void Mark(string text) => TraceHooks.Book?.AddMark(text, TraceHooks.Now, Time.frameCount);

    private List<Assembly> Assemblies(TraceSettings settings)
    {
        Missing.Clear();
        var list = new List<Assembly>();
        foreach (string id in settings.Mods ?? new List<string>())
        {
            var mod = ModRegistry.Find(id);
            if (mod == null || mod.Instance == null)
            {
                Missing.Add(id);
                continue;
            }
            var assembly = mod.Instance.GetType().Assembly;
            if (!list.Contains(assembly))
                list.Add(assembly);
        }
        return list;
    }
}
