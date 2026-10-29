// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Threading;
using HarmonyLib;
using ObenLoader;

namespace DevBridge.Tracing;

/// <summary>A traced method and what the hooks need to know about it, worked out once when it is patched.</summary>
internal sealed class TraceTarget
{
    public TraceTarget(MethodInfo method, TraceStats stats)
    {
        Name = stats.Method;
        Parameters = method.GetParameters();
        IsVoid = method.ReturnType == typeof(void);
        Stats = stats;
    }

    public readonly string Name;
    public readonly ParameterInfo[] Parameters;
    public readonly bool IsVoid;
    public readonly TraceStats Stats;
}

/// <summary>One call in flight, handed from the prefix to the postfix and finalizer.</summary>
internal sealed class TraceCall
{
    public TraceTarget Target;
    public object[] Args;
    public object Result;
    public long Start;
    public int Depth;
    public string From;
}

/// <summary>
/// Patches the traced methods with one prefix, postfix and finalizer each, under its own Harmony id, and
/// turns every call into a <see cref="TraceBook"/> entry. Only the first calls of each window are formatted;
/// the rest cost a dictionary lookup, a timestamp and a counter. Calls made while the tracer itself is busy
/// (formatting, finding the caller) are ignored, so it can't trace itself.
/// </summary>
internal static class TraceHooks
{
    public const string HarmonyId = "DevBridge.trace";

    private static readonly HarmonyMethod PrefixHook = new HarmonyMethod(Hook(nameof(Prefix))) { priority = Priority.First };
    private static readonly HarmonyMethod ResultHook = new HarmonyMethod(Hook(nameof(Postfix)));
    private static readonly HarmonyMethod FinalizerHook = new HarmonyMethod(Hook(nameof(Finalizer)));
    private static readonly Stopwatch Clock = Stopwatch.StartNew();

    private static Dictionary<MethodBase, TraceTarget> _targets;
    private static Action<MethodInfo> _patch;
    private static HashSet<Assembly> _traced = new HashSet<Assembly>();
    private static int _mainThread;
    private static bool _warned;
    private static Action<string> _warn;

    [ThreadStatic] private static int _depth;
    [ThreadStatic] private static bool _busy;

    /// <summary>The book being written, or the last one after a stop.</summary>
    public static TraceBook Book { get; private set; }

    /// <summary>Copied from Time.frameCount once a frame, so other threads never touch Unity.</summary>
    public static int Frame { get; set; }

    public static bool Active => _targets != null;
    public static int Patched => _targets?.Count ?? 0;
    public static List<string> Refused { get; } = new List<string>();

    /// <summary>Why the first refused method couldn't be patched, or null.</summary>
    public static string FirstRefusal { get; private set; }

    public static long PatchMs { get; private set; }

    public static double Now => Clock.Elapsed.TotalSeconds;

    public static bool IsMainThread => Thread.CurrentThread.ManagedThreadId == _mainThread;

    /// <summary>
    /// Patches <paramref name="methods"/>. Called on the main thread. <paramref name="patch"/> replaces the
    /// Harmony patching, for tests that call the hooks themselves.
    /// </summary>
    public static void Start(IList<MethodInfo> methods, IEnumerable<Assembly> traced, TraceBook book, Action<string> warn,
        Action<MethodInfo> patch = null)
    {
        Stop();
        _mainThread = Thread.CurrentThread.ManagedThreadId;
        _warn = warn;
        _warned = false;
        _traced = new HashSet<Assembly>(traced);
        Book = book;
        Refused.Clear();
        FirstRefusal = null;

        var watch = Stopwatch.StartNew();
        _patch = patch ?? PatchWithHarmony;
        var map = new Dictionary<MethodBase, TraceTarget>(methods.Count);
        PatchInto(map, methods);
        PatchMs = watch.ElapsedMilliseconds;
        _targets = map;
    }

    /// <summary>
    /// Adds methods to the running trace, e.g. patch handlers that went on after it started. Returns how
    /// many were new. Does nothing while the trace is off.
    /// </summary>
    public static int Add(IEnumerable<MethodInfo> methods)
    {
        var current = _targets;
        if (current == null)
            return 0;
        // The hooks read the map without a lock, so the new set goes into a copy that replaces it.
        var map = new Dictionary<MethodBase, TraceTarget>(current);
        PatchInto(map, methods);
        _targets = map;
        return map.Count - current.Count;
    }

    private static void PatchInto(Dictionary<MethodBase, TraceTarget> map, IEnumerable<MethodInfo> methods)
    {
        foreach (var method in methods)
        {
            if (method == null || map.ContainsKey(method))
                continue;
            var target = new TraceTarget(method, Book.StatsFor(TraceFormat.Method(method), Now));
            try
            {
                _patch(method);
                map[method] = target;
            }
            catch (Exception e)
            {
                Refused.Add(target.Name);
                FirstRefusal ??= target.Name + ": " + e.GetType().Name + ": " + e.Message;
                try { new Harmony(HarmonyId).Unpatch(method, HarmonyPatchType.All, HarmonyId); }
                catch (Exception) { }
            }
        }
    }

    private static void PatchWithHarmony(MethodInfo method) =>
        new Harmony(HarmonyId).Patch(method, PrefixHook, method.ReturnType == typeof(void) ? null : ResultHook, null, FinalizerHook);

    /// <summary>Takes every trace patch off. The book stays readable until the next start.</summary>
    public static void Stop()
    {
        if (_targets == null)
            return;
        _targets = null;
        try
        {
            Unpatching.RemoveAll(HarmonyId);
        }
        catch (Exception e)
        {
            _warn?.Invoke("Taking the trace patches off failed: " + e.GetType().Name + ": " + e.Message);
        }
        Book?.Sweep(Now, all: true);
    }

    internal static void Prefix(MethodBase __originalMethod, object[] __args, out TraceCall __state)
    {
        __state = null;
        if (_busy)
            return;
        var map = _targets;
        if (map == null || !map.TryGetValue(__originalMethod, out var target))
            return;
        _busy = true;
        try
        {
            var call = new TraceCall { Target = target, Args = __args, Depth = _depth };
            if (_depth == 0 && Book.WantsDetail(target.Stats, Now))
                call.From = Caller();
            _depth++;
            call.Start = Stopwatch.GetTimestamp();
            __state = call;
        }
        catch (Exception e)
        {
            Warn(e);
        }
        finally
        {
            _busy = false;
        }
    }

    internal static void Postfix(object __result, TraceCall __state)
    {
        if (__state != null)
            __state.Result = __result;
    }

    internal static void Finalizer(Exception __exception, TraceCall __state)
    {
        if (__state == null)
            return;
        long end = Stopwatch.GetTimestamp();
        _depth = __state.Depth;
        if (_busy)
            return;
        _busy = true;
        try
        {
            Finish(__state, __exception, (end - __state.Start) * 1000d / Stopwatch.Frequency);
        }
        catch (Exception e)
        {
            Warn(e);
        }
        finally
        {
            _busy = false;
        }
    }

    private static void Finish(TraceCall call, Exception error, double ms)
    {
        var target = call.Target;
        var book = Book;
        double now = Now;
        if (!book.Admit(target.Stats, ms, error != null, now, target.Parameters, call.Args, target.IsVoid, call.Result))
            return;

        var record = new TraceRecord
        {
            Time = now,
            Frame = Frame,
            Method = target.Name,
            Args = TraceFormat.Args(target.Parameters, call.Args),
            Ms = ms,
            Depth = call.Depth,
            Thread = IsMainThread ? 0 : Thread.CurrentThread.ManagedThreadId,
            From = call.From,
        };
        if (error != null)
        {
            string frame = TraceFormat.FirstFrame(error);
            record.Error = error.GetType().Name + ": " + TraceFormat.Cut(error.Message ?? "") + (frame == null ? "" : " at " + frame);
        }
        else
        {
            record.Result = target.IsVoid ? "void" : TraceFormat.Value(call.Result);
        }
        book.AddCall(record);
    }

    // The first frame outside the traced assemblies, Harmony and the runtime: the mod that made the call.
    private static string Caller()
    {
        var frames = new StackTrace(2, false).GetFrames();
        if (frames == null)
            return null;
        foreach (var frame in frames)
        {
            var type = frame.GetMethod()?.DeclaringType;
            if (type == null || _traced.Contains(type.Assembly) || Plumbing(type.Namespace))
                continue;
            return TraceFormat.ModName(type.Assembly.GetName().Name);
        }
        return null;
    }

    private static bool Plumbing(string space)
    {
        space ??= "";
        return space.StartsWith("System", StringComparison.Ordinal) || space.StartsWith("HarmonyLib", StringComparison.Ordinal)
            || space.StartsWith("MonoMod", StringComparison.Ordinal) || space == typeof(TraceHooks).Namespace;
    }

    private static void Warn(Exception e)
    {
        if (_warned)
            return;
        _warned = true;
        _warn?.Invoke("The tracer failed and some calls may be missing: " + e.GetType().Name + ": " + e.Message);
    }

    private static MethodInfo Hook(string name) =>
        typeof(TraceHooks).GetMethod(name, BindingFlags.Static | BindingFlags.NonPublic);
}
