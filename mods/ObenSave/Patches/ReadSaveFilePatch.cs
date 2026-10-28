using System;
using System.IO;
using HarmonyLib;
using ObenSave.Files;

namespace ObenSave.Patches;

/// <summary>
/// Every save file the game loads (Info, Globals, level files, dialogue) is read by
/// SaveController.ReadSaveFile. A container is decoded here; a vanilla file, or anything that goes
/// wrong, falls through to the game's own code. Applied whenever the mod is loaded, whether or not
/// the CompressedSaves feature is on, so saves stay loadable after it is switched off.
/// </summary>
[HarmonyPatch(typeof(SaveController), nameof(SaveController.ReadSaveFile))]
internal static class ReadSaveFilePatch
{
    private static bool Prefix(string path, ref string __result)
    {
        try
        {
            if (SaveFiles.TryReadContainer(path, out string text))
            {
                __result = text;
                return false;
            }
        }
        catch (Exception e) when (e is FileNotFoundException || e is DirectoryNotFoundException)
        {
            // Let the game report a missing file the way it always did.
        }
        catch (Exception e)
        {
            ObenSaveMod.Logger?.Error($"Couldn't read {path} as an ObenSave file", e);
        }
        return true;
    }
}
