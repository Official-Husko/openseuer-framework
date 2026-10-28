// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
using System.Collections.Generic;

namespace ObenFramework.World.Props;

/// <summary>What a world prop is built from.</summary>
public enum WorldPropKind
{
    /// <summary>A model loaded through <c>ModelRegistry</c>, named <c>modId/key</c>.</summary>
    Model = 0,

    /// <summary>A Unity primitive (cube, sphere, cylinder, plane).</summary>
    Primitive = 1,

    /// <summary>The look of a piece of furniture, without its behaviour.</summary>
    Furniture = 2,

    /// <summary>The world model of an item, without its behaviour.</summary>
    Item = 3,

    /// <summary>A point or spot light.</summary>
    Light = 4,

    /// <summary>An empty object, for example as a use point or a marker for other code.</summary>
    Anchor = 5,
}

/// <summary>The Unity primitives a prop can be.</summary>
public enum WorldPrimitive
{
    Cube = 0,
    Sphere = 1,
    Cylinder = 2,
    Plane = 3,
}

/// <summary>The lights a prop can be.</summary>
public enum WorldPropLight
{
    Point = 0,
    Spot = 1,
}

/// <summary>
/// One prop as kept in the save: everything needed to build it again. Vectors are plain arrays so the
/// record has no Unity types and serializes the same everywhere.
/// </summary>
public sealed class PropRecord
{
    public string Mod;
    public string Id;
    public string Scene;
    public WorldPropKind Kind;

    /// <summary>Model name (<c>modId/key</c>) or furniture id; empty for the other kinds.</summary>
    public string Key = "";

    /// <summary>Primitive shape, furniture alternative or item id, by kind.</summary>
    public int Alt;

    public float[] Position = { 0f, 0f, 0f };
    public float[] Rotation = { 0f, 0f, 0f, 1f };
    public float[] Scale = { 1f, 1f, 1f };
    public bool Collider = true;
    public bool Static;
    public string InteractKey = "";

    /// <summary>Prompt title shown while looking at a usable prop; empty uses the id.</summary>
    public string Title = "";

    public string Data = "";

    public WorldPropLight LightKind;
    public float[] LightColor = { 1f, 1f, 1f, 1f };
    public float LightRange;
    public float LightIntensity;
    public float LightSpotAngle;

    internal PropRecord Copy()
    {
        var copy = (PropRecord)MemberwiseClone();
        copy.Position = (float[])Position?.Clone();
        copy.Rotation = (float[])Rotation?.Clone();
        copy.Scale = (float[])Scale?.Clone();
        copy.LightColor = (float[])LightColor?.Clone();
        return copy;
    }
}

/// <summary>Every prop of every mod in one save, kept in the framework's own save file.</summary>
public sealed class PropFile
{
    public int Version = 1;
    public List<PropRecord> Props = new List<PropRecord>();
}
