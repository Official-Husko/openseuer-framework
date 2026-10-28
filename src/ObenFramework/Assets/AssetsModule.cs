using System;
using ObenLoader;
using ObenLoader.Runtime;

namespace ObenFramework.Assets;

/// <summary>
/// Sets up the asset loaders and logs for them. Each mod's assets are released when that mod unloads;
/// whatever is left when the framework itself unloads is released here.
/// </summary>
internal static class AssetsModule
{
    public static void Init(ObenMod framework)
    {
        AssetsLog.Source = framework;
        framework.Track(Disposable.Create(() =>
        {
            AssetSets.ReleaseAll();
            ModSounds.Reset();
            ModelMaterials.Reset();
            AssetsLog.Source = null;
        }));
    }
}

/// <summary>Logging for the asset code, safe to call while the framework is not loaded.</summary>
internal static class AssetsLog
{
    public static ObenMod Source;

    public static void Info(string message) => Source?.Log.Info(message);

    public static void Warn(string message) => Source?.Log.Warn(message);

    public static void Error(string message, Exception e = null)
    {
        var log = Source?.Log;
        if (log == null)
            return;
        if (e == null)
            log.Error(message);
        else
            log.Error(message, e);
    }
}
