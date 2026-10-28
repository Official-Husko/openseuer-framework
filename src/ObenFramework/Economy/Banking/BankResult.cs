namespace ObenFramework.Economy;

/// <summary>How a bank call went. Anything but <see cref="Ok"/> means nothing changed.</summary>
public enum BankResult
{
    Ok,

    /// <summary>No world is loaded, or the game's money or UI isn't there yet, or a save is being written or read.</summary>
    NotReady,

    /// <summary>No bank branch or account by that name in the loaded scenes.</summary>
    NoBank,

    /// <summary>The player has no account at that bank.</summary>
    NoAccount,

    /// <summary>The account is frozen.</summary>
    Frozen,

    /// <summary>The player, or the account, doesn't have the money.</summary>
    Insufficient,

    /// <summary>The bank itself is short of the currency asked for.</summary>
    BankShort,

    /// <summary>This branch doesn't offer the service (a withdraw-only branch, no exchange desk).</summary>
    NotOffered,

    /// <summary>The amount is too small to give anything back (an exchange that would pay 0).</summary>
    TooSmall,

    /// <summary>The amount or another argument is not acceptable (negative, above a limit, unknown invoice).</summary>
    Refused,

    /// <summary>The game threw; the error is logged.</summary>
    Failed,
}
