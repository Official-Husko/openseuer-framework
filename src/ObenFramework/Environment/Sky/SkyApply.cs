// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
using ObenFramework.Game;
using UnityEngine;
using UnityEngine.AzureSky;

namespace ObenFramework.Env;

/// <summary>
/// Writes the sky overrides into the render settings, the sky dome and the sun lights, every frame while
/// one holds. Around the game's own sky writers (<see cref="SkyPatches"/>) the game's values are put back
/// and read again afterwards, so whatever the game writes stays the value underneath.
/// </summary>
internal static class SkyApply
{
    private const string ExposureProperty = "_Exposure";
    private static readonly object RenderSettingsToken = typeof(RenderSettings);

    private static readonly SkyChannel<bool> FogEnabled = new SkyChannel<bool>(_ => RenderSettings.fog, (_, v) => RenderSettings.fog = v);
    private static readonly SkyChannel<FogMode> FogMode = new SkyChannel<FogMode>(_ => RenderSettings.fogMode, (_, v) => RenderSettings.fogMode = v);
    private static readonly SkyChannel<Color> FogColor = new SkyChannel<Color>(_ => RenderSettings.fogColor, (_, v) => RenderSettings.fogColor = v);
    private static readonly SkyChannel<float> FogDensity = new SkyChannel<float>(_ => RenderSettings.fogDensity, (_, v) => RenderSettings.fogDensity = v);
    private static readonly SkyChannel<float> FogStart = new SkyChannel<float>(_ => RenderSettings.fogStartDistance, (_, v) => RenderSettings.fogStartDistance = v);
    private static readonly SkyChannel<float> FogEnd = new SkyChannel<float>(_ => RenderSettings.fogEndDistance, (_, v) => RenderSettings.fogEndDistance = v);
    private static readonly SkyChannel<Color> AmbientFlat = new SkyChannel<Color>(_ => RenderSettings.ambientLight, (_, v) => RenderSettings.ambientLight = v);
    private static readonly SkyChannel<Color> AmbientSky = new SkyChannel<Color>(_ => RenderSettings.ambientSkyColor, (_, v) => RenderSettings.ambientSkyColor = v);
    private static readonly SkyChannel<Color> AmbientEquator = new SkyChannel<Color>(_ => RenderSettings.ambientEquatorColor, (_, v) => RenderSettings.ambientEquatorColor = v);
    private static readonly SkyChannel<Color> AmbientGround = new SkyChannel<Color>(_ => RenderSettings.ambientGroundColor, (_, v) => RenderSettings.ambientGroundColor = v);
    private static readonly SkyChannel<float> AmbientIntensity = new SkyChannel<float>(_ => RenderSettings.ambientIntensity, (_, v) => RenderSettings.ambientIntensity = v);
    private static readonly SkyChannel<float> MaterialExposure = new SkyChannel<float>(t => ((Material)t).GetFloat(ExposureProperty), (t, v) => ((Material)t).SetFloat(ExposureProperty, v));
    private static readonly SkyChannel<float> DomeExposure = new SkyChannel<float>(t => ((AzureSkyRenderController)t).exposure, (t, v) => ((AzureSkyRenderController)t).exposure = v);
    private static readonly SkyChannel<float> Stars = new SkyChannel<float>(t => ((AzureSkyRenderController)t).starsIntensity, (t, v) => ((AzureSkyRenderController)t).starsIntensity = v);
    private static readonly SkyChannel<float> Clouds = new SkyChannel<float>(t => ((AzureSkyRenderController)t).dynamicCloudsDensity, (t, v) => ((AzureSkyRenderController)t).dynamicCloudsDensity = v);

    private static readonly ISkyChannel[] RenderChannels =
        { FogEnabled, FogMode, FogColor, FogDensity, FogStart, FogEnd, AmbientFlat, AmbientSky, AmbientEquator, AmbientGround, AmbientIntensity };
    private static readonly ISkyChannel[] DomeChannels = { DomeExposure, Stars, Clouds };

    private static AzureSkyRenderController _heldDome;
    private static Material _heldSkybox;
    private static bool _running, _pending;
    private static int _suspended;

    /// <summary>True while any channel still holds a value, even after the last override went (until it is restored).</summary>
    public static bool Holding { get; private set; }

    public static void Run()
    {
        // The main menu keeps its own sky; overrides come back with the next level.
        if (_running || WorldState.IsLoadingSave || !WorldState.InWorld)
            return;
        if (_suspended > 0)
        {
            _pending = true;
            return;
        }
        _running = true;
        try
        {
            SkySun.Scan();
            ApplyAll();
        }
        finally
        {
            _running = false;
            Holding = ComputeHolding();
        }
    }

    public static void ForgetScene()
    {
        Forget(RenderChannels);
        ReleaseDome();
        ReleaseMaterial();
        SkySun.ForgetScene();
        Holding = ComputeHolding();
    }

    /// <summary>A game sky writer is about to run.</summary>
    public static void BeforeGame()
    {
        if (!Holding)
            return;
        _suspended++;
        if (_suspended == 1)
            Sweep(false);
    }

    /// <summary>A game sky writer finished (or threw).</summary>
    public static void AfterGame()
    {
        if (_suspended == 0)
            return;
        _suspended--;
        if (_suspended > 0)
            return;
        Sweep(true);
        if (!_pending)
            return;
        _pending = false;
        Run();
    }

    public static SkyState Read()
    {
        SkySun.Scan();
        var render = SkySun.Render;
        var state = new SkyState
        {
            FogEnabled = RenderSettings.fog,
            FogMode = RenderSettings.fogMode,
            FogColor = RenderSettings.fogColor,
            FogDensity = RenderSettings.fogDensity,
            FogStart = RenderSettings.fogStartDistance,
            FogEnd = RenderSettings.fogEndDistance,
            AmbientColor = RenderSettings.ambientLight,
            AmbientIntensity = RenderSettings.ambientIntensity,
        };
        var skybox = RenderSettings.skybox;
        if (render != null)
        {
            state.SkyboxExposure = render.exposure;
            state.StarsIntensity = render.starsIntensity;
            state.CloudDensity = render.dynamicCloudsDensity;
        }
        else if (skybox != null && skybox.HasProperty(ExposureProperty))
        {
            state.SkyboxExposure = skybox.GetFloat(ExposureProperty);
        }
        var sun = SkySun.First;
        if (sun != null)
        {
            state.SunIntensity = sun.intensity;
            state.SunColor = sun.color;
        }
        return state;
    }

    internal static void Step(ISkyChannel channel, bool capture)
    {
        if (capture)
            channel.Capture();
        else
            channel.Suspend();
    }

    private static void ApplyAll()
    {
        FogEnabled.Apply(RenderSettingsToken, WorldSky.Pick(o => o.FogEnabled));
        FogMode.Apply(RenderSettingsToken, WorldSky.Pick(o => o.FogMode));
        FogColor.Apply(RenderSettingsToken, WorldSky.Pick(o => o.FogColor));
        FogDensity.Apply(RenderSettingsToken, WorldSky.Pick(o => o.FogDensity));
        FogStart.Apply(RenderSettingsToken, WorldSky.Pick(o => o.FogStart));
        FogEnd.Apply(RenderSettingsToken, WorldSky.Pick(o => o.FogEnd));
        var ambient = WorldSky.Pick(o => o.AmbientColor);
        AmbientFlat.Apply(RenderSettingsToken, ambient);
        AmbientSky.Apply(RenderSettingsToken, ambient);
        AmbientEquator.Apply(RenderSettingsToken, ambient);
        AmbientGround.Apply(RenderSettingsToken, ambient);
        AmbientIntensity.Apply(RenderSettingsToken, WorldSky.Pick(o => o.AmbientIntensity));

        var exposure = WorldSky.Pick(o => o.SkyboxExposure);
        var skybox = RenderSettings.skybox;
        if (skybox == null || _heldSkybox != skybox)
            ReleaseMaterial();
        if (skybox != null && skybox.HasProperty(ExposureProperty))
        {
            MaterialExposure.Apply(skybox, exposure);
            _heldSkybox = skybox;
        }

        var render = SkySun.Render;
        if (render == null || _heldDome != render)
            ReleaseDome();
        if (render != null)
        {
            DomeExposure.Apply(render, exposure);
            Stars.Apply(render, WorldSky.Pick(o => o.StarsIntensity));
            Clouds.Apply(render, WorldSky.Pick(o => o.CloudDensity));
            _heldDome = render;
        }

        SkySun.Apply(WorldSky.Pick(o => o.SunIntensity), WorldSky.Pick(o => o.SunColor));
    }

    private static bool ComputeHolding() => SkySun.Holding || AnyHeld(RenderChannels) || AnyHeld(DomeChannels) || MaterialExposure.Held;

    private static bool AnyHeld(ISkyChannel[] channels)
    {
        foreach (var channel in channels)
        {
            if (channel.Held)
                return true;
        }
        return false;
    }

    private static void Forget(ISkyChannel[] channels)
    {
        foreach (var channel in channels)
            channel.Forget();
    }

    private static void Sweep(bool capture)
    {
        foreach (var channel in RenderChannels)
            Step(channel, capture);
        if (_heldSkybox != null)
            Step(MaterialExposure, capture);
        if (_heldDome != null)
        {
            foreach (var channel in DomeChannels)
                Step(channel, capture);
        }
        SkySun.Sweep(capture);
    }

    private static void ReleaseMaterial()
    {
        var old = _heldSkybox;
        _heldSkybox = null;
        if (old != null)
            MaterialExposure.Apply(old, null);
        MaterialExposure.Forget();
    }

    private static void ReleaseDome()
    {
        var old = _heldDome;
        _heldDome = null;
        if (old != null)
        {
            DomeExposure.Apply(old, null);
            Stars.Apply(old, null);
            Clouds.Apply(old, null);
        }
        Forget(DomeChannels);
    }
}
