using ObenFramework.Events;
using ObenFramework.World;
using ObenLoader;
using ObenLoader.Runtime;

namespace ObenFramework.Transport;

/// <summary>
/// Sets up levels and travel, elevators, ladders, tarp doors, trains, ticket machines, drones and wait
/// stations, and drops their scene-bound state between scenes and on unload. Their Harmony patches are
/// applied by the loader like the rest of the framework's.
/// </summary>
internal static class TransportModule
{
    internal static readonly OnceLog Log = new OnceLog("Transport");

    public static void Init(ObenMod framework)
    {
        GameEvents.LevelChanging.Subscribe(framework, ForgetScene);
        GameEvents.MainMenu.Subscribe(framework, ForgetScene);
        GameEvents.WorldReady.Subscribe(framework, Levels.OnWorldReady);
        Travel.Init(framework);
        TrainTickets.Init(framework);
        framework.Track(Disposable.Create(() =>
        {
            ForgetScene();
            Levels.Reset();
            Log.Reset();
        }));
    }

    /// <summary>Called every frame by the framework; returns at once unless someone listens for train movement.</summary>
    public static void Update() => Trains.Update();

    // Everything here refers to objects of the scene that is going away.
    private static void ForgetScene()
    {
        Trains.Reset();
        TrainTickets.ForgetScene();
    }
}
