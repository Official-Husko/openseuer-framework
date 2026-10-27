using ObenLoader;
using ObenUIKit.Input;

namespace ObenUIKit;

/// <summary>Entry point of the UI kit. It's essential: the mod menu that switches other mods lives here.</summary>
public sealed class UIKitMod : ObenMod
{
    internal static UIKitMod Instance { get; private set; }

    public override void OnLoad()
    {
        Instance = this;
        InputLock.Init(this);
        Log.Info("Ready.");
    }

    public override void OnUnload() => Instance = null;
}
