// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
using System;
using ObenFramework.Patching;
using ObenFramework.Player;
using ObenLoader;
using ObenLoader.Runtime;

namespace ObenFramework.Gambling;

/// <summary>
/// Scales what gambling pays out. Mods set multipliers per game (or for <see cref="GambleKind.All"/>); every
/// mod's multipliers multiply together, so two mods that each double winnings make them four times, and when
/// one goes away the other's stays. Applied where the game works out a win: slot machine and pajatso wins,
/// card table payouts, rat fight winnings and scratch card prizes. The game's gambling statistics count what
/// was actually paid; stakes and losses are not changed. Multipliers are not saved; set them in <c>OnLoad</c>.
/// </summary>
public static class Payouts
{
    /// <summary>True when the payout hooks are in place, so multipliers take effect.</summary>
    public static bool IsAvailable => PatchAreas.IsAvailable(PatchAreas.Framework, "Gambling");

    /// <summary>Largest multiplier one mod may set, and the cap on a game's combined multiplier.</summary>
    public const float MaxMultiplier = 10f;

    internal static readonly MultiplierTable Table =
        new MultiplierTable(Enum.GetValues(typeof(GambleKind)).Length, MaxMultiplier);

    private static readonly OwnerWatch Owners = new OwnerWatch(id => Table.RemoveOwner(id));

    /// <summary>
    /// Sets this mod's multiplier on a game's winnings: 1 changes nothing, 2 doubles, 0.5 halves, 0 pays
    /// nothing. The same owner, game and <paramref name="key"/> again replaces the earlier value. Dispose the
    /// result (or call <see cref="Remove"/>) to take it away; everything goes when the mod unloads.
    /// </summary>
    public static IDisposable Set(ObenMod owner, GambleKind game, float multiplier, string key = null)
    {
        Check(game);
        if (!Table.Accepts(multiplier))
            throw new ArgumentOutOfRangeException(nameof(multiplier), $"A multiplier must be a number from 0 to {MaxMultiplier}.");
        string id = Owners.Watch(owner);
        Table.Set(id, (int)game, key, multiplier);
        return Disposable.Create(() => Table.Remove(id, (int)game, key));
    }

    /// <summary>Takes this mod's multiplier on a game away. False when it had none.</summary>
    public static bool Remove(ObenMod owner, GambleKind game, string key = null)
    {
        if (owner == null)
            throw new ArgumentNullException(nameof(owner));
        Check(game);
        return Table.Remove(owner.Info.Id, (int)game, key);
    }

    /// <summary>Takes every multiplier of this mod away, on all games. Returns how many there were.</summary>
    public static int Clear(ObenMod owner) => owner == null ? 0 : Table.RemoveOwner(owner.Info.Id);

    /// <summary>What a game's wins are multiplied by right now: the <see cref="GambleKind.All"/> multipliers times the game's own, capped.</summary>
    public static float Multiplier(GambleKind game)
    {
        Check(game);
        if (Table.IsEmpty)
            return 1f;
        return game == GambleKind.All
            ? Table.Product(0)
            : PayoutMath.Combine(Table.Product((int)GambleKind.All), Table.Product((int)game), MaxMultiplier);
    }

    /// <summary>Every multiplier mods have set. <see cref="MultiplierEntry.Channel"/> is a <see cref="GambleKind"/>.</summary>
    public static MultiplierEntry[] Active => Table.Entries;

    /// <summary>A whole payout of a game after the multipliers.</summary>
    internal static int Apply(GambleKind game, int amount) =>
        Table.IsEmpty ? amount : PayoutMath.Scale(amount, Multiplier(game));

    internal static void Reset()
    {
        Table.Clear();
        Owners.Clear();
    }

    private static void Check(GambleKind game)
    {
        if ((int)game < 0 || (int)game >= Table.Channels)
            throw new ArgumentOutOfRangeException(nameof(game));
    }
}
