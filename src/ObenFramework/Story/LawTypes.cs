// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
namespace ObenFramework.Story;

/// <summary>The game's kinds of crime (Crime.CrimeType), same numbers.</summary>
public enum CrimeKind
{
    Unspecified = 0,
    Theft = 1,
    Violence = 2,
    Fraud = 3,
    PrisonEscape = 4,
    Administrative = 5,
    AnimalCruelty = 6,
    Trespassing = 7,
    Burglary = 8,

    /// <summary>The game's "no kind": used as "every kind" when clearing, never as a crime.</summary>
    None = 9,
    Harassment = 10,
    Vandalism = 11,
    Terrorism = 12,
}

/// <summary>A crime about to go on the player's record. Handlers of <see cref="Law.Intercept"/> may change or cancel it.</summary>
public sealed class CrimeAttempt
{
    internal CrimeAttempt(CrimeKind kind, int severity, int victimId, bool silent)
    {
        Kind = kind;
        Severity = severity;
        VictimId = victimId;
        Silent = silent;
    }

    /// <summary>The kind; changing it to <see cref="CrimeKind.None"/> or an unknown value is ignored.</summary>
    public CrimeKind Kind { get; set; }

    /// <summary>How much it adds to the record (1 to 1,000,000; the game's own crimes are mostly 5 to 500).</summary>
    public int Severity { get; set; }

    /// <summary>The NPC it was against, or -1 for a crime against nobody in particular.</summary>
    public int VictimId { get; }

    /// <summary>No "Crime committed" notification.</summary>
    public bool Silent { get; set; }

    /// <summary>Keeps the crime off the record. Once one handler cancels it stays cancelled.</summary>
    public bool Cancel { get; set; }
}

/// <summary>A crime that went on the record. See <see cref="Law.Committed"/>.</summary>
public readonly struct CrimeCommitted
{
    internal CrimeCommitted(CrimeKind kind, int severity, int victimId)
    {
        Kind = kind;
        Severity = severity;
        VictimId = victimId;
    }

    public CrimeKind Kind { get; }

    public int Severity { get; }

    /// <summary>The NPC it was against, or -1.</summary>
    public int VictimId { get; }
}

/// <summary>One line of the player's crime record, copied when read.</summary>
public readonly struct CrimeRecordLine
{
    internal CrimeRecordLine(CrimeKind kind, int victimId, string victimName, float amount)
    {
        Kind = kind;
        VictimId = victimId;
        VictimName = victimName;
        Amount = amount;
    }

    public CrimeKind Kind { get; }

    /// <summary>The NPC it was against, or -1 for the kind's general line.</summary>
    public int VictimId { get; }

    public string VictimName { get; }

    /// <summary>The severity added up so far; a cleared line reads 0.</summary>
    public float Amount { get; }
}
