// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
using System;
using HarmonyLib;
using ObenFramework.World;
using UnityEngine.AzureSky;

namespace ObenFramework.Env;

/// <summary>
/// What the weather is doing right now: temperature, rain, snow, wind and thunder, and an event when the
/// weather profile changes. Picking a weather is <see cref="Clock.GameWeather"/>; local zones are
/// <see cref="WeatherZones"/>. Interiors have no weather system: the values read 0 there.
/// </summary>
public static class WeatherConditions
{
    private static readonly AccessTools.FieldRef<WeatherManager, AzureWeatherController> Controller =
        AccessTools.FieldRefAccess<WeatherManager, AzureWeatherController>("weatherController");
    private static readonly AccessTools.FieldRef<WeatherManager, AzureEffectsController> Effects =
        AccessTools.FieldRefAccess<WeatherManager, AzureEffectsController>("effectsController");

    /// <summary>The weather was switched (by the game's daily roll, the console or a mod). The payload is the new weather's index.</summary>
    public static readonly WorldEvent<int> WeatherChanged = new WorldEvent<int>(nameof(WeatherChanged));

    /// <summary>True when the scene has a running weather system.</summary>
    public static bool IsAvailable => WeatherManager.instance != null && Controller(WeatherManager.instance) != null;

    /// <summary>Outside temperature in degrees Celsius, from the weather's temperature curve and the time of day.</summary>
    public static float Temperature
    {
        get
        {
            var manager = WeatherManager.instance;
            if (!IsAvailable || TimeOfDayAzure.instance == null || manager.ActiveWeather() == null)
                return 0f;
            return manager.Temperature();
        }
    }

    /// <summary>Rain strength 0 to 1 (the average of the light, medium and heavy rain effects).</summary>
    public static float RainIntensity
    {
        get
        {
            var effects = EffectsController;
            return effects != null ? (effects.lightRainIntensity + effects.mediumRainIntensity + effects.heavyRainIntensity) / 3f : 0f;
        }
    }

    public static bool IsRaining => RainIntensity > 0f;

    public static float SnowIntensity
    {
        get
        {
            var effects = EffectsController;
            return effects != null ? effects.snowIntensity : 0f;
        }
    }

    public static bool IsSnowing => SnowIntensity > 0f;

    public static float WindSpeed
    {
        get
        {
            var effects = EffectsController;
            return effects != null ? effects.windSpeed : 0f;
        }
    }

    /// <summary>Lightning and thunder are on.</summary>
    public static bool IsThunderActive => IsAvailable && WeatherManager.instance.IsThunderActive();

    /// <summary>Starts or stops lightning and thunder until the next weather change. False without a weather system.</summary>
    public static bool SetThunder(bool active)
    {
        if (!IsAvailable)
            return false;
        var manager = WeatherManager.instance;
        if (manager.IsThunderActive() != active)
        {
            if (active)
                manager.StartThunder();
            else
                manager.StopThunder();
        }
        return true;
    }

    internal static AzureWeatherController WeatherController => WeatherManager.instance != null ? Controller(WeatherManager.instance) : null;

    private static AzureEffectsController EffectsController => IsAvailable ? Effects(WeatherManager.instance) : null;
}

[HarmonyPatch(typeof(WeatherManager), nameof(WeatherManager.SetWeather), new[] { typeof(int), typeof(bool), typeof(float) })]
internal static class WeatherChangedPatch
{
    [HarmonyPostfix]
    private static void After(int weatherProfile)
    {
        if (!WeatherConditions.WeatherChanged.HasSubscribers || !WorldChanges.Active)
            return;
        try
        {
            WeatherConditions.WeatherChanged.Raise(weatherProfile);
        }
        catch (Exception e)
        {
            WorldLog.Failed("Weather hook", e);
        }
    }
}
