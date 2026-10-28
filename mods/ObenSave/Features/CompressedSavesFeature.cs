using System.IO;
using ObenFramework.Features;
using UnityEngine;

namespace ObenSave.Features;

/// <summary>
/// Turns the files of every save into the ObenSave container after the game wrote them. Switching it
/// off on purpose converts the compressed files back to what the game writes, so the saves on disk are
/// then readable without this mod. Unloading or reloading the mod does not convert anything.
/// The Harmony patch is in <see cref="Patches.SaveGamePatch"/>.
/// </summary>
internal sealed class CompressedSavesFeature : ModFeature
{
    public const string Key = "CompressedSaves";

    public override string Id => Key;
    public override string Name => "Compressed save files";
    public override string Description => "Saves are stored compressed in an open format. Switching this off turns them back into the game's own files.";
    public override string AppliesWhen => "the next time the game saves";

    /// <summary>The folder with every character's saves.</summary>
    public static string SavesFolder => Path.Combine(Application.persistentDataPath, "Saves");

    protected override void OnDisable()
    {
        // Also called when the mod unloads or reloads; only a switch the player made restores the files.
        if (ModFeatures.IsWanted(this))
            return;
        SaveConversion.Run(SavesFolder, compress: false);
    }
}
