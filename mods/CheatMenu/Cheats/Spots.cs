using ObenFramework.Player;
using UnityEngine;

namespace CheatMenu.Cheats;

/// <summary>Saved teleport spots, kept in the mod's config. A spot only works in the scene it was saved in.</summary>
internal static class Spots
{
    public static string Describe(CheatMenuConfig config, int slot)
    {
        var spot = config.Spots[slot];
        return spot.Used ? $"Spot {slot + 1} ({spot.Scene})" : $"Spot {slot + 1} (empty)";
    }

    public static string[] Labels(CheatMenuConfig config)
    {
        var labels = new string[CheatMenuConfig.SpotCount];
        for (int i = 0; i < labels.Length; i++)
            labels[i] = Describe(config, i);
        return labels;
    }

    /// <summary>Remembers where the player stands and which way they face.</summary>
    public static string Save(CheatContext ctx, int slot)
    {
        var position = PlayerRef.Position ?? throw new System.InvalidOperationException(CheatContext.NeedsGame);
        var spot = ctx.Config.Value.Spots[slot];
        spot.Used = true;
        spot.Scene = PlayerRef.SceneName;
        spot.X = position.x;
        spot.Y = position.y;
        spot.Z = position.z;
        spot.Yaw = PlayerRef.Yaw ?? 0f;
        ctx.Config.Save();
        return $"Saved spot {slot + 1} in {spot.Scene}.";
    }

    public static string Go(CheatContext ctx, int slot)
    {
        var spot = ctx.Config.Value.Spots[slot];
        if (!spot.Used)
            return $"Spot {slot + 1} is empty. Save one first.";
        if (!PlayerRef.Exists)
            throw new System.InvalidOperationException(CheatContext.NeedsGame);
        if (spot.Scene != PlayerRef.SceneName)
            return $"Spot {slot + 1} was saved in {spot.Scene}; you are in {PlayerRef.SceneName}.";
        if (!PlayerTeleport.CanTeleport(out string reason))
            return reason;

        PlayerTeleport.To(new Vector3(spot.X, spot.Y, spot.Z), spot.Yaw);
        return $"Teleported to spot {slot + 1}.";
    }
}
