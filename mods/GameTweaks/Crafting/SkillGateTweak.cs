// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
using HarmonyLib;

namespace GameTweaks.Crafting;

/// <summary>
/// Recipes stop asking for a minimum skill. The game uses a recipe's minimum skill in three places: to hide
/// or grey out the recipe (<c>RecipeInfoUI.CheckSkillSoft</c>), to multiply the craft time for every point
/// the player is short (<c>Recipe.GetProcessTime</c>), and to raise the fail chance when the result is rolled
/// (<c>Recipe.GetCraftingResult</c>). All three see no minimum while this is on. Skill still lowers the fail
/// chance and raises quality as usual. Furniture blueprints have no skill gate.
/// </summary>
internal sealed class SkillGateTweak : TweakFeature
{
    public const string Key = "NoMinimumSkill";

    public override string Id => Key;
    public override string Name => "Recipes don't need a minimum skill";
    public override string Description =>
        "Any recipe can be crafted whatever your skill, without the extra time. Skill still decides the fail chance and the quality.";

    public override void Apply(TweaksConfig config) { }
}

[HarmonyPatchCategory(SkillGateTweak.Key)]
[HarmonyPatch]
internal static class SkillGatePatches
{
    // The time without the "skill too low" multiplier; the rest of the game's formula is unchanged.
    // Harmony binds parameters by name: 'multpiler' is the game's own spelling.
    [HarmonyPostfix]
    [HarmonyPatch(typeof(Recipe), nameof(Recipe.GetProcessTime))]
    private static void ProcessTime(Recipe __instance, float multpiler, float equipmentQuality, ref int __result)
    {
        if (__instance.MinimumSkill == 0 || equipmentQuality == 0f)
            return;
        float time = __instance.ProcessTime * multpiler / equipmentQuality;
        var difficulty = DifficultyController.instance;
        if (difficulty != null && difficulty.currentDifficultyInfo != null)
            time = difficulty.currentDifficultyInfo.GetItemCraftingTime(time);
        __result = (int)time;
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(RecipeInfoUI), nameof(RecipeInfoUI.CheckSkillSoft))]
    private static void Visible(Recipe recipe, float equipmentquality, ref bool __result)
    {
        if (__result || recipe == null || PlayerStats.instance == null)
            return;
        if (Recipe.CurrentFailChance(recipe.FailChance, recipe.RelevantSkill, equipmentquality, recipe.SkillFactor, 0) < 1f)
            __result = true;
    }

    // The roll reads the field directly, so it is zeroed for the call and put back afterwards, even if the
    // game's code throws.
    [HarmonyPrefix]
    [HarmonyPatch(typeof(Recipe), nameof(Recipe.GetCraftingResult))]
    private static void BeforeRoll(Recipe __instance, out int __state)
    {
        __state = __instance.MinimumSkill;
        __instance.MinimumSkill = 0;
    }

    [HarmonyFinalizer]
    [HarmonyPatch(typeof(Recipe), nameof(Recipe.GetCraftingResult))]
    private static void AfterRoll(Recipe __instance, int __state) => __instance.MinimumSkill = __state;
}
