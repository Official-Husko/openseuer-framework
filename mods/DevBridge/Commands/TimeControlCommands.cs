using DevBridge.Server;
using Newtonsoft.Json.Linq;
using ObenFramework.Clock;

namespace DevBridge.Commands;

/// <summary>Clock overrides held by the bridge itself; they go when the bridge unloads or the game returns to the menu.</summary>
internal static class TimeControlCommands
{
    [BridgeCommand("time.control", "Clock overrides: frozen, speed, whether they stand aside for a sleep or wait, and who holds what.")]
    private static object Status(JObject p)
    {
        var holders = new JArray();
        foreach (var pair in TimeControl.Holders())
            holders.Add(new JObject { ["owner"] = pair.Key, ["holds"] = pair.Value });
        return new JObject
        {
            ["available"] = TimeControl.IsAvailable,
            ["frozen"] = TimeControl.IsFrozen,
            ["speed"] = TimeControl.Speed,
            ["suspended"] = TimeControl.IsSuspended,
            ["clock"] = GameClock.IsAvailable ? GameClock.TimeText : null,
            ["holders"] = holders,
        };
    }

    [BridgeCommand("time.freeze", "Freeze or unfreeze in-game time (held by the bridge).", Params = new[] { "on: bool, default true" })]
    private static object Freeze(JObject p)
    {
        if (p.Bool("on", true))
            TimeControl.Freeze(DevBridgeMod.Instance);
        else
            TimeControl.Unfreeze(DevBridgeMod.Instance);
        return Status(p);
    }

    [BridgeCommand("time.speed", "Run in-game time faster or slower (held by the bridge); clear=true drops it.",
        Params = new[] { "speed: 0.01-100, 1 is normal", "clear?: bool" })]
    private static object Speed(JObject p)
    {
        if (p.Bool("clear", false))
            TimeControl.ClearSpeed(DevBridgeMod.Instance);
        else
            TimeControl.SetSpeed(DevBridgeMod.Instance, p.Float("speed"));
        return Status(p);
    }
}
