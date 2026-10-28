using System.Collections.Generic;

namespace ObenFramework.World.Logic;

/// <summary>
/// Which writes to the level's logic are accepted. The game's own setters take any value; these rules
/// keep a mod from putting a counter, state machine or timer into a state the level never expects.
/// Unity-free so they are covered by unit tests.
/// </summary>
public static class SceneStateRules
{
    /// <summary>Longest interval accepted for a timer: a day.</summary>
    public const float MaxInterval = 86400f;

    /// <summary>A counter can be set to any value from 0 up; the game clamps at its maximum and fires there.</summary>
    public static bool CounterValue(int value) => value >= 0;

    /// <summary>A counter only goes up, by at least one.</summary>
    public static bool CounterStep(int amount) => amount >= 1;

    /// <summary>A state machine only changes to one of its own states.</summary>
    public static bool StateName(IList<string> names, string wanted)
    {
        if (string.IsNullOrEmpty(wanted) || names == null)
            return false;
        foreach (string name in names)
        {
            if (name == wanted)
                return true;
        }
        return false;
    }

    /// <summary>A timer interval is a finite number of seconds above zero and at most <see cref="MaxInterval"/>.</summary>
    public static bool Interval(float seconds) =>
        !float.IsNaN(seconds) && !float.IsInfinity(seconds) && seconds > 0f && seconds <= MaxInterval;

    /// <summary>
    /// Whether a relay would fire if triggered now: it must be active, not used up and its requirements met.
    /// Mirrors the checks at the top of the game's <c>Relay.triggerOutputs</c>.
    /// </summary>
    public static bool RelayCanFire(bool active, bool fireOnceOnly, bool firedOnce, bool requirementsMet) =>
        active && !(fireOnceOnly && firedOnce) && requirementsMet;
}
