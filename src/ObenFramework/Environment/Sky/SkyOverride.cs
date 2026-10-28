using UnityEngine;

namespace ObenFramework.Env;

/// <summary>
/// The parts of the sky a mod wants to set. Leave a part null to keep the game's value; each part comes
/// from the newest mod that sets it. Colours are ordinary colours, the same kind <see cref="WorldSky.Current"/>
/// reads back; Unity converts them for the game's linear rendering itself.
/// </summary>
public sealed class SkyOverride
{
    public bool? FogEnabled { get; set; }

    public FogMode? FogMode { get; set; }

    public Color? FogColor { get; set; }

    /// <summary>Density for the exponential fog modes (the game uses exponential squared, about 0.01 outdoors).</summary>
    public float? FogDensity { get; set; }

    /// <summary>Start distance for linear fog, in metres.</summary>
    public float? FogStart { get; set; }

    /// <summary>End distance for linear fog, in metres.</summary>
    public float? FogEnd { get; set; }

    /// <summary>Ambient light: written to the flat ambient and the sky, equator and ground colours alike.</summary>
    public Color? AmbientColor { get; set; }

    public float? AmbientIntensity { get; set; }

    /// <summary>Brightness of the sky dome (Azure's exposure, or a skybox material's <c>_Exposure</c>).</summary>
    public float? SkyboxExposure { get; set; }

    public Color? SunColor { get; set; }

    public float? SunIntensity { get; set; }

    public float? StarsIntensity { get; set; }

    /// <summary>Cloud cover of the sky dome, 0 to 1.</summary>
    public float? CloudDensity { get; set; }

    internal SkyOverride Copy() => (SkyOverride)MemberwiseClone();

    /// <summary>True when no part is set.</summary>
    public bool IsEmpty =>
        FogEnabled == null && FogMode == null && FogColor == null && FogDensity == null && FogStart == null && FogEnd == null
        && AmbientColor == null && AmbientIntensity == null && SkyboxExposure == null && SunColor == null && SunIntensity == null
        && StarsIntensity == null && CloudDensity == null;
}

/// <summary>What the sky shows right now, mod overrides included. Parts the scene does not have are null.</summary>
public sealed class SkyState
{
    public bool FogEnabled { get; internal set; }

    public FogMode FogMode { get; internal set; }

    public Color FogColor { get; internal set; }

    public float FogDensity { get; internal set; }

    public float FogStart { get; internal set; }

    public float FogEnd { get; internal set; }

    public Color AmbientColor { get; internal set; }

    public float AmbientIntensity { get; internal set; }

    public float? SkyboxExposure { get; internal set; }

    public Color? SunColor { get; internal set; }

    public float? SunIntensity { get; internal set; }

    public float? StarsIntensity { get; internal set; }

    public float? CloudDensity { get; internal set; }
}
