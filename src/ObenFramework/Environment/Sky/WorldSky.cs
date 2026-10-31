// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
using System;
using System.Collections.Generic;
using ObenFramework.Patching;
using ObenFramework.World;
using ObenLoader;
using UnityEngine;

namespace ObenFramework.Env;

/// <summary>
/// Sky overrides: fog, ambient light, the sky dome's exposure, stars and clouds, and the sun's colour and
/// strength. A mod sets the parts it wants with <see cref="Set"/>; they are written every frame on top of
/// what the weather and time of day do, and the game's values come back when the mod clears its override
/// or unloads. Overrides last across level changes. Each part comes from the newest mod that sets it.
/// </summary>
public static class WorldSky
{
    private static readonly OwnedValues<SkyOverride> Overrides = new OwnedValues<SkyOverride> { Released = _ => Changed() };
    private static readonly OnceLog Log = new OnceLog("Sky");
    private static bool _rendering;

    /// <summary>What the sky shows now, overrides included. Parts the scene lacks are null.</summary>
    public static SkyState Current
    {
        get
        {
            // The game's sky writes its own values every frame and the overrides go back on just before
            // rendering, so in the middle of a frame the render settings hold the game's values. Put the
            // overrides on first so this reports what is drawn.
            if (!Overrides.IsEmpty || SkyApply.Holding)
                SkyApply.Run();
            return SkyApply.Read();
        }
    }

    /// <summary>
    /// True when the hooks are in place that keep the game's own sky values underneath the overrides, so the
    /// sky goes back to the game's once they are cleared.
    /// </summary>
    public static bool IsAvailable => PatchAreas.IsAvailable(PatchAreas.Framework, "Env");

    /// <summary>True while some mod holds an override.</summary>
    public static bool IsOverridden => !Overrides.IsEmpty;

    /// <summary>Every override, oldest first.</summary>
    public static List<OwnedValue<SkyOverride>> All() => Overrides.Entries;

    /// <summary>Replaces <paramref name="owner"/>'s override with <paramref name="values"/> (a copy is kept).</summary>
    public static void Set(ObenMod owner, SkyOverride values)
    {
        if (owner == null)
            throw new ArgumentNullException(nameof(owner));
        if (values == null)
            throw new ArgumentNullException(nameof(values));
        Validate(values);
        var copy = values.Copy();
        if (copy.CloudDensity.HasValue)
            copy.CloudDensity = Mathf.Clamp01(copy.CloudDensity.Value);
        WarnOverlap(owner.Info.Id, copy);
        Overrides.Set(owner, owner.Info.Id, "", copy);
        Changed();
    }

    /// <summary>Ends <paramref name="owner"/>'s override. True when it had one.</summary>
    public static bool Clear(ObenMod owner)
    {
        if (owner == null || !Overrides.Remove(owner, ""))
            return false;
        Changed();
        return true;
    }

    internal static T? Pick<T>(Func<SkyOverride, T?> field) where T : struct => Overrides.Pick("", field);

    internal static bool Active => !Overrides.IsEmpty;

    /// <summary>A new scene is ready: write the overrides into it.</summary>
    internal static void Reapply()
    {
        if (Active)
            Changed();
    }

    internal static void SceneChanging()
    {
        SkyApply.ForgetScene();
        Sync();
    }

    internal static void Unload()
    {
        Overrides.Clear();
        try
        {
            SkyApply.Run();
        }
        finally
        {
            _rendering = false;
            Application.onBeforeRender -= OnBeforeRender;
        }
    }

    private static void Changed()
    {
        SkyApply.Run();
        Sync();
    }

    // Writes once more right before rendering, after every Update and LateUpdate of the game.
    private static void Sync()
    {
        bool want = !Overrides.IsEmpty || SkyApply.Holding;
        if (want == _rendering)
            return;
        _rendering = want;
        if (want)
            Application.onBeforeRender += OnBeforeRender;
        else
            Application.onBeforeRender -= OnBeforeRender;
    }

    private static void OnBeforeRender()
    {
        try
        {
            SkyApply.Run();
            Sync();
        }
        catch (Exception e)
        {
            WorldLog.Failed("Sky override", e);
        }
    }

    private static void WarnOverlap(string ownerId, SkyOverride values)
    {
        foreach (var other in Overrides.Entries)
        {
            if (other.OwnerId == ownerId)
                continue;
            var o = other.Value;
            bool shared = (o.FogEnabled.HasValue && values.FogEnabled.HasValue) || (o.FogMode.HasValue && values.FogMode.HasValue)
                || (o.FogColor.HasValue && values.FogColor.HasValue) || (o.FogDensity.HasValue && values.FogDensity.HasValue)
                || (o.FogStart.HasValue && values.FogStart.HasValue) || (o.FogEnd.HasValue && values.FogEnd.HasValue)
                || (o.AmbientColor.HasValue && values.AmbientColor.HasValue) || (o.AmbientIntensity.HasValue && values.AmbientIntensity.HasValue)
                || (o.SkyboxExposure.HasValue && values.SkyboxExposure.HasValue) || (o.SunColor.HasValue && values.SunColor.HasValue)
                || (o.SunIntensity.HasValue && values.SunIntensity.HasValue) || (o.StarsIntensity.HasValue && values.StarsIntensity.HasValue)
                || (o.CloudDensity.HasValue && values.CloudDensity.HasValue);
            if (shared)
                Log.Warn("overlap " + other.OwnerId + " " + ownerId, $"{other.OwnerId} and {ownerId} both override parts of the sky; the newest ({ownerId}) shows.");
        }
    }

    private static void Validate(SkyOverride v)
    {
        NotNegative(v.FogDensity, nameof(v.FogDensity));
        Finite(v.FogStart, nameof(v.FogStart));
        Finite(v.FogEnd, nameof(v.FogEnd));
        NotNegative(v.AmbientIntensity, nameof(v.AmbientIntensity));
        NotNegative(v.SkyboxExposure, nameof(v.SkyboxExposure));
        NotNegative(v.SunIntensity, nameof(v.SunIntensity));
        NotNegative(v.StarsIntensity, nameof(v.StarsIntensity));
        Finite(v.CloudDensity, nameof(v.CloudDensity));
        FiniteColor(v.FogColor, nameof(v.FogColor));
        FiniteColor(v.AmbientColor, nameof(v.AmbientColor));
        FiniteColor(v.SunColor, nameof(v.SunColor));
    }

    private static void Finite(float? value, string name)
    {
        if (value.HasValue && (float.IsNaN(value.Value) || float.IsInfinity(value.Value)))
            throw new ArgumentException(name + " must be a finite number.", "values");
    }

    private static void NotNegative(float? value, string name)
    {
        Finite(value, name);
        if (value.HasValue && value.Value < 0f)
            throw new ArgumentException(name + " must not be negative.", "values");
    }

    private static void FiniteColor(Color? value, string name)
    {
        if (!value.HasValue)
            return;
        var c = value.Value;
        Finite(c.r, name);
        Finite(c.g, name);
        Finite(c.b, name);
        Finite(c.a, name);
    }
}
