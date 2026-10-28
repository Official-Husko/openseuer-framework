using System;
using HarmonyLib;
using ObenFramework.World;
using Currency = Money.Currency;

namespace ObenFramework.Gambling;

/// <summary>
/// Scales one payout the game is about to make, without rewriting the method that makes it. A caller that
/// knows the game will next pay exactly <c>expected</c> (money in a currency, or a count of an item) arms the
/// scope just before; the matching <c>Money.AddMoney</c> or <c>ItemOperations.AddItems</c> call then pays the
/// scaled amount instead and disarms it. The caller always disarms afterwards, so a payout that didn't
/// happen can't change a later one. Used for the card tables, whose payout sits inside a compiler-generated
/// async state machine that is fragile to patch directly.
/// </summary>
[HarmonyPatch]
internal static class PayoutScope
{
    private static bool _armed;
    private static Item _item;
    private static Currency _currency;
    private static int _expected;
    private static int _scaled;

    public static void ArmMoney(Currency currency, int expected, int scaled) => Arm(null, currency, expected, scaled);

    public static void ArmItems(Item item, int expected, int scaled) => Arm(item, Currency.OC, expected, scaled);

    public static void Disarm()
    {
        _armed = false;
        _item = null;
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
        if (!_armed || _item == null || !ReferenceEquals(item, _item) || amount != _expected)
            return;
        Disarm();
        amount = _scaled;
    }

    internal static void Fail(Exception e)
    {
        Disarm();
        WorldLog.Failed("Payout scaling", e);
    }
}
