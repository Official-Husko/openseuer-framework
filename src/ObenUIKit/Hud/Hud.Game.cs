using System;
using ObenLoader.Runtime;

namespace ObenUIKit;

/// <summary>Hiding the game's own HUD pieces.</summary>
public static partial class Hud
{
    /// <summary>
    /// Hides the crosshair until the result is disposed or <paramref name="owner"/> unloads. Several mods
    /// can ask; it comes back when the last one lets go. Survives level changes.
    /// </summary>
    public static IDisposable HideCrosshair(ITracker owner) => Lease(owner, l => l.HideCrosshair());

    /// <summary>
    /// Hides the game's whole HUD (like its hide-HUD key) until the result is disposed or
    /// <paramref name="owner"/> unloads. The kit's hints, bars and prompts hide with it.
    /// </summary>
    public static IDisposable HideGameHud(ITracker owner) => Lease(owner, l => l.HideHud());

    private static IDisposable Lease(ITracker owner, Func<GameHudLeases, IDisposable> take)
    {
        if (owner == null)
            throw new ArgumentNullException(nameof(owner));
        var leases = GameHudLeases.Current ?? throw new InvalidOperationException("ObenUIKit isn't loaded.");
        return owner.Track(take(leases));
    }
}
