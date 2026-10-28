namespace CheatMenu;

/// <summary>Settings in the mod's config.json. Missing values keep these defaults.</summary>
public sealed class CheatMenuConfig
{
    public const int SpotCount = 3;

    /// <summary>The key that opens and closes the cheat window (a Unity key name such as Insert, Home or F2).</summary>
    public string Key = "Insert";

    /// <summary>Move speed multiplier, kept between sessions (1 is the game's own speed).</summary>
    public float MoveSpeed = 1f;

    /// <summary>The saved teleport spots. Always <see cref="SpotCount"/> entries after <see cref="Normalise"/>.</summary>
    public SavedSpot[] Spots = new SavedSpot[SpotCount];

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
