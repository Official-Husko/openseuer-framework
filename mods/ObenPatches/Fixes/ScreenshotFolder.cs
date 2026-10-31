using System.IO;
using HarmonyLib;
using ObenFramework.Features;
using UnityEngine;

namespace ObenPatches.Fixes;

/// <summary>
/// F11 and F12 never save a screenshot. TakeScreenshot.getFilename creates a Screenshots folder next to the game's
/// exe and checks for free names there, but hands Unity the relative name "Screenshots/...", which the player
/// resolves inside Obenseuer_Data, where no such folder exists, so every shot ends in "Failed to store screen
/// shot". The fix hands Unity the full path of the folder the game made.
/// </summary>
internal sealed class ScreenshotFolder : ModFeature
{
    public const string Key = "ScreenshotFolder";

    public override string Id => Key;
    public override string Name => "Screenshots are saved";
    public override string Description => "F11 and F12 save their screenshots in the Screenshots folder next to the game.";
}

[HarmonyPatchCategory(ScreenshotFolder.Key)]
[HarmonyPatch(typeof(TakeScreenshot), "getFilename")]
internal static class ScreenshotFolderPatch
{
    private static void Postfix(ref string __result)
    {
        if (string.IsNullOrEmpty(__result) || Path.IsPathRooted(__result))
            return;
        __result = Path.GetFullPath(Path.Combine(Application.dataPath, "..", __result));
    }
}
