// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
using System.Collections.Generic;
using DevBridge.Tracing;
using ObenFramework.Features;

namespace DevBridge.Commands;

/// <summary>
/// The tracer from the F1 console, like the reference's core_debug: "trace on [filter]" starts it (an empty
/// filter traces everything), "trace off" stops it and writes logs/trace.log, "trace status" says what runs
/// and what is called most, "trace save" writes the file without stopping.
/// </summary>
internal static class TraceConsole
{
    public const string Name = "trace";
    public const string Usage = "trace [on [filter]|off|status|save]";

    public static string Run(string[] args)
    {
        var trace = ApiTrace.Instance;
        if (trace == null)
            return "The tracer isn't registered.";
        string verb = args.Length > 0 ? args[0].ToLowerInvariant() : "status";
        switch (verb)
        {
            case "on":
                trace.Settings.Filter = args.Length > 1 ? string.Join(" ", args, 1, args.Length - 1).Trim() : "";
                trace.SaveSettings();
                if (trace.Enabled)
                    ModFeatures.SetEnabled(trace, false);
                ModFeatures.SetEnabled(trace, true);
                return Status(trace);
            case "off":
                if (!trace.Enabled)
                    return "The tracer is off.";
                ModFeatures.SetEnabled(trace, false);
                return "Trace stopped. " + Top();
            case "save":
                string path = trace.WriteText();
                return path != null ? "Wrote " + path : "Nothing to write: no trace ran this session.";
            case "status":
                return Status(trace);
            default:
                return "Usage: " + Usage;
        }
    }

    private static string Status(ApiTrace trace)
    {
        if (!TraceHooks.Active)
            return "The tracer is off. " + Top();
        string filter = string.IsNullOrWhiteSpace(trace.Settings.Filter) ? "everything" : "'" + trace.Settings.Filter + "'";
        return $"Tracing {TraceHooks.Patched} methods of {string.Join(", ", trace.Settings.Mods)} matching {filter} "
            + $"({TraceHooks.Refused.Count} refused, patched in {TraceHooks.PatchMs} ms). " + Top();
    }

    private static string Top()
    {
        var top = TraceHooks.Book?.Top(10);
        if (top == null || top.Count == 0)
            return "Nothing recorded yet.";
        var names = new List<string>();
        foreach (var stats in top)
            names.Add($"{stats.Method} x{stats.Calls}");
        return "Most called: " + string.Join(", ", names) + ".";
    }
}
