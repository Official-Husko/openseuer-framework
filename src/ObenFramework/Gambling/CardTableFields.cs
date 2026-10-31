using System.Runtime.CompilerServices;
using HarmonyLib;
using Currency = Money.Currency;

namespace ObenFramework.Gambling;

/// <summary>The private state of the game's card tables, bound once.</summary>
internal static class CardTableFields
{
    public static readonly AccessTools.FieldRef<CardGameTable, CardGame> Game = AccessTools.FieldRefAccess<CardGameTable, CardGame>("selectedGame");
    public static readonly AccessTools.FieldRef<CardGameTable, int> Stake = AccessTools.FieldRefAccess<CardGameTable, int>("gameStake");
    public static readonly AccessTools.FieldRef<CardGameTable, Currency> Currency = AccessTools.FieldRefAccess<CardGameTable, Currency>("currency");
    public static readonly AccessTools.FieldRef<CardGameTable, bool> ItemBet = AccessTools.FieldRefAccess<CardGameTable, bool>("useItemAsBet");
    public static readonly AccessTools.FieldRef<CardGameTable, ItemReference> BetItem = AccessTools.FieldRefAccess<CardGameTable, ItemReference>("itemToBet");

    // The table's own netWinnings adds the stake back at the end of a game instead of the payout, so it is
    // always 0 after a finished game; the framework keeps its own count per table.
    private static readonly ConditionalWeakTable<CardGameTable, StrongBox<int>> Net = new ConditionalWeakTable<CardGameTable, StrongBox<int>>();

    public static int NetOf(CardGameTable table) => Net.TryGetValue(table, out var box) ? box.Value : 0;

    public static void AddNet(CardGameTable table, int amount) => Net.GetOrCreateValue(table).Value += amount;

    public static void ResetNet(CardGameTable table) => Net.Remove(table);

    public static GambleKind KindOf(CardGame game) => game switch
    {
        ChinaHand _ => GambleKind.ChinaHand,
        Ristiseiska _ => GambleKind.Ristiseiska,
        _ => GambleKind.All,
    };

    /// <summary>The item staked at a table that plays for items, or null.</summary>
    public static Item BetItemOf(CardGameTable table)
    {
        if (!ItemBet(table))
            return null;
        var reference = BetItem(table);
        return reference != null ? reference.Item : null;
    }
}
