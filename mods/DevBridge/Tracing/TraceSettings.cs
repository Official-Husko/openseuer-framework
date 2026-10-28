using System.Collections.Generic;

namespace DevBridge.Tracing;

/// <summary>The tracer's settings, in Mods/DevBridge/trace.json. trace.start writes the filter and mods it is given.</summary>
public sealed class TraceSettings
{
    /// <summary>
    /// Comma-separated type name or namespace prefixes ("Quest,GlobalFlags", "ObenFramework.Housing").
    /// Empty traces the whole public API, about 1600 methods for the framework, which takes a few seconds to patch.
    /// </summary>
    public string Filter = "";

    /// <summary>The mods whose public API is traced, by mod id.</summary>
    public List<string> Mods = new List<string> { "ObenFramework" };

    /// <summary>How many records are kept; older ones go first.</summary>
    public int Capacity = 2000;

    /// <summary>Calls per method kept in full in each window; the rest are counted.</summary>
    public int LinesPerWindow = 20;

    public float WindowSeconds = 10f;

    /// <summary>A safety limit on how many methods one start may patch.</summary>
    public int MaxMethods = 4000;
}
