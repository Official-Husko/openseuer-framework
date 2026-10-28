using ObenFramework.Events;
using ObenFramework.World.Addressing;
using ObenLoader;
using ObenLoader.Runtime;

namespace ObenFramework.World;

/// <summary>
/// Sets up the world module: forgets scene-bound caches when a level goes away and undoes everything on
/// unload. The Harmony patches themselves are applied by the loader like the framework's others.
/// </summary>
internal static class WorldModule
{
    public static void Init(ObenMod framework)
    {
        GameEvents.LevelChanging.Subscribe(framework, ForgetScene);
        GameEvents.MainMenu.Subscribe(framework, ForgetScene);
        GameEvents.WorldReady.Subscribe(framework, _ => GuidIndex.Invalidate());
        framework.Track(Disposable.Create(ForgetScene));
        PlacedFurniture.Init(framework);
    }

    // Everything cached per scene refers to objects that are about to be destroyed.
    private static void ForgetScene()
    {
        WorldChanges.Reset();
        WorldLog.Reset();
        GuidIndex.Reset();
        Doors.ClearCaches();
        PlacedFurniture.ForgetScene();
        FurnitureKinds.Reset();
    }
}
