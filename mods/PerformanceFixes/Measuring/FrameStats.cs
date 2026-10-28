using System;
using System.Text;
using UnityEngine;

namespace PerformanceFixes.Measuring;

/// <summary>
/// Remembers the last few minutes of frame times in fixed arrays, so recording a frame never
/// allocates. Reading it back (the perf command) may allocate; that is a one-off.
/// </summary>
internal static class FrameStats
{
    private const int Capacity = 1 << 16;
    private const int Mask = Capacity - 1;

    private static readonly float[] FrameSeconds = new float[Capacity];
    private static readonly float[] FrameEnd = new float[Capacity];
    private static readonly int[] Collections = new int[Capacity];
    private static readonly float[] AllocatedKb = new float[Capacity];

    private static int _count;
    private static long _lastHeap;

    /// <summary>Called once per frame.</summary>
    public static void Record()
    {
        long heap = GC.GetTotalMemory(false);
        int slot = _count & Mask;
        FrameSeconds[slot] = Time.unscaledDeltaTime;
        FrameEnd[slot] = Time.unscaledTime;
        Collections[slot] = GC.CollectionCount(0);
        // Growth between two frames is what the game allocated; a drop means a collection ran, and
        // that frame counts as zero. Memory is handed out in blocks, so this is only an estimate.
        AllocatedKb[slot] = heap > _lastHeap ? (heap - _lastHeap) / 1024f : 0f;
        _lastHeap = heap;
        _count++;
    }

    public static void Reset() => _count = 0;

    /// <summary>A short report on the last <paramref name="seconds"/> seconds of frames.</summary>
    public static string Report(float seconds)
    {
        int available = Math.Min(_count, Capacity);
        if (available < 2)
            return "No frames recorded yet.";

        int newest = _count - 1;
        float from = FrameEnd[newest & Mask] - seconds;
        int frames = 0;
        double total = 0;
        double allocated = 0;
        float worst = 0f;
        while (frames < available - 1 && FrameEnd[(newest - frames) & Mask] >= from)
        {
            int slot = (newest - frames) & Mask;
            total += FrameSeconds[slot];
            allocated += AllocatedKb[slot];
            worst = Math.Max(worst, FrameSeconds[slot]);
            frames++;
        }
        if (frames == 0)
            return "No frames in that window.";

        var sorted = new float[frames];
        for (int i = 0; i < frames; i++)
            sorted[i] = FrameSeconds[(newest - i) & Mask];
        Array.Sort(sorted);

        double span = Math.Max(total, 0.0001);
        int collectionsBefore = Collections[(newest - frames) & Mask];
        int collections = Collections[newest & Mask] - collectionsBefore;
        float p99 = sorted[Math.Min(frames - 1, (int)(frames * 0.99))];
        float slowTail = AverageOfSlowest(sorted, Math.Max(1, frames / 100));

        var text = new StringBuilder();
        text.Append("Last ").Append(span.ToString("0.0")).Append(" s, ").Append(frames).Append(" frames\n");
        text.Append("Average ").Append((total / frames * 1000).ToString("0.00")).Append(" ms (")
            .Append((frames / span).ToString("0.0")).Append(" fps)\n");
        text.Append("Worst ").Append((worst * 1000).ToString("0.00")).Append(" ms, 99th percentile ")
            .Append((p99 * 1000).ToString("0.00")).Append(" ms, slowest 1% average ")
            .Append((slowTail * 1000).ToString("0.00")).Append(" ms\n");
        text.Append("Allocated about ").Append((allocated / span).ToString("0")).Append(" KB/s, ")
            .Append(collections).Append(" garbage collections, heap ")
            .Append((GC.GetTotalMemory(false) / (1024 * 1024)).ToString()).Append(" MB");
        return text.ToString();
    }

    private static float AverageOfSlowest(float[] sorted, int count)
    {
        double sum = 0;
        for (int i = 0; i < count; i++)
            sum += sorted[sorted.Length - 1 - i];
        return (float)(sum / count);
    }
}
