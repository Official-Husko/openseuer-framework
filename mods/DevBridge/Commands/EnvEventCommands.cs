using DevBridge.Server;
using Newtonsoft.Json.Linq;
using ObenFramework.Env;
using ObenFramework.World;

namespace DevBridge.Commands;

/// <summary>Events of ObenFramework.Env and the power side of the world module, recorded for checks in the game.</summary>
internal static class EnvEventCommands
{
    private static readonly EventRecorder Events = new EventRecorder(recorder =>
    {
        var owner = DevBridgeMod.Instance;
        GlobalFlags.FlagChanged.Subscribe(owner, c => recorder.Add("Flag", new JObject
        {
            ["key"] = c.Key, ["active"] = c.Active, ["was"] = c.WasActive, ["exists"] = c.Exists, ["source"] = c.Source.ToString(),
        }));
        Tanks.TankChanged.Subscribe(owner, c => recorder.Add("Tank", new JObject
        {
            ["id"] = c.Id, ["item"] = c.LiquidItemId, ["amount"] = c.Amount, ["wasItem"] = c.PreviousItemId, ["wasAmount"] = c.PreviousAmount,
            ["source"] = c.Source.ToString(),
        }));
        Lanterns.LanternChanged.Subscribe(owner, c => recorder.Add("Lantern", Change(c.Id, c.Kind.ToString(), c.Source)));
        FloodGates.GateChanged.Subscribe(owner, c => recorder.Add("FloodGate", new JObject { ["id"] = c.Id, ["opening"] = c.Opening, ["source"] = c.Source.ToString() }));
        Showers.ShowerChanged.Subscribe(owner, c => recorder.Add("Shower", new JObject { ["id"] = c.Id, ["on"] = c.On, ["source"] = c.Source.ToString() }));
        Saunas.SaunaChanged.Subscribe(owner, c => recorder.Add("Sauna", new JObject
        {
            ["id"] = c.Id, ["on"] = c.On, ["temperature"] = c.Temperature, ["steam"] = c.Steam, ["source"] = c.Source.ToString(),
        }));
        MultiStateLights.StateChanged.Subscribe(owner, c => recorder.Add("MultiStateLight", new JObject
        {
            ["id"] = c.Id, ["on"] = c.On, ["state"] = c.State, ["source"] = c.Source.ToString(),
        }));
        PowerDevices.DeviceChanged.Subscribe(owner, c => recorder.Add("PowerDevice", new JObject
        {
            ["id"] = c.Id, ["device"] = c.DeviceKind.ToString(), ["kind"] = c.Kind.ToString(), ["on"] = c.Info?.IsOn, ["source"] = c.Source.ToString(),
        }));
        Hazards.Entered.Subscribe(owner, e => recorder.Add("HazardEntered", new JObject { ["id"] = e.Id, ["kind"] = e.Kind.ToString() }));
        BlastSites.Detonated.Subscribe(owner, cone => recorder.Add("Detonated", new JObject { ["id"] = BlastSites.Describe(cone).Id }));
        WorldWater.UnderwaterChanged.Subscribe(owner, under => recorder.Add("Underwater", new JObject { ["under"] = under }));
        Surfaces.SurfaceChanged.Subscribe(owner, s => recorder.Add("Surface", new JObject
        {
            ["id"] = s.Id, ["item"] = s.WallpaperItemId, ["skin"] = s.SkinIndex, ["source"] = s.Source.ToString(),
        }));
        WeatherConditions.WeatherChanged.Subscribe(owner, index => recorder.Add("Weather", new JObject { ["index"] = index }));
        GameSettings.Changed.Subscribe(owner, area => recorder.Add("Settings", new JObject { ["area"] = area.ToString() }));
        Blackouts.Started.Subscribe(owner, trigger => recorder.Add("Blackout", new JObject { ["trigger"] = trigger != null ? trigger.name : null }));
        PowerGrids.GridChanged.Subscribe(owner, c => recorder.Add("Grid", new JObject { ["id"] = c.Info.Id, ["kind"] = c.Kind.ToString(), ["source"] = c.Source.ToString() }));
    });

    [BridgeCommand("env.events", "Flag, tank, fixture, power device, grid, hazard, blast, water, surface, weather, settings and blackout events since recording started.",
        Params = new[] { "clear?: forget what was returned, default true" })]
    private static object EnvEvents(JObject p) => Events.Read(p.Bool("clear", true));

    private static JObject Change(string id, string kind, ChangeSource source) =>
        new JObject { ["id"] = id, ["kind"] = kind, ["source"] = source.ToString() };
}
