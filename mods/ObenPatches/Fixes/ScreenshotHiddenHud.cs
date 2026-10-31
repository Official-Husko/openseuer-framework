using HarmonyLib;
using ObenFramework.Features;

namespace ObenPatches.Fixes;

/// <summary>
/// A screenshot without the HUD (F12) hides the HUD for one frame by adding GameController's object to
/// GameUIController.hiddenUI, and takes it out again only when the HUD was showing before. The hide-HUD key uses
/// the same object, so with the HUD already hidden the list ends up holding it twice and the key needs two presses
/// to bring the HUD back; hidden by anything else, it stayed hidden for good. With the HUD already hidden there is
/// nothing to hide, so the fix takes an ordinary screenshot then.
/// </summary>
internal sealed class ScreenshotHiddenHud : ModFeature
{
    public const string Key = "ScreenshotHiddenHud";

    public override string Id => Key;
    public override string Name => "HUD comes back after a screenshot";
    public override string Description => "A screenshot without the HUD, taken while the HUD is hidden, no longer makes the hide-HUD key need two presses.";
}

[HarmonyPatchCategory(ScreenshotHiddenHud.Key)]
[HarmonyPatch(typeof(TakeScreenshot), nameof(TakeScreenshot.ScreenshotNoUI))]
internal static class ScreenshotHiddenHudPatch
{
    private static bool Prefix()
    {
        var ui = GameUIController.instance;
        if (ui == null || ui.GameUIVisible())
            return true;
        TakeScreenshot.Screenshot();
        return false;
    }
}
