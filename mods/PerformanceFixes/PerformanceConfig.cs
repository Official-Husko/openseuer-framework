namespace PerformanceFixes;

/// <summary>Settings in the mod's config.json. Missing values keep these defaults.</summary>
public sealed class PerformanceConfig
{
    /// <summary>
    /// Seconds between refreshes of the open inventory screen (0.25 is four times a second).
    /// Values outside 0.05 to 2 are clamped.
    /// </summary>
    public float InventoryRefreshSeconds = 0.25f;
}
