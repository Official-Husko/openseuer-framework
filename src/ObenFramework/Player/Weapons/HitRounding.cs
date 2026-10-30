namespace ObenFramework.Player;

/// <summary>How a melee hit's damage becomes a whole number again after a multiplier.</summary>
public enum HitRounding
{
    /// <summary>
    /// The fraction is the chance to round up, so the average comes out exact: a hit of 1 at 1.5x does 1 or 2,
    /// half the time each. The default.
    /// </summary>
    Chance,

    /// <summary>To the nearest whole number, halves up: the same hit always does the same damage (1 at 1.5x is always 2).</summary>
    Nearest,
}
