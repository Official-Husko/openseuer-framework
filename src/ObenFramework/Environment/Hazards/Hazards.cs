// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
using System;
using System.Collections.Generic;
using ObenFramework.Items;
using ObenFramework.Patching;
using ObenFramework.Saves;
using ObenFramework.World;
using UnityEngine;

namespace ObenFramework.Env;

public enum HazardKind
{
    /// <summary>A trigger volume that adds a radiation dose while the player is inside (<c>TriggerRadiation</c>).</summary>
    RadiationZone,

    /// <summary>A trigger volume that hurts what enters it: razor wire, fire, sewage, poison mushrooms (<c>TriggerHurt</c>).</summary>
    DamageZone,

    /// <summary>A point that radiates through walls up to a range (<c>point_radiation</c>). Saved with the game.</summary>
    RadiationSource,

    /// <summary>A scene radiation meter that drives lights or signs (<c>point_radiation_detector</c>).</summary>
    Detector,
}

/// <summary>A snapshot of one hazard.</summary>
public sealed class HazardInfo
{
    internal HazardInfo(string id, HazardKind kind, string name, Vector3 position, bool enabled, float strength, string damageType)
    {
        Id = id;
        Kind = kind;
        Name = name;
        Position = position;
        Enabled = enabled;
        Strength = strength;
        DamageType = damageType;
    }

    public string Id { get; }

    public HazardKind Kind { get; }

    public string Name { get; }

    public Vector3 Position { get; }

    public bool Enabled { get; }

    /// <summary>mSv per hour for radiation, damage per game second for a damage zone, the threshold for a detector.</summary>
    public float Strength { get; }

    /// <summary>What a damage zone does (Bleed, Fire, Radiation, Mushrooms...); null for the other kinds.</summary>
    public string DamageType { get; }
}

/// <summary>The player walked into a radiation or damage zone, as raised by <see cref="Hazards.Entered"/>.</summary>
public readonly struct HazardEntry
{
    public HazardEntry(Component zone, HazardKind kind)
    {
        Zone = zone;
        Kind = kind;
    }

    /// <summary>The <c>TriggerRadiation</c> or <c>TriggerHurt</c>.</summary>
    public Component Zone { get; }

    public HazardKind Kind { get; }

    public string Id => Hazards.IdOf(Zone);
}

/// <summary>
/// Radiation zones and sources, damage zones and radiation meters of the loaded scenes. List them, switch
/// them on and off, and hear when the player walks into one. Zones switched by a mod are back to the
/// scene's state after a level load; radiation sources save their switch with the game. Changes are refused
/// (false) while a save is written or loaded.
/// </summary>
public static class Hazards
{
    /// <summary>The player entered an enabled radiation or damage zone. A damage zone that only adds radiation counts once, as a damage zone.</summary>
    public static readonly WorldEvent<HazardEntry> Entered = new WorldEvent<HazardEntry>(nameof(Entered));

    /// <summary>True when the hooks are in place, so <see cref="Entered"/> is raised.</summary>
    public static bool IsAvailable => PatchAreas.IsAvailable(PatchAreas.Framework, "Env");

    /// <summary>Every hazard of the loaded scenes. A scan.</summary>
    public static List<HazardInfo> All()
    {
        var list = new List<HazardInfo>();
        foreach (var zone in ObjectIds.Active<TriggerRadiation>())
            list.Add(Describe(zone));
        foreach (var zone in ObjectIds.Active<TriggerHurt>())
            list.Add(Describe(zone));
        foreach (var source in ObjectIds.Active<point_radiation>())
            list.Add(Describe(source));
        foreach (var meter in ObjectIds.Active<point_radiation_detector>())
            list.Add(Describe(meter));
        return list;
    }

    /// <summary>The hazard component with this id, or null.</summary>
    public static Component Find(string id)
    {
        if (string.IsNullOrEmpty(id))
            return null;
        Component found = ObjectIds.Find<TriggerHurt>(id);
        if (found == null)
            found = ObjectIds.Find<TriggerRadiation>(id);
        if (found == null)
            found = ObjectIds.Find<point_radiation>(id);
        if (found == null)
            found = ObjectIds.Find<point_radiation_detector>(id);
        return found != null ? found : null;
    }

    public static string IdOf(Component hazard)
    {
        switch (hazard)
        {
            case TriggerRadiation zone: return ObjectIds.IdOf(zone);
            case TriggerHurt zone: return ObjectIds.IdOf(zone);
            case point_radiation source: return ObjectIds.IdOf(source);
            case point_radiation_detector meter: return ObjectIds.IdOf(meter);
            default: return null;
        }
    }

    public static HazardInfo Describe(Component hazard)
    {
        switch (hazard)
        {
            case TriggerRadiation zone:
                return new HazardInfo(IdOf(zone), HazardKind.RadiationZone, zone.name, zone.transform.position, zone.Enabled && zone.enabled, zone.radiation, null);
            case TriggerHurt zone:
                return new HazardInfo(IdOf(zone), HazardKind.DamageZone, zone.name, zone.transform.position, zone.Enabled && zone.enabled, zone.DamagePerSecond, zone.DamageType.ToString());
            case point_radiation source:
                return new HazardInfo(IdOf(source), HazardKind.RadiationSource, source.name, source.transform.position, source.active, source.radiation, null);
            case point_radiation_detector meter:
                return new HazardInfo(IdOf(meter), HazardKind.Detector, meter.name, meter.transform.position, meter.on, meter.threshold, null);
            default:
                return null;
        }
    }

    /// <summary>
    /// Switches a hazard on or off. A damage zone that adds radiation switches its radiation part with it.
    /// Throws when there is no hazard with that id.
    /// </summary>
    public static bool SetEnabled(string id, bool enabled)
    {
        var hazard = Find(id);
        if (hazard == null)
            throw new ArgumentException($"No hazard '{id}'.", nameof(id));
        return SetEnabled(hazard, enabled);
    }

    /// <summary>
    /// Switches a hazard on or off. A radiation zone switched off stops adding to the dose at once, also while
    /// the player stands in it (the game's own switch only keeps new visitors out). True when it is that way.
    /// </summary>
    public static bool SetEnabled(Component hazard, bool enabled)
    {
        if (hazard == null)
            throw new ArgumentNullException(nameof(hazard));
        if (KindOf(hazard) == null)
            throw new ArgumentException("Not a hazard.", nameof(hazard));
        if (SaveGate.IsBusy)
            return false;
        using (WorldChanges.EnterMod())
        {
            switch (hazard)
            {
                case TriggerRadiation zone:
                    SwitchRadiation(zone, enabled);
                    break;
                case TriggerHurt zone:
                    zone.Enable(enabled);
                    if (zone.DamageType == DamageTypeNames.Radiation)
                    {
                        foreach (var part in zone.GetComponents<TriggerRadiation>())
                            SwitchRadiation(part, enabled);
                    }
                    break;
                case point_radiation source:
                    source.Toggle(enabled);
                    break;
                case point_radiation_detector meter:
                    meter.on = enabled;
                    break;
            }
        }
        return Describe(hazard).Enabled == enabled;
    }

    /// <summary>The kind of a hazard component, or null when it is not one.</summary>
    public static HazardKind? KindOf(Component hazard)
    {
        switch (hazard)
        {
            case TriggerRadiation _: return HazardKind.RadiationZone;
            case TriggerHurt _: return HazardKind.DamageZone;
            case point_radiation _: return HazardKind.RadiationSource;
            case point_radiation_detector _: return HazardKind.Detector;
            default: return null;
        }
    }

    // The zone's dose tick only checks the component's own switch, so both go together.
    private static void SwitchRadiation(TriggerRadiation zone, bool enabled)
    {
        zone.Enable(enabled);
        zone.enabled = enabled;
    }

    internal static void RaiseRadiationEntered(TriggerRadiation zone)
    {
        // A damage zone of the radiation type adds this component itself; it is reported as the damage zone.
        var hurt = zone.GetComponent<TriggerHurt>();
        if (hurt != null && hurt.DamageType == DamageTypeNames.Radiation)
            return;
        Entered.Raise(new HazardEntry(zone, HazardKind.RadiationZone));
    }

    internal static void RaiseDamageEntered(TriggerHurt zone)
    {
        if (zone.Enabled)
            Entered.Raise(new HazardEntry(zone, HazardKind.DamageZone));
    }
}
