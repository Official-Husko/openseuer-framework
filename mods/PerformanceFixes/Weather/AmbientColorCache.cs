using HarmonyLib;
using ObenFramework.Features;
using UnityEngine;
using UnityEngine.AzureSky;

namespace PerformanceFixes.Weather;

/// <summary>
/// Every area portal asks the weather for the outside ambient colour on every frame. That is a
/// string search through the weather profile followed by a gradient lookup, repeated for every
/// portal even though the answer is the same for all of them. The colour is a pure function of the
/// weather profile and three numbers from the sky clock, so this remembers the last answer and
/// reuses it while those four things are exactly the same. When any of them moves, it is
/// calculated the normal way, so the colour always matches what the game would have computed.
/// </summary>
internal sealed class AmbientColorCache : ModFeature
{
    public const string Key = "AmbientColorCache";

    public override string Id => Key;
    public override string Name => "Outside ambient colour is looked up once";
    public override string Description => "Area portals and window lights share one ambient colour lookup per change of weather or time.";

    protected override void OnDisable() => AmbientColorCachePatch.Clear();
}

[HarmonyPatchCategory(AmbientColorCache.Key)]
[HarmonyPatch(typeof(WeatherManager), nameof(WeatherManager.CurrentOutsideAmbience))]
internal static class AmbientColorCachePatch
{
    private static bool _valid;
    private static AzureWeatherProfile _profile;
    private static float _timeline;
    private static float _sun;
    private static float _moon;
    private static Color _color;

    // Inputs of the call that is running now, kept for the postfix.
    private static AzureWeatherProfile _pendingProfile;
    private static float _pendingTimeline;
    private static float _pendingSun;
    private static float _pendingMoon;
    private static bool _pending;

    internal static void Clear()
    {
        _valid = false;
        _pending = false;
        _profile = null;
        _pendingProfile = null;
    }

    private static bool Prefix(WeatherManager __instance, ref Color __result)
    {
        _pending = false;
        var timeOfDay = TimeOfDayAzure.instance;
        var clock = timeOfDay != null ? timeOfDay.TimeController : null;
        var profile = __instance.ActiveWeather();
        // Anything unexpected is left to the game, which fails the way it always did.
        if (clock == null || profile == null)
            return true;

        float timeline = clock.GetTimeline();
        float sun = clock.GetSunElevation();
        float moon = clock.GetMoonElevation();
        if (_valid && _profile == profile && _timeline == timeline && _sun == sun && _moon == moon)
        {
            __result = _color;
            return false;
        }

        _pendingProfile = profile;
        _pendingTimeline = timeline;
        _pendingSun = sun;
        _pendingMoon = moon;
        _pending = true;
        return true;
    }

    private static void Postfix(Color __result)
    {
        if (!_pending)
            return;
        _pending = false;
        _profile = _pendingProfile;
        _timeline = _pendingTimeline;
        _sun = _pendingSun;
        _moon = _pendingMoon;
        _color = __result;
        _valid = true;
    }
}
