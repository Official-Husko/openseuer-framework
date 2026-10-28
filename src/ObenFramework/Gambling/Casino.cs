// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
using System.Collections.Generic;
using ObenFramework.World;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ObenFramework.Gambling;

/// <summary>
/// The gambling machines: slot machines (ten in the Bazaar casino, single ones in bars, kiosks and the
/// mines) and the pajatso coin pinball in the casino, plus scratch cards. Lists the machines of the loaded
/// scene and reports plays, wins, doubling and cash-outs. Wins can be scaled with <see cref="Payouts"/>.
/// Call on the main thread.
/// </summary>
public static class Casino
{
    /// <summary>A play started: <see cref="CasinoEvent.Amount"/> is the stake taken from the coins in the machine.</summary>
    public static readonly WorldEvent<CasinoEvent> Played = new WorldEvent<CasinoEvent>(nameof(Played));

    /// <summary>A play won: <see cref="CasinoEvent.Amount"/> is the win, after <see cref="Payouts"/>.</summary>
    public static readonly WorldEvent<CasinoEvent> Won = new WorldEvent<CasinoEvent>(nameof(Won));

    /// <summary>A slot machine's coin toss for double or nothing: <see cref="CasinoEvent.Before"/> went in, <see cref="CasinoEvent.Amount"/> came out.</summary>
    public static readonly WorldEvent<CasinoEvent> Doubled = new WorldEvent<CasinoEvent>(nameof(Doubled));

    /// <summary>The player took the machine's coins and winnings out: <see cref="CasinoEvent.Amount"/> went to the wallet.</summary>
    public static readonly WorldEvent<CasinoEvent> CashedOut = new WorldEvent<CasinoEvent>(nameof(CashedOut));

    /// <summary>A scratch card was scratched.</summary>
    public static readonly WorldEvent<ScratchcardResult> Scratched = new WorldEvent<ScratchcardResult>(nameof(Scratched));

    /// <summary>Every slot machine and pajatso of the loaded scenes.</summary>
    public static List<CasinoMachineInfo> Machines()
    {
        var list = new List<CasinoMachineInfo>();
        foreach (var slot in Object.FindObjectsByType<SlotMachineGameplay>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            list.Add(CasinoFields.Describe(slot));
        foreach (var pajatso in Object.FindObjectsByType<PajatsoGameplay>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            list.Add(CasinoFields.Describe(pajatso));
        return list;
    }

    /// <summary>The player's gambling record: total won and lost, addiction and urge. Zero without a player.</summary>
    public static GamblingStats Stats
    {
        get
        {
            var stats = PlayerStats.instance;
            if (stats == null || stats.gambling == null)
                return default;
            var addictions = stats.addictions;
            return new GamblingStats(stats.gambling.OCWon, stats.gambling.OCLost,
                addictions != null ? addictions.GamblingAddiction : 0f, addictions != null ? addictions.GamblingNeed : 0f);
        }
    }

    internal static void Raise(WorldEvent<CasinoEvent> e, Component machine, int amount, int before = 0)
    {
        if (e.HasSubscribers && machine != null)
            e.Raise(new CasinoEvent(CasinoFields.Describe(machine), amount, before));
    }
}
