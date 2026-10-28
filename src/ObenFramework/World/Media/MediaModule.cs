using ObenFramework.Events;
using ObenLoader;
using ObenLoader.Runtime;

namespace ObenFramework.World;

/// <summary>Sets up radios, televisions and instruments; stops songs and held notes between scenes and on unload.</summary>
internal static class MediaModule
{
    public static void Init(ObenMod framework)
    {
        GameEvents.LevelChanging.Subscribe(framework, Instruments.Reset);
        GameEvents.MainMenu.Subscribe(framework, Instruments.Reset);
        framework.Track(Disposable.Create(Instruments.Reset));
    }

    /// <summary>Called every frame by the framework; returns at once without a song or held note.</summary>
    public static void Update() => Instruments.Update();
}
