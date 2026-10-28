using HarmonyLib;
using UnityEngine;

namespace ObenFramework.Gambling;

/// <summary>The private state of the game's gambling machines, bound once.</summary>
internal static class CasinoFields
{
    private static readonly AccessTools.FieldRef<SlotMachineGameplay, int[]> Stakes = AccessTools.FieldRefAccess<SlotMachineGameplay, int[]>("stakes");
    private static readonly AccessTools.FieldRef<SlotMachineGameplay, int> StakeIndex = AccessTools.FieldRefAccess<SlotMachineGameplay, int>("_stakesIndex");
    private static readonly AccessTools.FieldRef<SlotMachineGameplay, int> SlotMoney = AccessTools.FieldRefAccess<SlotMachineGameplay, int>("_moneyInMachine");
    private static readonly AccessTools.FieldRef<SlotMachineGameplay, int> SlotWinnings = AccessTools.FieldRefAccess<SlotMachineGameplay, int>("_currentRunEarnings");
    private static readonly AccessTools.FieldRef<PajatsoGameplay, int> PajatsoMoney = AccessTools.FieldRefAccess<PajatsoGameplay, int>("coinsInMachine");
    private static readonly AccessTools.FieldRef<PajatsoGameplay, int> PajatsoWinnings = AccessTools.FieldRefAccess<PajatsoGameplay, int>("coinsWon");

    /// <summary>The pajatso takes one coin per launch.</summary>
    public const int PajatsoStake = 1;

    public static int StakeOf(SlotMachineGameplay slot)
    {
        var stakes = Stakes(slot);
        int index = StakeIndex(slot);
        return stakes != null && index >= 0 && index < stakes.Length ? stakes[index] : 0;
    }

    public static CasinoMachineInfo Describe(Component machine) => machine switch
    {
        SlotMachineGameplay slot => new CasinoMachineInfo(slot, GambleKind.SlotMachine, StakeOf(slot), SlotMoney(slot), SlotWinnings(slot)),
        PajatsoGameplay pajatso => new CasinoMachineInfo(pajatso, GambleKind.Pajatso, PajatsoStake, PajatsoMoney(pajatso), PajatsoWinnings(pajatso)),
        _ => new CasinoMachineInfo(machine, GambleKind.All, 0, 0, 0),
    };
}
