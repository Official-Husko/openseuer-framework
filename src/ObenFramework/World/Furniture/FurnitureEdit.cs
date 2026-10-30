using UnityEngine;

namespace ObenFramework.World;

/// <summary>What happened to a call of <see cref="FurnitureBuilder"/>: either it worked, or it says why not.</summary>
public readonly struct FurnitureEdit
{
    private FurnitureEdit(bool success, string error, FurnitureInfo furniture)
    {
        Success = success;
        Error = error;
        Furniture = furniture;
    }

    /// <summary>The change was made.</summary>
    public bool Success { get; }

    /// <summary>Why nothing was changed, or null after a success.</summary>
    public string Error { get; }

    /// <summary>
    /// The piece after a place or move; for a remove, the piece as it stood (its object is about to be
    /// destroyed). Default after a failure.
    /// </summary>
    public FurnitureInfo Furniture { get; }

    internal static FurnitureEdit Ok(FurnitureInfo furniture) => new FurnitureEdit(true, null, furniture);

    internal static FurnitureEdit Fail(string error) => new FurnitureEdit(false, error, default);

    public override string ToString() => Success ? "ok" : Error;
}

/// <summary>Optional parts of <see cref="FurnitureBuilder.Place"/>. A default value places the plain piece.</summary>
public struct FurniturePlacement
{
    /// <summary>The skin, 1 and up; 0 is the default look.</summary>
    public int Skin;

    /// <summary>The alternative prefab, 1 and up; 0 is the default.</summary>
    public int Alternative;

    /// <summary>
    /// Local rotation of the first child, the part the build menu's rotate key turns (what
    /// <see cref="FurnitureInfo.ModelRotation"/> reports). Identity when not given.
    /// </summary>
    public Quaternion? ModelRotation;

    /// <summary>
    /// The id to give the new piece, for a machine that has to agree with another on ids. A fresh id is
    /// made when null. It must not belong to another piece.
    /// </summary>
    public string Id;

    /// <summary>Id of the piece this one stands on or in, so it counts as its child (it is taken with it).</summary>
    public string ParentId;

    /// <summary>Id of the piece this one hangs on (a shelf on a wall unit).</summary>
    public string AttachedToId;

    /// <summary>
    /// Refuse a piece the build menu doesn't offer and no mod defined (<see cref="FurnitureKinds.IsKnown"/>), such
    /// as an asset path only a scene uses. For placing what another machine asks for.
    /// </summary>
    public bool KnownOnly;
}

/// <summary>A building zone: one of the game's furniture managers (an apartment, a workshop, a shop).</summary>
public readonly struct FurnitureZone
{
    public FurnitureZone(string id, string scene, Vector3 position, int placedCount, bool isActive)
    {
        Id = id;
        Scene = scene;
        Position = position;
        PlacedCount = placedCount;
        IsActive = isActive;
    }

    /// <summary>The manager's GUID, which is what <see cref="FurnitureInfo.Manager"/> and the zone arguments use.</summary>
    public string Id { get; }

    public string Scene { get; }

    public Vector3 Position { get; }

    /// <summary>How many pieces the player placed here.</summary>
    public int PlacedCount { get; }

    /// <summary>The zone the local player is building in right now.</summary>
    public bool IsActive { get; }
}
