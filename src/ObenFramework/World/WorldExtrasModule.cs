using ObenFramework.Events;
using ObenFramework.World.Logic;
using ObenFramework.World.Props;
using ObenLoader;
using ObenLoader.Runtime;

namespace ObenFramework.World;

/// <summary>
/// Sets up props, landmarks, saved-object data and visited scenes, and resets the scene logic and sign
/// helpers between scenes and on unload. Their Harmony patches are applied by the loader.
/// </summary>
internal static class WorldExtrasModule
{
    public static void Init(ObenMod framework)
    {
        PropsModule.Init(framework);
        Landmarks.Init(framework);
        GameEvents.LevelChanging.Subscribe(framework, Signs.Reset);
        GameEvents.MainMenu.Subscribe(framework, Signs.Reset);
        framework.Track(Disposable.Create(() =>
        {
            Signs.Reset();
            SceneState.Log.Reset();
        }));
    }
}
