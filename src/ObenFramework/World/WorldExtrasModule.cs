using ObenFramework.World.Props;
using ObenLoader;

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
    }
}
