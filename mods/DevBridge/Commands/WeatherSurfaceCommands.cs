using System;
using DevBridge.Server;
using Newtonsoft.Json.Linq;
using ObenFramework.Env;

namespace DevBridge.Commands;

/// <summary>Weather conditions and zones, and wall and floor coverings, through ObenFramework.Env.</summary>
internal static class WeatherSurfaceCommands
{
    [BridgeCommand("weather.conditions", "Temperature, rain, snow, wind and thunder now; thunder=true/false switches thunder.",
        Params = new[] { "thunder?: true or false" })]
    private static object Conditions(JObject p)
    {
        if (!WeatherConditions.IsAvailable)
            throw new InvalidOperationException("There is no weather system here (main menu, loading or an interior).");
        bool ok = p["thunder"] == null || WeatherConditions.SetThunder(p.Bool("thunder"));
        return new JObject
        {
            ["ok"] = ok,
            ["temperature"] = WeatherConditions.Temperature, ["rain"] = WeatherConditions.RainIntensity, ["snow"] = WeatherConditions.SnowIntensity,
            ["wind"] = WeatherConditions.WindSpeed, ["thunder"] = WeatherConditions.IsThunderActive,
        };
    }

    [BridgeCommand("weather.zones", "Local weather zones of the scene.")]
    private static object Zones(JObject p)
    {
        var list = new JArray();
        foreach (var z in WeatherZones.All())
            list.Add(new JObject
            {
                ["id"] = z.Id, ["name"] = z.Name, ["active"] = z.Active, ["blend"] = z.BlendDistance, ["profile"] = z.Profile,
                ["weatherIndex"] = z.WeatherIndex, ["overridden"] = z.Overridden,
            });
        return new JObject { ["zones"] = list };
    }

    [BridgeCommand("weather.zone", "Give a weather zone one of the global weathers until cleared, or clear it.",
        Params = new[] { "id: zone id (see weather.zones)", "weather?: weather index (see weather.list)", "blend?: metres, default 50", "clear?: true ends the override", "mod?: owner, default the bridge" })]
    private static object Zone(JObject p)
    {
        var owner = BridgeOwner.Of(p);
        string id = p.Str("id");
        if (p.Bool("clear", false))
            WeatherZones.Clear(owner, id);
        else
            WeatherZones.Set(owner, id, p["weather"]?.Value<int>() ?? throw new ArgumentException("Give 'weather' or clear=true."), p.Float("blend", 50f));
        return Zones(p);
    }

    [BridgeCommand("surfaces.list", "Walls, floors and ceilings that take wallpaper, paint or panels, with their covering.", Params = new[] { "max?: default 100" })]
    private static object SurfacesList(JObject p)
    {
        int max = Math.Max(1, p.Int("max", 100));
        var all = Surfaces.All();
        var list = new JArray();
        for (int i = 0; i < all.Count && i < max; i++)
            list.Add(new JObject { ["id"] = all[i].Id, ["name"] = all[i].Name, ["item"] = all[i].WallpaperItemId, ["skin"] = all[i].SkinIndex });
        return new JObject { ["total"] = all.Count, ["surfaces"] = list };
    }

    [BridgeCommand("surfaces.set", "Put a wallpaper, paint or panel item on a surface, or scrape it bare (item 0).",
        Params = new[] { "id: surface id (see surfaces.list)", "item: covering item id, 0 to clear", "skin?: variant index, default 0" })]
    private static object SurfacesSet(JObject p)
    {
        string id = p.Str("id");
        int item = p.Int("item", 0);
        bool placed;
        if (item <= 0)
            placed = Surfaces.Clear(id);
        else
            placed = Surfaces.SetCovering(id, item, p.Int("skin", 0));
        var s = Surfaces.Describe(Surfaces.Find(id));
        return new JObject { ["id"] = s.Id, ["item"] = s.WallpaperItemId, ["skin"] = s.SkinIndex, ["accepted"] = placed };
    }
}
