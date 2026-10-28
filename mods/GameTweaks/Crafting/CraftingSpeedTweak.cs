// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
using HarmonyLib;
using ObenFramework.Crafting;

namespace GameTweaks.Crafting;

/// <summary>
/// Crafting goes faster (or slower). Crafting stations go through the framework's <see cref="CraftingSpeed"/>
/// for all stations; furniture built from blueprints at a workbench doesn't use a station recipe, so its
/// build time is divided here when the build starts. Crafts already running keep their time.
/// </summary>
internal sealed class CraftingSpeedTweak : TweakFeature
{
    public const string Key = "CraftingSpeed";

    internal static float Speed = 1f;

    public override string Id => Key;
    public override string Name => "Faster (or slower) crafting";
    public override string Description =>
        "Every crafting station and furniture blueprint takes less time, by the speed on the Settings page. Crafts already running keep their time.";

    public override void Apply(TweaksConfig config)
    {
        Speed = TweakMath.Factor(config.CraftingSpeed, CraftingSpeed.MinSpeed, CraftingSpeed.MaxSpeed);
        if (Speed == 1f)
            CraftingSpeed.Remove(this, CraftingSpeed.AllStations);
        else
            CraftingSpeed.Set(this, CraftingSpeed.AllStations, Speed);
    }

    protected override void OnDisable()
    {
        Speed = 1f;
        CraftingSpeed.Clear(this);
    }
}

[HarmonyPatchCategory(CraftingSpeedTweak.Key)]
[HarmonyPatch(typeof(BaseCrafting), nameof(BaseCrafting.StartCrafting))]
internal static class BlueprintSpeedPatch
{
    private static void Postfix(BaseCrafting __instance) =>
        __instance.craftingLength = TweakMath.Faster(__instance.craftingLength, CraftingSpeedTweak.Speed);
}
