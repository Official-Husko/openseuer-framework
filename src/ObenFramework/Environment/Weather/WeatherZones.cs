// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
using System;
using System.Collections.Generic;
using HarmonyLib;
using ObenFramework.Clock;
using ObenFramework.Events;
using ObenFramework.Items;
using ObenLoader;
using UnityEngine;
using UnityEngine.AzureSky;

namespace ObenFramework.Env;

/// <summary>A snapshot of one local weather zone.</summary>
public sealed class WeatherZoneInfo
{
    internal WeatherZoneInfo(string id, string name, bool active, float blendDistance, string profile, int weatherIndex, bool overridden)
    {
        Id = id;
        Name = name;
        Active = active;
        BlendDistance = blendDistance;
        Profile = profile;
        WeatherIndex = weatherIndex;
        Overridden = overridden;
    }

    public string Id { get; }

    public string Name { get; }

    /// <summary>The zone's trigger is enabled, so it applies when the player is inside.</summary>
    public bool Active { get; }

    /// <summary>Metres over which the zone's weather blends into the outside weather.</summary>
    public float BlendDistance { get; }

    /// <summary>The weather profile's name.</summary>
    public string Profile { get; }

    /// <summary>The profile's index among the global weathers, or -1 for a zone-only profile.</summary>
    public int WeatherIndex { get; }

    /// <summary>A mod overrides it.</summary>
    public bool Overridden { get; }
}

/// <summary>
/// Local weather zones (Azure weather zones): areas with their own weather, like a shack with no sky.
/// A mod can give a zone one of the global weathers for as long as it wants; the zone's own profile comes
/// back when the mod clears it or unloads. An override is kept by zone id and applies again whenever the
/// zone's level is loaded. The time of day's own sky-override zone is not listed.
/// </summary>
public static class WeatherZones
{
    public const float MaxBlendDistance = 10000f;

    private readonly struct ZoneValue
    {
        public ZoneValue(int weather, float blend)
        {
            Weather = weather;
            Blend = blend;
        }

        public int Weather { get; }

        public float Blend { get; }
    }

    private readonly struct Original
    {
        public Original(AzureWeatherProfile profile, float blend)
        {
            Profile = profile;
            Blend = blend;
        }

        public AzureWeatherProfile Profile { get; }

        public float Blend { get; }
    }

    private static readonly AccessTools.FieldRef<TimeOfDayAzure, AzureWeatherZone> SkyZone =
        AccessTools.FieldRefAccess<TimeOfDayAzure, AzureWeatherZone>("overrideSkyZone");

    private static readonly OwnedValues<ZoneValue> Overrides = new OwnedValues<ZoneValue> { Released = Apply };
    private static readonly Dictionary<AzureWeatherZone, Original> Originals = new Dictionary<AzureWeatherZone, Original>();
    private static readonly HashSet<string> Missing = new HashSet<string>();

    /// <summary>The zones of the current scene's weather system; empty without one.</summary>
    public static List<WeatherZoneInfo> All()
    {
        var list = new List<WeatherZoneInfo>();
        foreach (var zone in Zones())
            list.Add(Describe(zone));
        return list;
    }

    /// <summary>
    /// Gives the zone the global weather at <paramref name="weatherIndex"/> (see <see cref="GameWeather.Names"/>)
    /// with the given blend distance, until <paramref name="owner"/> clears it or unloads. The newest override
    /// of a zone wins. Throws for an unknown weather; a zone id that is not in this scene is kept for later.
    /// </summary>
    public static void Set(ObenMod owner, string zoneId, int weatherIndex, float blendDistance)
    {
        if (owner == null)
            throw new ArgumentNullException(nameof(owner));
        if (string.IsNullOrEmpty(zoneId))
            throw new ArgumentException("Give a zone id.", nameof(zoneId));
        if (!(blendDistance >= 0f && blendDistance <= MaxBlendDistance))
            throw new ArgumentOutOfRangeException(nameof(blendDistance), $"0 to {MaxBlendDistance} metres.");
        if (GameWeather.IsAvailable && (weatherIndex < 0 || weatherIndex >= GameWeather.Count))
            throw new ArgumentOutOfRangeException(nameof(weatherIndex), $"Weather index must be 0 to {GameWeather.Count - 1}.");
        Overrides.Set(owner, owner.Info.Id, zoneId, new ZoneValue(weatherIndex, blendDistance));
        Apply(zoneId);
    }

    /// <summary>Ends <paramref name="owner"/>'s override of one zone. True when it had one.</summary>
    public static bool Clear(ObenMod owner, string zoneId)
    {
        if (owner == null || !Overrides.Remove(owner, zoneId))
            return false;
        Apply(zoneId);
        return true;
    }

    /// <summary>Ends every zone override of <paramref name="owner"/>. Returns how many there were.</summary>
    public static int ClearAll(ObenMod owner)
    {
        if (owner == null)
            return 0;
        var keys = Overrides.RemoveOwner(owner);
        foreach (string key in keys)
            Apply(key);
        return keys.Count;
    }

    internal static void Reapply()
    {
        foreach (string key in Overrides.Keys())
            Apply(key);
    }

    /// <summary>The scene is going: its zones go with it, nothing to restore.</summary>
    internal static void ForgetScene()
    {
        Originals.Clear();
        Missing.Clear();
    }

    internal static void Unload()
    {
        var keys = Overrides.Keys();
        Overrides.Clear();
        foreach (string key in keys)
            Apply(key);
        ForgetScene();
    }

    private static void Apply(string zoneId)
    {
        var zone = ObjectIds.Find<AzureWeatherZone>(zoneId);
        if (zone == null)
        {
            if (Overrides.Has(zoneId) && Missing.Add(zoneId))
                GameEvents.Log?.Info($"Weather zone '{zoneId}' is not in this scene; its override waits for its level.");
            return;
        }

        if (!Overrides.TryGet(zoneId, out var want))
        {
            if (Originals.TryGetValue(zone, out var original))
            {
                zone.profile = original.Profile;
                zone.blendDistance = original.Blend;
                Originals.Remove(zone);
            }
            return;
        }

        var profiles = WeatherConditions.WeatherController != null ? WeatherConditions.WeatherController.GetGlobalWeatherProfilesList() : null;
        if (profiles == null || want.Weather < 0 || want.Weather >= profiles.Count || profiles[want.Weather] == null)
            return;
        if (!Originals.ContainsKey(zone))
            Originals[zone] = new Original(zone.profile, zone.blendDistance);
        zone.SetProfile(profiles[want.Weather].profile);
        zone.blendDistance = want.Blend;
    }

    private static List<AzureWeatherZone> Zones()
    {
        var list = new List<AzureWeatherZone>();
        var controller = WeatherConditions.WeatherController;
        if (controller == null || controller.m_localWeatherZonesList == null)
            return list;
        var skyZone = TimeOfDayAzure.instance != null ? SkyZone(TimeOfDayAzure.instance) : null;
        foreach (var zone in controller.m_localWeatherZonesList)
        {
            if (zone != null && zone != skyZone)
                list.Add(zone);
        }
        return list;
    }

    private static WeatherZoneInfo Describe(AzureWeatherZone zone)
    {
        var collider = zone.GetComponent<Collider>();
        bool active = collider != null && collider.enabled && collider.gameObject.activeInHierarchy;
        return new WeatherZoneInfo(ObjectIds.IdOf(zone), zone.name, active, zone.blendDistance, zone.profile != null ? zone.profile.name : null,
            IndexOf(zone.profile), Originals.ContainsKey(zone));
    }

    private static int IndexOf(AzureWeatherProfile profile)
    {
        var profiles = profile != null && WeatherConditions.WeatherController != null ? WeatherConditions.WeatherController.GetGlobalWeatherProfilesList() : null;
        if (profiles == null)
            return -1;
        for (int i = 0; i < profiles.Count; i++)
        {
            if (profiles[i] != null && profiles[i].profile == profile)
                return i;
        }
        return -1;
    }
}
