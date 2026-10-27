using System.IO;
using HarmonyLib;
using ObenFramework.Features;
using UnityEngine;

namespace ObenPatches.Fixes;

/// <summary>
/// Until the first save exists, the game lists save folders in a Saves folder that isn't there
/// yet, and every visit to the main menu logs a DirectoryNotFoundException. The fix creates the
/// empty folder first, which is what the first save would do anyway.
/// </summary>
internal sealed class MissingSavesFolder : ModFeature
{
    public const string Key = "MissingSavesFolder";

    public override string Id => Key;
    public override string Name => "No error before the first save";
    public override string Description => "Creates the empty Saves folder so the main menu stops logging an error on a fresh install.";
}

[HarmonyPatchCategory(MissingSavesFolder.Key)]
[HarmonyPatch(typeof(SaveController), nameof(SaveController.GetCharacters))]
internal static class MissingSavesFolderPatch
{
    private static void Prefix() =>
        Directory.CreateDirectory(Path.Combine(Application.persistentDataPath, "Saves"));
}
