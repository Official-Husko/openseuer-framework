using System.Globalization;

namespace PerformanceFixes.Measuring;

/// <summary>The perf console command: frame time statistics for comparing a fix on and off.</summary>
internal static class PerfCommand
{
    private const float DefaultSeconds = 10f;
    private const float LongestSeconds = 300f;

    public static string Run(string[] args)
    {
        if (args.Length > 0 && args[0] == "reset")
        {
            FrameStats.Reset();
            return "Frame history cleared.";
        }

        float seconds = DefaultSeconds;
        if (args.Length > 0 && !float.TryParse(args[0], NumberStyles.Float, CultureInfo.InvariantCulture, out seconds))
            return "Usage: perf [seconds|reset]";
        if (seconds <= 0f || seconds > LongestSeconds)
            return $"Pick a window between 0 and {LongestSeconds:0} seconds.";
        return FrameStats.Report(seconds);
    }
}
