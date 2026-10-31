using System;
using HarmonyLib;
using ObenFramework.Features;

namespace ObenPatches.Fixes;

/// <summary>
/// Putting on a hat with a head lamp (Hard Hat with Headlamp) shows a "Head Lamp [B]" prompt until the lamp is first
/// switched on, and the lamp's onUnequip hides it again. But taking the hat off, from the inventory or by swapping
/// it, goes through CharacterSlotController.UpdateItem, which destroys the lamp's object without calling onUnequip,
/// so the prompt stayed on screen until a lamp was switched on somewhere. The fix hides the head lamp prompt
/// whenever the head slot changes; a lamp put on again shows it again.
/// </summary>
internal sealed class HeadLampPrompt : ModFeature
{
    public const string Key = "HeadLampPrompt";

    public override string Id => Key;
    public override string Name => "Head lamp prompt goes away";
    public override string Description => "Taking off a hat with a head lamp also takes its \"Head Lamp\" key prompt off the screen.";
}

[HarmonyPatchCategory(HeadLampPrompt.Key)]
[HarmonyPatch(typeof(CharacterSlotController), nameof(CharacterSlotController.UpdateItem))]
internal static class HeadLampPromptPatch
{
    private static readonly Action<Tutorial> HideButton = AccessTools.MethodDelegate<Action<Tutorial>>(
        AccessTools.Method(typeof(Tutorial), "HideTutorialButton"));

    private static void Prefix(CharacterSlotController __instance)
    {
        var tutorial = Tutorial.instance;
        if (!(__instance is HeadSlotController) || tutorial == null || !tutorial.HeadLightTutorial)
            return;
        // What the lamp's own onUnequip does for the head slot, without clearing anyone else's prompt flags.
        tutorial.HeadLightTutorial = false;
        HideButton(tutorial);
    }
}
