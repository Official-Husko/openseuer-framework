using ObenFramework.Console;
using ObenFramework.Features;
using ObenLoader;
using ObenLoader.Config;
using PerformanceFixes.Interaction;
using PerformanceFixes.Lighting;
using PerformanceFixes.Measuring;
using PerformanceFixes.Slots;

namespace PerformanceFixes;

/// <summary>
/// Performance Fixes: optimisations for work the game repeats every frame, from the notes in
/// docs/research/performance.md. Each one gives the same result with less work and is a feature
/// the player can switch off on its own. Write-ups are in docs/modding/performance-fixes.md.
/// </summary>
public sealed class PerformanceFixesMod : ObenMod
{
    internal static ConfigFile<PerformanceConfig> Config { get; private set; }

    public override void OnLoad()
    {
        Config = LoadConfig<PerformanceConfig>();
        Config.Save();

        ModFeatures.Register(this, new InventoryRefreshThrottle());
        ModFeatures.Register(this, new InteractableLookup());
        ModFeatures.Register(this, new LightZoneCheck());
        ModFeatures.Register(this, new InventorySlotBuffers());

        GameConsole.Register(this, "perf", "perf [seconds|reset]", PerfCommand.Run);
    }

    public override void OnUpdate() => FrameStats.Record();
}
