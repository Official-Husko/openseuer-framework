using ObenLoader;

namespace ObenKit;

/// <summary>Entry point of the framework mod. Its Harmony patches are applied by the loader.</summary>
public sealed class KitMod : ObenMod
{
    internal static KitMod Instance { get; private set; }

    public override void OnLoad()
    {
        Instance = this;
        Log.Info("Ready.");
    }

    public override void OnUnload() => Instance = null;
}
