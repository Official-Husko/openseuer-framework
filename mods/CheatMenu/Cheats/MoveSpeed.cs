using ObenFramework.Player;
using ObenLoader;

namespace CheatMenu.Cheats;

/// <summary>
/// Scales the player's walking, running, swimming and sneaking speed through the framework's
/// movement multipliers, so other mods that change speed combine with it instead of being overwritten.
/// The framework re-applies it to the controller the game builds for every level and drops it when
/// this mod unloads.
/// </summary>
internal static class MoveSpeed
{
    public const float Min = 0.5f;
    public const float Max = 5f;

    private static ObenMod _owner;

    /// <summary>The multiplier now in effect, 1 is the game's own speed.</summary>
    public static float Factor { get; private set; } = 1f;

    public static bool Available => PlayerRef.Exists;

    /// <summary>Remembers the mod the multiplier belongs to and applies the saved factor.</summary>
    public static void Init(ObenMod owner, float factor)
    {
        _owner = owner;
        Factor = 1f;
        Set(factor);
    }

    public static void Set(float factor)
    {
        Factor = UnityEngine.Mathf.Clamp(factor, Min, Max);
        if (Factor == 1f)
            PlayerMovement.Remove(_owner, MovementStat.Speed);
        else
            PlayerMovement.Set(_owner, MovementStat.Speed, Factor);
    }
}
