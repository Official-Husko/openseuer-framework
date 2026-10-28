using ObenFramework.Crafting;
using ObenLoader.Runtime;

namespace ObenFramework.Economy;

/// <summary>
/// Per-mod price factors on traders, kept in the shared <see cref="OwnedFactors"/> table: one key per trader
/// address or trader name and direction (<c>{shopId}|buy</c>, <c>name:{name}|sell</c>), with <c>*</c> standing
/// for every trader. Factors of different mods multiply; a mod's factors go away when it unloads. Reading
/// allocates nothing: the per-shop keys are cached on the <see cref="ShopInfo"/>.
/// </summary>
internal static class TraderFactors
{
    public const float Max = 10f;
    public const string AllTraders = "*";

    private static readonly string AllBuy = KeyOf(AllTraders, true);
    private static readonly string AllSell = KeyOf(AllTraders, false);

    private static OwnedFactors _factors = new OwnedFactors();

    public static bool Any => _factors.Table.Any;

    public static string KeyOf(string shopId, bool playerBuys) => shopId + (playerBuys ? "|buy" : "|sell");

    /// <summary>The key part for every trader of a name (names are not unique, so it can cover several).</summary>
    public static string NameKey(string name) => "name:" + (name ?? "").ToLowerInvariant();

    public static void Set(ITracker owner, string shopId, float buy, float sell)
    {
        _factors.Set(owner, KeyOf(shopId, true), buy);
        _factors.Set(owner, KeyOf(shopId, false), sell);
    }

    public static bool Clear(ITracker owner, string shopId)
    {
        bool buy = _factors.Remove(owner, KeyOf(shopId, true));
        bool sell = _factors.Remove(owner, KeyOf(shopId, false));
        return buy || sell;
    }

    /// <summary>The combined factor for one trader and direction, capped at <see cref="Max"/>.</summary>
    public static float For(ShopInfo shop, bool playerBuys)
    {
        var table = _factors.Table;
        if (!table.Any)
            return 1f;
        float product = table.Of(playerBuys ? AllBuy : AllSell);
        if (shop != null)
            product *= table.Of(shop.FactorKey(playerBuys)) * table.Of(shop.FactorNameKey(playerBuys));
        return product > Max ? Max : product;
    }

    public static void Reset() => _factors = new OwnedFactors();
}
