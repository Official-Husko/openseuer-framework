using System;
using HarmonyLib;
using ObenFramework.World;
using Currency = Money.Currency;

namespace ObenFramework.Gambling;

/// <summary>
/// Scales one payout the game is about to make, without rewriting the method that makes it. A caller that
/// knows the game will next pay exactly <c>expected</c> (money in a currency, or a count of an item) arms the
/// scope just before; the matching <c>Money.AddMoney</c> or <c>ItemOperations.AddItems</c> call then pays the
/// scaled amount instead and disarms it. The win booked in the gambling statistics has a slot of its own,
/// matched on <c>PlayerStatsGambling.UpdateValues</c>. The caller always disarms afterwards, so a payout that
/// didn't happen can't change a later one. Used for the card tables, whose payout sits inside a
/// compiler-generated async state machine that is fragile to patch directly, and for the rat fights' booking.
/// </summary>
[HarmonyPatch]
internal static class PayoutScope
{
    private static bool _armed;
    private static Item _item;
    private static Currency _currency;
    private static int _expected;
    private static int _scaled;
    private static bool _statsArmed;
    private static int _statsExpected;
    private static int _statsScaled;

    public static void ArmMoney(Currency currency, int expected, int scaled) => Arm(null, currency, expected, scaled);

    public static void ArmItems(Item item, int expected, int scaled) => Arm(item, Currency.OC, expected, scaled);

    /// <summary>The next booking of exactly <paramref name="expected"/> won coins counts <paramref name="scaled"/> instead.</summary>
    public static void ArmStats(int expected, int scaled)
    {
        _statsArmed = expected != scaled;
        _statsExpected = expected;
        _statsScaled = scaled;
    }

    public static void Disarm()
    {
        _armed = false;
        _item = null;
        _statsArmed = false;
    }

    private static void Arm(Item item, Currency currency, int expected, int scaled)
    {
        _armed = expected != scaled;
        _item = item;
        _currency = currency;
        _expected = expected;
        _scaled = scaled;
    }

    [HarmonyPatch(typeof(Money), nameof(Money.AddMoney), new[] { typeof(float), typeof(Currency) }), HarmonyPrefix]
    [HarmonyPriority(Priority.First)]
    private static void ScaleMoney(ref float amount, Currency currency)
    {
        if (!_armed || _item != null || currency != _currency || amount != _expected)
            return;
        Disarm();
        amount = _scaled;
    }

    [HarmonyPatch(typeof(ItemOperations), nameof(ItemOperations.AddItems),
        new[] { typeof(Item), typeof(int), typeof(int), typeof(object[]), typeof(SlotController), typeof(bool), typeof(bool) })]
    [HarmonyPrefix]
    [HarmonyPriority(Priority.First)]
    private static void ScaleItems(Item item, ref int amount)
    {
        if (!_armed || _item == null || item == null || item.ID != _item.ID || amount != _expected)
            return;
        Disarm();
        amount = _scaled;
    }

    [HarmonyPatch(typeof(PlayerStatsGambling), nameof(PlayerStatsGambling.UpdateValues), new[] { typeof(int), typeof(int) })]
    [HarmonyPrefix]
    [HarmonyPriority(Priority.First)]
    private static void ScaleBooking(ref int wins)
    {
        if (!_statsArmed || wins != _statsExpected)
            return;
        _statsArmed = false;
        wins = _statsScaled;
    }

    internal static void Fail(Exception e)
    {
        Disarm();
        WorldLog.Failed("Payout scaling", e);
    }
}
