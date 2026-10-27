using System;
using System.Collections.Generic;
using System.Threading;

namespace ObenLoader.Runtime;

/// <summary>Queue work from any thread to run on Unity's main thread during the next Update.</summary>
public static class MainThread
{
    private static readonly Queue<Action> Pending = new Queue<Action>();
    private static readonly List<Action> Draining = new List<Action>();
    private static readonly object Lock = new object();
    private static int _mainThreadId;

    public static bool IsCurrent => Thread.CurrentThread.ManagedThreadId == _mainThreadId;

    internal static void CaptureThread() => _mainThreadId = Thread.CurrentThread.ManagedThreadId;

    public static void Post(Action action)
    {
        lock (Lock)
            Pending.Enqueue(action);
    }

    /// <summary>Runs <paramref name="func"/> on the main thread and waits for the result.</summary>
    public static T Invoke<T>(Func<T> func, int timeoutMs = 10000)
    {
        if (IsCurrent)
            return func();

        T result = default;
        Exception error = null;
        using var done = new ManualResetEventSlim(false);
        Post(() =>
        {
            try { result = func(); }
            catch (Exception e) { error = e; }
            finally { done.Set(); }
        });

        if (!done.Wait(timeoutMs))
            throw new TimeoutException("The game didn't respond in time (is it stuck on a loading screen?).");
        if (error != null)
            throw error;
        return result;
    }

    internal static void Drain(Logging.ModLogger log)
    {
        lock (Lock)
        {
            if (Pending.Count == 0)
                return;
            while (Pending.Count > 0)
                Draining.Add(Pending.Dequeue());
        }

        for (int i = 0; i < Draining.Count; i++)
        {
            try { Draining[i](); }
            catch (Exception e) { log.Error("Queued action failed", e); }
        }
        Draining.Clear();
    }
}
