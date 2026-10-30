using System.Collections.Generic;
using System.ComponentModel;

namespace DevBridge.Tracing;

/// <summary>The tracer's settings, in Mods/DevBridge/trace.json. trace.start writes the filter and mods it is given.</summary>
public sealed class TraceSettings
{
    /// <summary>
    /// Comma-separated type name or namespace prefixes ("Quest,GlobalFlags", "ObenFramework.Housing").
    /// Empty traces the whole public API, about 1600 methods for the framework, which takes a few seconds to patch.
    /// </summary>
    [Description("Comma-separated type or namespace prefixes to trace, such as Quest,GlobalFlags. Empty traces the whole public API, which takes a few seconds to set up.")]
    public string Filter = "";

    /// <summary>The mods whose public API is traced, by mod id.</summary>
    [Description("The mods whose public API is traced, by mod id.")]
    public List<string> Mods = new List<string> { "ObenFramework" };

    /// <summary>
    /// Also trace those mods' patch handlers (their prefixes and postfixes on game methods), the filter
    /// applying to the handler's class, including handlers that go on while the trace runs. Handlers on
    /// per-frame methods add a lot of calls.
    /// </summary>
    [Description("Also trace the patch handlers of those mods, the prefixes and postfixes they add to game methods. Handlers on per-frame methods add a lot of calls.")]
    public bool PatchHandlers;

    /// <summary>
    /// Also trace the mods' non-public methods (their internals: save and event plumbing, helpers), within the
    /// filter. Many more calls; narrow the filter first.
    /// </summary>
    [Description("Also trace the non-public methods of those mods, their internals, within the filter. Many more calls; narrow the filter first.")]
    public bool NonPublic;

    /// <summary>
    /// Copy every line the mods and the loader log (debug lines too with verbose logging) into the trace as
    /// a mark, so log and calls read in order.
    /// </summary>
    [Description("Copy log lines from the mods and the loader into the trace, so the log and the calls read in order. Debug lines are included when verbose logging is on.")]
    public bool MirrorLog = true;

    /// <summary>At most this many log lines a second are copied into the trace; the rest are counted in a mark.</summary>
    [Description("At most this many log lines a second are copied into the trace. The rest are counted.")]
    public int MirrorLinesPerSecond = 200;

    /// <summary>Verbose logging while a trace runs, so debug lines reach the logs and the trace; put back when it stops.</summary>
    [Description("Turn verbose logging on while a trace runs, so debug lines reach the logs and the trace. It is put back when the trace stops.")]
    public bool VerboseLog = true;

    /// <summary>Write the trace as text to ObenLoader/logs/trace.log when it stops.</summary>
    [Description("When a trace stops, write it as text to ObenLoader/logs/trace.log.")]
    public bool WriteFile = true;

    /// <summary>How many records are kept; older ones go first.</summary>
    [Description("How many trace records are kept. Older records are dropped first.")]
    public int Capacity = 2000;

    /// <summary>Calls per method kept in full in each window; the rest are counted.</summary>
    [Description("How many calls per method are kept in full in each window. The rest are only counted.")]
    public int LinesPerWindow = 20;

    [Description("The length of each trace window, in seconds.")]
    public float WindowSeconds = 10f;

    /// <summary>A safety limit on how many methods one start may patch.</summary>
    [Description("The most methods one trace start may patch.")]
    public int MaxMethods = 4000;
}
