namespace ObenFramework.Gambling;

/// <summary>The games whose winnings <see cref="Payouts"/> can scale.</summary>
public enum GambleKind
{
    /// <summary>Every game below at once.</summary>
    All,

    /// <summary>The casino's one-armed bandits (and the odd one in bars and kiosks).</summary>
    SlotMachine,

    /// <summary>The coin pinball machine in the casino.</summary>
    Pajatso,

    /// <summary>The China Hand card game.</summary>
    ChinaHand,

    /// <summary>The Ristiseiska card game.</summary>
    Ristiseiska,

    /// <summary>Bets at the rat fighting arena.</summary>
    RatFight,

    /// <summary>Scratch card prizes (OC Pot and Spade of Aces).</summary>
    Scratchcard,
}
