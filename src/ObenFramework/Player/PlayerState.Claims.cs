// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
using System;
using ObenFramework.Events;
using ObenLoader;
using ObenLoader.Runtime;

namespace ObenFramework.Player;

public static partial class PlayerState
{
    private static readonly FlagClaim GodmodeClaim = new FlagClaim("god mode", s => s.Godmode, (s, on) => s.Godmode = on);
    private static readonly FlagClaim FrozenClaim = new FlagClaim("frozen needs", s => s.StatsProgressDisabled, (s, on) => s.StatsProgressDisabled = on);

    /// <summary>
    /// The game's "stats progress disabled" switch (the console's toggle): hunger, thirst, tiredness and
    /// the other needs stop changing. Saved with the game like god mode.
    /// </summary>
    public static bool NeedsFrozen
    {
        get => PlayerStats.instance != null && PlayerStats.instance.StatsProgressDisabled;
        set
        {
            if (PlayerStats.instance == null)
                throw new InvalidOperationException("There is no player right now (main menu or loading).");
            PlayerStats.instance.StatsProgressDisabled = value;
        }
    }

    /// <summary>The character's first name, or null at the main menu.</summary>
    public static string FirstName => PlayerIdentity.identity != null ? PlayerIdentity.identity.firstName : null;

    public static string LastName => PlayerIdentity.identity != null ? PlayerIdentity.identity.lastName : null;

    /// <summary>
    /// Keeps god mode on for as long as <paramref name="owner"/> wants it: across level loads, and out of
    /// the save files (it is switched back to what it was while the game writes a save, so removing the mod
    /// doesn't leave a save stuck in god mode). When the last mod lets go, god mode goes back to what it was
    /// before the first one took it. Dispose the result or unload the mod to let go.
    /// </summary>
    public static IDisposable HoldGodmode(ObenMod owner) => GodmodeClaim.Hold(owner);

    /// <summary>Like <see cref="HoldGodmode"/> for <see cref="NeedsFrozen"/>.</summary>
    public static IDisposable HoldNeedsFrozen(ObenMod owner) => FrozenClaim.Hold(owner);

    /// <summary>Mods hold god mode through <see cref="HoldGodmode"/>.</summary>
    public static bool GodmodeHeld => GodmodeClaim.Held;

    /// <summary>Mods hold frozen needs through <see cref="HoldNeedsFrozen"/>.</summary>
    public static bool NeedsFrozenHeld => FrozenClaim.Held;

    internal static void InitClaims(ObenMod framework)
    {
        GameEvents.Saving.Subscribe(framework, () =>
        {
            GodmodeClaim.Park();
            FrozenClaim.Park();
        });
        GameEvents.Saved.Subscribe(framework, _ =>
        {
            GodmodeClaim.Unpark();
            FrozenClaim.Unpark();
        });
        GameEvents.Loaded.Subscribe(framework, Reapply);
        GameEvents.WorldReady.Subscribe(framework, _ => Reapply());
        framework.Track(Disposable.Create(() =>
        {
            GodmodeClaim.Clear();
            FrozenClaim.Clear();
        }));
    }

    private static void Reapply()
    {
        GodmodeClaim.Reapply();
        FrozenClaim.Reapply();
    }
}
