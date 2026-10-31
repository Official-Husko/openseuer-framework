// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
using System.Collections.Generic;
using HarmonyLib;
using ObenFramework.Saves;
using ObenFramework.World;
using ObenFramework.World.Addressing;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ObenFramework.Gambling;

/// <summary>
/// Contests: the drinking contest at the Bazaar bar, the sauna contest and bets at the rat fighting arena.
/// Lists what is going on, reports contests starting and ending and money at the arena, and lets a mod make
/// the player give up a drinking or sauna contest. Rat fight winnings can be scaled with <see cref="Payouts"/>;
/// the bar and sauna contests pay through their dialogue, not the framework. Call on the main thread.
/// </summary>
public static class Contests
{
    private static readonly AccessTools.FieldRef<ContestController, ContestController.IContestParticipant> Participant =
        AccessTools.FieldRefAccess<ContestController, ContestController.IContestParticipant>("activeParticipant");

    /// <summary>True when the contest hooks are in place, so the events fire and rat fight winnings can be scaled.</summary>
    public static bool IsAvailable => Payouts.IsAvailable;

    /// <summary>A contest began (a drinking or sauna contest, or a rat fight round).</summary>
    public static readonly WorldEvent<ContestInfo> Started = new WorldEvent<ContestInfo>(nameof(Started));

    /// <summary>A contest ended, or a rat fight round was fought (with or without a bet of the player's).</summary>
    public static readonly WorldEvent<ContestResult> Finished = new WorldEvent<ContestResult>(nameof(Finished));

    /// <summary>The player bet on a rat.</summary>
    public static readonly WorldEvent<RatFightMoney> BetPlaced = new WorldEvent<RatFightMoney>(nameof(BetPlaced));

    /// <summary>The player collected rat fight winnings.</summary>
    public static readonly WorldEvent<RatFightMoney> WinningsCollected = new WorldEvent<RatFightMoney>(nameof(WinningsCollected));

    /// <summary>The drinking or sauna contest going on and the rat fights under way.</summary>
    public static List<ContestInfo> Active()
    {
        var list = new List<ContestInfo>();
        var current = Current();
        if (current != null)
            list.Add(current);
        foreach (var arena in Object.FindObjectsByType<RatFightArena>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
        {
            if (arena.currentState == RatFightArena.State.Fighting)
                list.Add(Describe(arena));
        }
        return list;
    }

    /// <summary>
    /// How much the opponent in the drinking or sauna contest has left, 0 to 1 (they lose at 0), or -1 when no
    /// such contest is on.
    /// </summary>
    public static float OpponentTolerance()
    {
        var controller = ContestController.instance;
        var participant = controller != null && ContestController.ContestActive ? Participant(controller) : null;
        return participant != null ? Mathf.Clamp01(participant.tolerance) : -1f;
    }

    /// <summary>Makes the player give up the drinking or sauna contest going on (a loss). False when none is on.</summary>
    public static bool GiveUp()
    {
        var controller = ContestController.instance;
        if (SaveGate.IsBusy || controller == null || !ContestController.ContestActive || Participant(controller) == null)
            return false;
        controller.StopContest(false);
        return true;
    }

    /// <summary>
    /// How the last contest this session ended (a drinking or sauna contest, or a rat fight round), or null
    /// before the first. The same result <see cref="Finished"/> raised.
    /// </summary>
    public static ContestResult? LastResult { get; private set; }

    /// <summary>
    /// Who takes part: the opponent of the drinking or sauna contest going on, or the rats of a rat fight, in
    /// the arena's order (the order bets and <see cref="RatFightMoney.Fighter"/> use). Empty for a contest
    /// that is over or unknown.
    /// </summary>
    public static List<string> Participants(ContestInfo contest)
    {
        var names = new List<string>();
        if (contest == null)
            return names;
        if (contest.Kind == ContestKind.RatFight)
        {
            foreach (var arena in Object.FindObjectsByType<RatFightArena>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            {
                if (Describe(arena).Id != contest.Id || arena.combatants == null)
                    continue;
                foreach (var combatant in arena.combatants)
                    names.Add(combatant != null ? combatant.name ?? "" : "");
                break;
            }
            return names;
        }
        var current = Current();
        if (current != null && current.Id == contest.Id && !string.IsNullOrEmpty(current.Opponent))
            names.Add(current.Opponent);
        return names;
    }

    internal static void RaiseFinished(ContestResult result)
    {
        LastResult = result;
        Finished.Raise(result);
    }

    /// <summary>The drinking or sauna contest going on, or null.</summary>
    public static ContestInfo Current()
    {
        var controller = ContestController.instance;
        if (controller == null || controller.ActiveContest == null)
            return null;
        return Describe(controller.ActiveContest, Participant(controller));
    }

    internal static ContestInfo Describe(ContestController.IContest contest, ContestController.IContestParticipant participant)
    {
        var kind = contest switch
        {
            DrinkingContest _ => ContestKind.Drinking,
            SaunaContest _ => ContestKind.Sauna,
            _ => ContestKind.Unknown,
        };
        var seat = participant as Component;
        string opponent = participant != null ? NameOf(participant.npcInfo) : null;
        string id = kind + ":" + (seat != null ? ObjectAddress.Of(seat).ToString() : "");
        return new ContestInfo(id, kind, contest.contestName, opponent);
    }

    private static string NameOf(NPCInfo npc)
    {
        if (npc == null)
            return null;
        return npc.character != null && !string.IsNullOrEmpty(npc.character.Name) ? npc.character.Name : npc.name;
    }

    internal static ContestInfo Describe(RatFightArena arena) =>
        new ContestInfo(ContestKind.RatFight + ":" + ObjectAddress.Of(arena), ContestKind.RatFight, "Rat fight", null);
}
