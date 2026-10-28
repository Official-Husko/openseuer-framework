using System;
using DevBridge.Server;
using Newtonsoft.Json.Linq;
using ObenFramework.Env;
using UnityEngine;

namespace DevBridge.Commands;

/// <summary>Screen effects through ObenFramework.Env.ScreenFx, held by the bridge (or the mod named in 'mod').</summary>
internal static class ScreenFxCommands
{
    [BridgeCommand("screenfx", "Shake the camera, hold a vignette, blur or black screen, or clear what the bridge holds.",
        Params = new[]
        {
            "effect: shake, vignette, blur, black or clear",
            "seconds?: shake length, default 1", "amount?: shake metres or blur 0-1, default 0.2 / 0.5",
            "color?: vignette [r,g,b,a], default black", "strength?: vignette 0-1, default 0.6",
            "fade?: seconds to fade the vignette or blur, default 0.5", "speed?: black fade speed, default 0.1",
            "mod?: owner, default the bridge",
        })]
    private static object Fx(JObject p)
    {
        var owner = BridgeOwner.Of(p);
        string effect = p.Str("effect").ToLowerInvariant();
        float fade = p.Float("fade", 0.5f);
        switch (effect)
        {
            case "shake":
                if (!ScreenFx.Shake(p.Float("seconds", 1f), p.Float("amount", 0.2f)))
                    throw new InvalidOperationException("There is no camera to shake here.");
                break;
            case "vignette":
                ScreenFx.Vignette(owner, ColorOf(p["color"] as JArray), p.Float("strength", 0.6f), fade);
                break;
            case "blur":
                ScreenFx.Blur(owner, p.Float("amount", 0.5f), fade);
                break;
            case "black":
                if (!ScreenFx.FadeToBlack(owner, p.Float("speed", 0.1f)))
                    throw new InvalidOperationException("There is no fade canvas here (main menu or loading).");
                break;
            case "clear":
                ScreenFx.ClearVignette(owner, fade);
                ScreenFx.ClearBlur(owner, fade);
                ScreenFx.FadeFromBlack(owner, p.Float("speed", 0.1f));
                break;
            default:
                throw new ArgumentException("effect is shake, vignette, blur, black or clear.");
        }
        return new JObject { ["black"] = ScreenFx.IsHeldBlack, ["vignette"] = ScreenFx.HasVignette, ["blur"] = ScreenFx.HasBlur };
    }

    private static Color ColorOf(JArray c) =>
        c != null && c.Count >= 3 ? new Color(c[0].Value<float>(), c[1].Value<float>(), c[2].Value<float>(), c.Count > 3 ? c[3].Value<float>() : 1f) : Color.black;
}
