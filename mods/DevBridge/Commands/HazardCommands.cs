using System;
using System.Collections.Generic;
using DevBridge.Server;
using Newtonsoft.Json.Linq;
using ObenFramework.Env;
using ObenFramework.Player;
using UnityEngine;

namespace DevBridge.Commands;

/// <summary>Hazards, radiation and water through ObenFramework.Env.</summary>
internal static class HazardCommands
{
    [BridgeCommand("hazards.list", "Radiation zones and sources, damage zones and radiation meters, nearest to the player first.",
        Params = new[] { "kind?: RadiationZone, DamageZone, RadiationSource or Detector", "radius?: metres around the player", "max?: default 50" })]
    private static object HazardsList(JObject p)
    {
        string kind = p.Str("kind", null);
        float radius = p.Float("radius", 0f);
        int max = Math.Max(1, p.Int("max", 50));
        var origin = PlayerRef.Position;

        var found = new List<(float distance, HazardInfo info)>();
        foreach (var hazard in Hazards.All())
        {
            if (kind != null && !string.Equals(hazard.Kind.ToString(), kind, StringComparison.OrdinalIgnoreCase))
                continue;
            float distance = origin.HasValue ? Vector3.Distance(origin.Value, hazard.Position) : 0f;
            if (radius > 0f && origin.HasValue && distance > radius)
                continue;
            found.Add((distance, hazard));
        }
        found.Sort((a, b) => a.distance.CompareTo(b.distance));

        var list = new JArray();
        for (int i = 0; i < found.Count && i < max; i++)
        {
            var h = found[i].info;
            list.Add(new JObject
            {
                ["id"] = h.Id, ["kind"] = h.Kind.ToString(), ["name"] = h.Name, ["enabled"] = h.Enabled, ["strength"] = h.Strength,
                ["damageType"] = h.DamageType, ["distance"] = origin.HasValue ? (float)Math.Round(found[i].distance, 1) : (float?)null,
            });
        }
        return new JObject { ["total"] = found.Count, ["hazards"] = list };
    }

    [BridgeCommand("hazards.set", "Switch a hazard on or off by id (see hazards.list).", Params = new[] { "id: hazard id", "enabled: true or false" })]
    private static object HazardsSet(JObject p)
    {
        string id = p.Str("id");
        var hazard = Hazards.Find(id) ?? throw new ArgumentException($"No hazard '{id}'.");
        Hazards.SetEnabled(hazard, p.Bool("enabled"));
        var h = Hazards.Describe(hazard);
        return new JObject { ["id"] = h.Id, ["kind"] = h.Kind.ToString(), ["enabled"] = h.Enabled };
    }

    [BridgeCommand("radiation.info", "The player's dose, the rate here, the background and the dosimeter in hand; dose sets the dose.",
        Params = new[] { "dose?: set the player's dose (mSv)" })]
    private static object RadiationInfo(JObject p)
    {
        if (!Radiation.IsAvailable)
            throw new InvalidOperationException("There is no player here.");
        if (p["dose"] != null)
            Radiation.SetDose(p.Float("dose"));
        return new JObject
        {
            ["dose"] = Radiation.Dose, ["damageThreshold"] = Radiation.DamageThreshold, ["rateHere"] = Radiation.RateHere,
            ["background"] = Radiation.Background, ["wasteland"] = Radiation.IsWasteland, ["dosimeter"] = Radiation.DosimeterEquipped,
            ["dosimeterOn"] = Radiation.DosimeterOn, ["dosimeterCharge"] = Radiation.DosimeterCharge,
        };
    }

    [BridgeCommand("water.info", "Swimming, breath, the scene's water supply and its bodies of water.")]
    private static object WaterInfo(JObject p)
    {
        var volumes = new JArray();
        foreach (var w in WorldWater.Volumes())
            volumes.Add(Volume(w));
        return new JObject
        {
            ["swimming"] = WorldWater.IsPlayerSwimming, ["underwater"] = WorldWater.IsPlayerUnderwater, ["breathUsed"] = WorldWater.BreathUsed,
            ["maxBreath"] = WorldWater.MaxBreath, ["supply"] = WorldWater.SupplyAmount, ["supplyInfinite"] = WorldWater.SupplyIsInfinite, ["volumes"] = volumes,
        };
    }

    [BridgeCommand("water.set", "Change a body of water (see water.info), or give the player a full breath.",
        Params = new[] { "id?: water id", "density?/drag?/drownDamage?: numbers", "hygiene?: what a swim sets hygiene to, -1 for nothing", "breath?: true refills the breath" })]
    private static object WaterSet(JObject p)
    {
        if (p.Bool("breath", false) && !WorldWater.ResetBreath())
            throw new InvalidOperationException("There is no water system here.");
        string id = p.Str("id", null);
        if (id != null)
        {
            WorldWater.SetVolume(id, p["density"]?.Value<float>(), p["drag"]?.Value<float>(), p["drownDamage"]?.Value<float>(), p["hygiene"]?.Value<int>());
            return Volume(WorldWater.Describe(WorldWater.Find(id)));
        }
        return WaterInfo(p);
    }

    private static JObject Volume(WaterVolumeInfo w) => new JObject
    {
        ["id"] = w.Id, ["name"] = w.Name, ["density"] = w.Density, ["drag"] = w.Drag, ["drownDamage"] = w.DrownDamage,
        ["swimHygiene"] = w.SwimHygiene, ["maxDepth"] = w.MaxDepth,
    };
}
