using ObenFramework.World.Addressing;
using UnityEngine;

namespace ObenFramework.Gambling;

/// <summary>A gambling machine of the loaded scene.</summary>
public sealed class CasinoMachineInfo
{
    internal CasinoMachineInfo(Component machine, GambleKind kind, int stake, int moneyIn, int winnings)
    {
        Machine = machine;
        Id = ObjectAddress.Of(machine).ToString();
        Kind = kind;
        Scene = machine.gameObject.scene.name;
        Stake = stake;
        MoneyIn = moneyIn;
        Winnings = winnings;
    }

    /// <summary>The game component (<c>SlotMachineGameplay</c> or <c>PajatsoGameplay</c>).</summary>
    public Component Machine { get; }

    /// <summary>The machine's address.</summary>
    public string Id { get; }

    /// <summary><see cref="GambleKind.SlotMachine"/> or <see cref="GambleKind.Pajatso"/>.</summary>
    public GambleKind Kind { get; }

    public string Scene { get; }

    /// <summary>Coins per play: the chosen stake on a slot machine, always 1 on the pajatso.</summary>
    public int Stake { get; }

    /// <summary>Coins fed into the machine and not played yet.</summary>
    public int MoneyIn { get; }

    /// <summary>Winnings waiting to be redeemed or collected.</summary>
    public int Winnings { get; }
}

/// <summary>Something happened at a gambling machine; see <see cref="Casino"/> for which event carries what.</summary>
public readonly struct CasinoEvent
{
    internal CasinoEvent(CasinoMachineInfo machine, int amount, int before)
    {
        Machine = machine;
        Amount = amount;
        Before = before;
    }

    public CasinoMachineInfo Machine { get; }

    /// <summary>The stake played, the win (after <see cref="Payouts"/>), the amount after doubling, or the coins cashed out.</summary>
    public int Amount { get; }

    /// <summary>For <see cref="Casino.Doubled"/>: the winnings before the coin toss.</summary>
    public int Before { get; }
}

/// <summary>A scratched card, raised by <see cref="Casino.Scratched"/>.</summary>
public readonly struct ScratchcardResult
{
    internal ScratchcardResult(Item card, int prize, bool realMoney, int cost)
    {
        Card = card;
        Prize = prize;
        RealMoney = realMoney;
        Cost = cost;
    }

    /// <summary>The card item scratched.</summary>
    public Item Card { get; }

    /// <summary>The prize printed on the receipt, 0 for a losing card. A winning receipt is cashed in by talking to the seller.</summary>
    public int Prize { get; }

    /// <summary>True when the prize is in Real Money.</summary>
    public bool RealMoney { get; }

    /// <summary>The card's value.</summary>
    public int Cost { get; }

    public bool Won => Prize > 0;
}

/// <summary>The player's gambling record, from the game's statistics.</summary>
public readonly struct GamblingStats
{
    internal GamblingStats(int won, int lost, float addiction, float need)
    {
        Won = won;
        Lost = lost;
        Addiction = addiction;
        Need = need;
    }

    /// <summary>Coins won in all (Real Money counted as ten coins).</summary>
    public int Won { get; }

    /// <summary>Coins lost in all.</summary>
    public int Lost { get; }

    /// <summary>Gambling addiction, 0 to 100.</summary>
    public float Addiction { get; }

    /// <summary>The urge to gamble, 0 to 100.</summary>
    public float Need { get; }
}
