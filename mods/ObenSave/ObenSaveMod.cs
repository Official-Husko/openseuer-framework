using ObenFramework.Features;
using ObenLoader;
using ObenLoader.Logging;
using ObenSave.Commands;
using ObenSave.Features;

namespace ObenSave;

/// <summary>
/// ObenSave: save files in an open, compressed container (docs/modding/save-format.md). Reading both
/// formats is always on while the mod is loaded; writing the container is the CompressedSaves feature.
/// </summary>
public sealed class ObenSaveMod : ObenMod
{
    internal static ModLogger Logger;

    public override void OnLoad()
    {
        Logger = Log;
        ModFeatures.Register(this, new CompressedSavesFeature());
        SaveConsoleCommand.Register(this);
    }

    public override void OnUnload() => Logger = null;
}
