using System;
using DevBridge.Server;
using Newtonsoft.Json.Linq;
using ObenFramework.Env;
using ObenLoader;
using ObenLoader.Mods;

namespace DevBridge.Commands;

/// <summary>Global flags and the player's options, through ObenFramework.Env. Forces and overrides belong to the bridge (or the mod named in 'mod').</summary>
internal static class FlagSettingCommands
{
    [BridgeCommand("flags.list", "Global flags with their values, and the forces in place.", Params = new[] { "filter?: only names containing this text" })]
    private static object FlagsList(JObject p)
    {
        RequireFlags();
        string filter = p.Str("filter", null);
        var flags = new JArray();
        foreach (string key in GlobalFlags.Keys())
        {
            if (filter != null && key.IndexOf(filter, StringComparison.OrdinalIgnoreCase) < 0)
                continue;
            flags.Add(Flag(key));
        }
        var forced = new JArray();
        foreach (var entry in GlobalFlags.Forced())
            forced.Add(new JObject { ["key"] = entry.Key, ["active"] = entry.Value, ["mod"] = entry.OwnerId });
        return new JObject { ["count"] = flags.Count, ["flags"] = flags, ["forced"] = forced };
    }

    [BridgeCommand("flags.get", "One global flag.", Params = new[] { "key: flag name" })]
    private static object FlagsGet(JObject p)
    {
        RequireFlags();
        return Flag(p.Str("key"));
    }

    [BridgeCommand("flags.set", "Set a global flag for good (saved with the game), force it until released, release a force, or remove it.",
        Params = new[] { "key: flag name", "active?: true or false", "mode?: set (default), force, release or remove", "mod?: owner of a force, default the bridge" })]
    private static object FlagsSet(JObject p)
    {
        RequireFlags();
        string key = p.Str("key");
        string mode = p.Str("mode", "set").ToLowerInvariant();
        bool ok = true;
        switch (mode)
        {
            case "set":
                ok = GlobalFlags.Set(key, p.Bool("active", true));
                break;
            case "force":
                ok = GlobalFlags.Force(BridgeOwner.Of(p), key, p.Bool("active", true));
                break;
            case "release":
                if (!GlobalFlags.Release(BridgeOwner.Of(p), key))
                    throw new ArgumentException($"'{key}' is not forced by {BridgeOwner.Of(p).Info.Id}.");
                break;
            case "remove":
                if (!GlobalFlags.Remove(key))
                    throw new ArgumentException($"There is no flag '{key}'.");
                break;
            default:
                throw new ArgumentException("mode is set, force, release or remove.");
        }
        var result = Flag(key);
        result["ok"] = ok;
        return result;
    }

    [BridgeCommand("settings.get", "The player's options (display, graphics, controls, volume) and the overrides mods hold.")]
    private static object SettingsGet(JObject p)
    {
        var result = new JObject { ["hints"] = GameSettings.HintsEnabled, ["effectiveFov"] = GameSettings.EffectiveFov };
        var d = GameSettings.Display;
        if (d != null)
            result["display"] = new JObject
            {
                ["width"] = d.Width, ["height"] = d.Height, ["refreshRate"] = d.RefreshRate, ["mode"] = d.Mode.ToString(),
                ["vSync"] = d.VSync, ["fov"] = d.Fov, ["brightness"] = d.Brightness, ["runInBackground"] = d.RunInBackground, ["language"] = d.Language,
            };
        var g = GameSettings.Graphics;
        if (g != null)
            result["graphics"] = new JObject
            {
                ["preset"] = g.Preset, ["custom"] = g.Custom, ["shadowQuality"] = g.ShadowQuality, ["shadowResolution"] = g.ShadowResolution,
                ["textureResolution"] = g.TextureResolution, ["ssao"] = g.Ssao, ["antialiasing"] = g.Antialiasing, ["postFx"] = g.PostFx,
            };
        var c = GameSettings.Controls;
        if (c != null)
            result["controls"] = new JObject
            {
                ["mouseSensitivity"] = c.MouseSensitivity, ["invertX"] = c.InvertX, ["invertY"] = c.InvertY,
                ["toggleSprint"] = c.ToggleSprint, ["toggleCrouch"] = c.ToggleCrouch,
            };
        var v = GameSettings.Volume;
        if (v != null)
            result["volume"] = new JObject { ["speakerMode"] = v.SpeakerMode, ["master"] = v.Master, ["sound"] = v.Sound, ["music"] = v.Music, ["nasty"] = v.Nasty };
        return result;
    }

    [BridgeCommand("settings.override", "Override the field of view, brightness or mouse sensitivity without touching the options files; clear=true ends it.",
        Params = new[] { "fov?: degrees, 30-120", "brightness?: percent, 0-200", "sensitivity?: slider value, 100-10000", "clear?: end the bridge's overrides", "mod?: owner, default the bridge" })]
    private static object SettingsOverride(JObject p)
    {
        var owner = BridgeOwner.Of(p);
        if (p.Bool("clear", false))
        {
            GameSettings.ClearFov(owner);
            GameSettings.ClearBrightness(owner);
            GameSettings.ClearMouseSensitivity(owner);
            return SettingsGet(p);
        }
        if (p["fov"] == null && p["brightness"] == null && p["sensitivity"] == null)
            throw new ArgumentException("Give fov, brightness, sensitivity or clear.");
        if (p["fov"] != null)
            GameSettings.OverrideFov(owner, p.Float("fov"));
        if (p["brightness"] != null)
            GameSettings.OverrideBrightness(owner, p.Int("brightness", 100));
        if (p["sensitivity"] != null)
            GameSettings.OverrideMouseSensitivity(owner, p.Int("sensitivity", 900));
        return SettingsGet(p);
    }

    private static JObject Flag(string key) => new JObject
    {
        ["key"] = key,
        ["exists"] = GlobalFlags.Exists(key),
        ["active"] = GlobalFlags.Get(key),
        ["forced"] = GlobalFlags.IsForced(key),
        ["data"] = GlobalFlags.GetData(key) is object data ? JToken.FromObject(data.ToString()) : null,
    };

    private static void RequireFlags()
    {
        if (!GlobalFlags.IsAvailable)
            throw new InvalidOperationException("There are no global flags here: not in a gameplay scene.");
    }
}

/// <summary>The mod that owns what a bridge command forces: the one named in 'mod', else the bridge itself.</summary>
internal static class BridgeOwner
{
    public static ObenMod Of(JObject p)
    {
        string id = p.Str("mod", null);
        if (id == null)
            return DevBridgeMod.Instance;
        return ModRegistry.Find(id)?.Instance ?? throw new ArgumentException($"Mod '{id}' isn't loaded.");
    }
}
