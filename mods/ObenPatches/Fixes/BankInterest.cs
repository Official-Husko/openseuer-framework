using System;
using HarmonyLib;
using ObenFramework.Features;
using ObenFramework.Saves;
using UnityEngine;

namespace ObenPatches.Fixes;

/// <summary>
/// Bank interest is meant to be paid (and charged on debts) once a week: Money.CurrentTime raises
/// balances by (1 + rate) ^ (days / 7). But it runs every day with days = 1, and the division is
/// an integer one, so the exponent is always 0 and no interest ever happens. The fix applies the
/// same formulas per whole week since interest was last applied, and remembers that day in the
/// save. Counting starts the day the fix is first used, so nothing is charged for the past.
/// </summary>
internal sealed class BankInterest : ModFeature
{
    public const string Key = "BankInterest";

    public override string Id => Key;
    public override string Name => "Bank interest is paid every week";
    public override string Description =>
        "Savings earn the bank's weekly interest and debts grow by theirs, as the banks advertise. Without the fix neither ever happens.";

    /// <summary>The last day interest was applied, kept per save.</summary>
    internal static ModSaveData<BankInterestState> State;
}

/// <summary>Stored next to the save as mods/ObenPatches.bank-interest.json.</summary>
internal sealed class BankInterestState
{
    /// <summary>Game day interest was last applied on; -1 until the fix has seen a day go by.</summary>
    public int LastInterestDay = -1;
}

[HarmonyPatchCategory(BankInterest.Key)]
[HarmonyPatch(typeof(Money), "CurrentTime")]
internal static class BankInterestPatch
{
    private const int DaysPerPeriod = 7;

    // Runs before the game's own daily check and does the interest part properly. Moving the
    // game's last-check day up to today makes its own (always zero) interest step skip itself;
    // everything after it, like loan payments, runs as before.
    private static void Prefix(Money __instance, TimeAndDay Today, TimeAndDay ___lastCheck)
    {
        var saved = BankInterest.State;
        if (saved == null || Today == null || ___lastCheck == null || Today.currentDay <= ___lastCheck.currentDay)
            return;

        try
        {
            var state = saved.Value;
            if (state.LastInterestDay < 0)
                state.LastInterestDay = ___lastCheck.currentDay;

            int weeks = (Today.currentDay - state.LastInterestDay) / DaysPerPeriod;
            if (weeks > 0)
            {
                Apply(__instance, weeks);
                state.LastInterestDay += weeks * DaysPerPeriod;
            }
            ___lastCheck.currentDay = Today.currentDay;
        }
        catch (Exception e)
        {
            Debug.LogException(e);
        }
    }

    // The game's own formulas, with whole weeks as the exponent.
    private static void Apply(Money money, int weeks)
    {
        foreach (var account in money.bankAccounts)
        {
            if (!account.Frozen && account.Interest > 0f)
            {
                float growth = Mathf.Pow(1f + account.Interest, weeks);
                account.OC = (int)(account.OC * growth);
                account.RM = (float)Math.Round(account.RM * growth, 2);
            }
            if (account.CurrentDebt.IsMoreThanZero() && account.DebtInterest > 0f)
            {
                float growth = Mathf.Pow(1f + account.DebtInterest, weeks);
                account.CurrentDebt.OC = (int)(account.CurrentDebt.OC * growth);
                account.CurrentDebt.RM = (float)Math.Round(account.CurrentDebt.RM * growth, 2);
            }
        }
    }
}
