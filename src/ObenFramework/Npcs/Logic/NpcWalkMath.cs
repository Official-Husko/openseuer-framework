// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
using System;

namespace ObenFramework.Npcs;

/// <summary>Numbers behind NPC walking orders: timeouts, wander points, patrol order, idle waits. No Unity types.</summary>
internal static class NpcWalkMath
{
    private const float MinSpeed = 0.5f;
    private const float TimeoutFactor = 3f;
    private const float TimeoutSlack = 10f;
    public const float MinWait = 4f;
    public const float MaxWait = 15f;

    /// <summary>
    /// Seconds a walk may take before it counts as stuck: three times the time at the agent's speed plus ten
    /// seconds, with a floor on the speed so a stopped agent still times out.
    /// </summary>
    public static float Timeout(float distance, float speed) =>
        distance / Math.Max(speed, MinSpeed) * TimeoutFactor + TimeoutSlack;

    /// <summary>A uniformly spread point in a disc of <paramref name="radius"/>, as an offset from its centre.</summary>
    public static void DiscPoint(Random random, float radius, out float dx, out float dz)
    {
        double angle = random.NextDouble() * 2.0 * Math.PI;
        double distance = Math.Sqrt(random.NextDouble()) * radius;
        dx = (float)(Math.Cos(angle) * distance);
        dz = (float)(Math.Sin(angle) * distance);
    }

    /// <summary>The next patrol point, going round.</summary>
    public static int NextPatrolIndex(int current, int count) => count <= 0 ? 0 : (current + 1) % count;

    /// <summary>How long a wandering NPC stands before it picks the next point: 4 to 15 seconds.</summary>
    public static float WaitSeconds(Random random) => MinWait + (float)random.NextDouble() * (MaxWait - MinWait);

    /// <summary>
    /// The game's path code treats (0,0,0) as "no target", so a walk to exactly there would never start.
    /// </summary>
    public static bool IsOrigin(float x, float y, float z) => x == 0f && y == 0f && z == 0f;

    /// <summary>The finite-number test the order and routine checks use.</summary>
    public static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
}
