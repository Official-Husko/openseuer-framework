using System;
using HarmonyLib;
using ObenSave.Features;

namespace ObenSave.Patches;

/// <summary>
/// After SaveGame returns, the folder it saved into is converted to containers. The game has already
/// written its complete files by then, and a conversion that fails leaves them as they are. The patch
/// belongs to the CompressedSaves feature, so it exists only while the feature is on.
/// </summary>
[HarmonyPatchCategory(CompressedSavesFeature.Key)]
[HarmonyPatch(typeof(SaveController), nameof(SaveController.SaveGame))]
internal static class SaveGamePatch
{
    private static void Postfix()
    {
        try
        {
            var controller = SaveController.instance;
            if (controller == null)
                return;
            SaveConversion.Run(controller.GetCurrentSavePath(), compress: true);
        }
        catch (Exception e)
        {
            ObenSaveMod.Logger?.Error("Compressing the save failed, the game's files are kept", e);
        }
    }
}
