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
    public static readonly AccessTools.FieldRef<CardGameTable, int> NetWinnings = AccessTools.FieldRefAccess<CardGameTable, int>("netWinnings");

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
