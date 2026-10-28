using UnityEngine;

namespace ObenFramework.World;

/// <summary>A snapshot of one piece of placed furniture.</summary>
public readonly struct FurnitureInfo
{
    public FurnitureInfo(FurniturePlaceable placeable, string id, string typeId, string title, string manager,
        string scene, Vector3 position, Quaternion rotation, Quaternion modelRotation, int skin, int alternative,
        bool preplaced, string parentId, string attachedToId)
    {
        Placeable = placeable;
        Id = id;
        TypeId = typeId;
        Title = title;
        Manager = manager;
        Scene = scene;
        Position = position;
        Rotation = rotation;
        ModelRotation = modelRotation;
        Skin = skin;
        Alternative = alternative;
        Preplaced = preplaced;
        ParentId = parentId;
        AttachedToId = attachedToId;
    }

    /// <summary>The live object. Unity-null once the furniture was taken and destroyed; the other fields stay valid.</summary>
    public FurniturePlaceable Placeable { get; }

    /// <summary>
    /// Our stable id (see <see cref="PlacedFurniture.IdOf"/>). Furniture that came with the scene has the
    /// scene's own GUID as <c>pre:GUID</c>.
    /// </summary>
    public string Id { get; }

    /// <summary>The kind of furniture: the game's <c>Furniture.Id</c> (its id, else its asset path, else its title).</summary>
    public string TypeId { get; }

    public string Title { get; }

    /// <summary>GUID of the <c>FurnitureManager</c> (the apartment, workshop or shop zone) it is placed in.</summary>
    public string Manager { get; }

    public string Scene { get; }

    public Vector3 Position { get; }

    /// <summary>World rotation of the root object.</summary>
    public Quaternion Rotation { get; }

    /// <summary>Local rotation of the first child, which is the part the build menu's rotate key turns.</summary>
    public Quaternion ModelRotation { get; }

    /// <summary>The chosen skin (0 is the default).</summary>
    public int Skin { get; }

    /// <summary>The chosen alternative prefab (0 is the default).</summary>
    public int Alternative { get; }

    /// <summary>It was part of the scene rather than placed by the player.</summary>
    public bool Preplaced { get; }

    /// <summary>Id of the furniture this one stands on or inside, or null.</summary>
    public string ParentId { get; }

    /// <summary>Id of the furniture this one is attached to (a shelf on a wall unit), or null.</summary>
    public string AttachedToId { get; }
}

/// <summary>A piece of furniture placed or removed, as raised by <see cref="PlacedFurniture.FurniturePlaced"/> and <see cref="PlacedFurniture.FurnitureRemoved"/>.</summary>
public readonly struct FurnitureChange
{
    public FurnitureChange(FurnitureInfo furniture, ChangeSource source)
    {
        Furniture = furniture;
        Source = source;
    }

    public FurnitureInfo Furniture { get; }

    public ChangeSource Source { get; }

    public bool ByLocalPlayer => Source == ChangeSource.LocalPlayer;
}

/// <summary>A piece of furniture picked up and put down again with the build menu's move command.</summary>
public readonly struct FurnitureMove
{
    public FurnitureMove(FurnitureInfo before, FurnitureInfo after, ChangeSource source)
    {
        Before = before;
        After = after;
        Source = source;
    }

    /// <summary>Where and how it stood. Its object is gone; the id is the same as <see cref="After"/>'s.</summary>
    public FurnitureInfo Before { get; }

    public FurnitureInfo After { get; }

    public ChangeSource Source { get; }

    public bool ByLocalPlayer => Source == ChangeSource.LocalPlayer;
}
