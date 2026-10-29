// Shows a feature that fails while switching on leaving nothing behind. Run it with
//   oben run scripts/examples/broken-feature.cs
// It registers a feature for the Script Runner mod whose [HarmonyPatchCategory] class adds a line to the F1
// console's "help", and whose OnEnable then throws. The patch went on first, so before this was fixed the line
// stayed although the feature never came up. Now the patch comes off again: the result says "patched False",
// oben call console.exec command=help has no "Patched by a broken feature." line, and the log has the error.
// Reloading Script Runner (oben call mods.reload id=ScriptRunner) forgets the feature.
using System;
using HarmonyLib;
using ObenFramework.Features;
using ObenLoader;
using ObenLoader.Mods;
using ScriptRunner;

public static class BrokenFeatureDemo
{
    public static object Run(ScriptScope scope)
    {
        ObenMod owner = ModRegistry.Find("ScriptRunner").Instance;
        var feature = new BrokenFeature();
        ModFeatures.Register(owner, feature);
        ModFeatures.SetEnabled(feature, true);

        var info = Harmony.GetPatchInfo(AccessTools.Method(typeof(DeveloperConsole), nameof(DeveloperConsole.PrintValidCommands)));
        bool patched = info != null && info.Owners.Contains("ScriptRunner");
        return $"enabled {feature.Enabled}, patched {patched}";
    }
}

public sealed class BrokenFeature : ModFeature
{
    public override string Id => "BrokenDemo";
    public override string Name => "Broken demo";
    public override string Description => "Fails on purpose while switching on.";
    public override bool EnabledByDefault => false;

    protected override void OnEnable() => throw new InvalidOperationException("this feature fails on purpose");
}

[HarmonyPatch(typeof(DeveloperConsole), nameof(DeveloperConsole.PrintValidCommands))]
[HarmonyPatchCategory("BrokenDemo")]
public static class BrokenFeaturePatch
{
    private static void Postfix(DeveloperConsole __instance) => __instance.Print("Patched by a broken feature.");
}
