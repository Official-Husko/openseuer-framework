using System;
using System.Collections.Generic;
using DevBridge.Server;
using DevBridge.Tracing;
using Newtonsoft.Json.Linq;
using ObenFramework.Features;

namespace DevBridge.Commands;

/// <summary>
/// The API call tracer over the bridge. Starting and stopping switch the DevBridge/ApiTrace feature (so the
/// Mods page shows it and it stays on across restarts until stopped); the list reads what was recorded and
/// is still readable after a stop. Everything comes back as flat JSON objects for scripts and MCP clients.
/// </summary>
internal static class TraceCommands
{
    private const int RefusedShown = 30;

    [BridgeCommand("trace.start", "Start (or restart) the framework API call tracer, optionally with a new filter or set of mods.",
        Params = new[] { "filter?: comma-separated type name or namespace prefixes, e.g. Quest,GlobalFlags; empty traces everything", "mods?: comma-separated mod ids whose API is traced, default ObenFramework", "handlers?: also trace those mods' patch handlers (true/false, kept until changed)", "internals?: also trace their non-public methods (true/false, kept until changed)" })]
    private static object Start(JObject p)
    {
        var trace = Feature();
        var settings = trace.Settings;
        if (p["filter"] != null)
            settings.Filter = p.Str("filter", "");
        if (p["mods"] != null)
            settings.Mods = Split(p.Str("mods", ""));
        if (p["handlers"] != null)
            settings.PatchHandlers = p.Bool("handlers", false);
        if (p["internals"] != null)
            settings.NonPublic = p.Bool("internals", false);
        trace.SaveSettings();

        if (trace.Enabled)
            ModFeatures.SetEnabled(trace, false);
        ModFeatures.SetEnabled(trace, true);
        return Status(trace);
    }

    [BridgeCommand("trace.stop", "Stop the API call tracer and take its patches off. What it recorded stays readable with trace.list.")]
    private static object Stop(JObject p)
    {
        var trace = Feature();
        if (trace.Enabled)
            ModFeatures.SetEnabled(trace, false);
        var status = Status(trace);
        status["top"] = Top(10);
        return status;
    }

    [BridgeCommand("trace.save", "Write what the tracer recorded to ObenLoader/logs/trace.log as text, one line per record, calls indented by depth. Stopping a trace does this too.")]
    private static object Save(JObject p)
    {
        string path = Feature().WriteText();
        return new JObject { ["written"] = path != null, ["path"] = path };
    }

    [BridgeCommand("trace.list", "Recorded calls (oldest first), markers and 'more' notes for calls left out, with the most called methods. Pass the returned 'next' as 'after' to get only new records.",
        Params = new[] { "after?: only records after this sequence number", "max?: at most this many records, default 200", "method?: only records whose method contains this", "top?: how many of the most called methods, default 10", "clear?: forget the records afterwards" })]
    private static object List(JObject p)
    {
        var trace = Feature();
        var status = Status(trace);
        var book = TraceHooks.Book;
        var calls = new JArray();
        long next = 0;
        if (book != null)
        {
            book.Sweep(TraceHooks.Now);
            foreach (var record in book.Read(p["after"]?.Value<long>() ?? 0L, Math.Max(1, p.Int("max", 200)), p.Str("method", null)))
                calls.Add(Describe(record));
            next = book.NextSeq - 1;
        }
        status["next"] = next;
        status["records"] = calls;
        status["top"] = Top(p.Int("top", 10));
        if (p.Bool("clear", false))
            book?.Clear();
        return status;
    }

    private static ApiTrace Feature() =>
        ApiTrace.Instance ?? throw new InvalidOperationException("The tracer isn't registered; is DevBridge loading?");

    private static JObject Status(ApiTrace trace)
    {
        var refused = TraceHooks.Refused;
        var names = new JArray();
        for (int i = 0; i < refused.Count && i < RefusedShown; i++)
            names.Add(refused[i]);
        return new JObject
        {
            ["active"] = TraceHooks.Active,
            ["feature"] = ModFeatures.KeyOf(trace),
            ["filter"] = trace.Settings.Filter ?? "",
            ["mods"] = new JArray(trace.Settings.Mods?.ToArray() ?? new string[0]),
            ["missingMods"] = new JArray(trace.Missing.ToArray()),
            ["handlers"] = trace.Settings.PatchHandlers,
            ["internals"] = trace.Settings.NonPublic,
            ["selected"] = trace.Selected,
            ["patched"] = TraceHooks.Patched,
            ["patchMs"] = TraceHooks.PatchMs,
            ["refused"] = refused.Count,
            ["refusedNames"] = names,
        };
    }

    private static JObject Describe(TraceRecord r)
    {
        var json = new JObject
        {
            ["seq"] = r.Seq,
            ["kind"] = r.Kind,
            ["t"] = Math.Round(r.Time, 3),
            ["frame"] = r.Frame,
            ["method"] = r.Method,
        };
        if (r.Kind == TraceBook.CallKind)
        {
            json["args"] = r.Args;
            json["ms"] = Math.Round(r.Ms, 3);
            json["depth"] = r.Depth;
            if (r.Error != null)
                json["error"] = r.Error;
            else
                json["result"] = r.Result;
            if (r.Thread != 0)
                json["thread"] = r.Thread;
            if (r.From != null)
                json["from"] = r.From;
        }
        else if (r.Kind == TraceBook.MoreKind)
        {
            json["more"] = r.More;
            json["lastArgs"] = r.Args;
            json["lastResult"] = r.Result;
        }
        return json;
    }

    private static JArray Top(int max)
    {
        var top = new JArray();
        var book = TraceHooks.Book;
        if (book == null)
            return top;
        foreach (var stats in book.Top(Math.Max(0, max)))
        {
            top.Add(new JObject
            {
                ["method"] = stats.Method,
                ["calls"] = stats.Calls,
                ["errors"] = stats.Errors,
                ["avgMs"] = Math.Round(stats.TotalMs / stats.Calls, 4),
                ["maxMs"] = Math.Round(stats.MaxMs, 3),
            });
        }
        return top;
    }

    private static List<string> Split(string text)
    {
        var list = new List<string>();
        foreach (string part in text.Split(','))
        {
            if (part.Trim().Length > 0)
                list.Add(part.Trim());
        }
        return list;
    }
}
