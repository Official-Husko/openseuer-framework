using System.Collections.Generic;
using System.ComponentModel;

namespace CheatMenu;

/// <summary>Settings in the mod's config.json. Missing values keep these defaults.</summary>
public sealed class CheatMenuConfig
{
    public const int SpotCount = 3;

    /// <summary>The key that opens and closes the cheat window (a Unity key name such as Insert, Home or F2).</summary>
    [Description("The key that opens and closes the cheat window, as a Unity key name such as Insert, Home or F2.")]
    public string Key = "Insert";

    /// <summary>
    /// Move speed multiplier, kept between sessions (1 is the game's own speed). The cheat window sets it, so it
    /// stays off the Settings page.
    /// </summary>
    [Browsable(false)]
    public float MoveSpeed = 1f;

    /// <summary>The saved teleport spots. Always <see cref="SpotCount"/> entries after <see cref="Normalise"/>.</summary>
    [Browsable(false)]
    public SavedSpot[] Spots = new SavedSpot[SpotCount];

    /// <summary>The developer menu's watches, each "Type:member.path".</summary>
    [Browsable(false)]
    public List<string> Watches = new List<string>();

    /// <summary>Makes sure every spot slot exists, whatever the file held.</summary>
    internal void Normalise()
    {
        if (Spots == null || Spots.Length != SpotCount)
        {
            var fixedSpots = new SavedSpot[SpotCount];
            if (Spots != null)
                System.Array.Copy(Spots, fixedSpots, System.Math.Min(Spots.Length, SpotCount));
            Spots = fixedSpots;
        }
        for (int i = 0; i < Spots.Length; i++)
            Spots[i] ??= new SavedSpot();
        Watches ??= new List<string>();
        Watches.RemoveAll(string.IsNullOrWhiteSpace);
        if (string.IsNullOrWhiteSpace(Key))
            Key = "Insert";
        if (float.IsNaN(MoveSpeed) || float.IsInfinity(MoveSpeed))
            MoveSpeed = 1f;
        MoveSpeed = System.Math.Max(Cheats.MoveSpeed.Min, System.Math.Min(Cheats.MoveSpeed.Max, MoveSpeed));
    }
}

/// <summary>A place the player can come back to. It belongs to the scene it was saved in.</summary>
public sealed class SavedSpot
{
    public bool Used;
    public string Scene = "";
    public float X;
    public float Y;
    public float Z;
    public float Yaw;
}
