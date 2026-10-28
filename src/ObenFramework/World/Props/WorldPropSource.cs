// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
using System;
using UnityEngine;

namespace ObenFramework.World.Props;

/// <summary>Options for <see cref="WorldProps.Place"/>. All optional.</summary>
public sealed class WorldPropOptions
{
    /// <summary>Keep or add a collider so the player bumps into the prop. Needed for <see cref="InteractKey"/>.</summary>
    public bool Collider = true;

    /// <summary>Marks the object static. A static prop can't be moved with <see cref="WorldProps.Move"/>.</summary>
    public bool IsStatic;

    /// <summary>
    /// Makes the prop usable: the player sees a prompt and the handler the mod registered with
    /// <see cref="WorldProps.OnUse"/> for this key runs. Up to 64 characters.
    /// </summary>
    public string InteractKey;

    /// <summary>The prompt title shown while looking at a usable prop. Defaults to the prop id.</summary>
    public string Title;

    /// <summary>Free text kept with the prop for the mod's own use, up to 256 characters.</summary>
    public string Data;

    internal WorldPropOptions Copy() => (WorldPropOptions)MemberwiseClone();
}

/// <summary>What a prop is built from. Make one with the static methods.</summary>
public sealed class WorldPropSource
{
    private WorldPropSource(WorldPropKind kind)
    {
        Kind = kind;
    }

    public WorldPropKind Kind { get; }

    internal string Key { get; private set; } = "";
    internal int Alt { get; private set; }
    internal WorldPropLight LightKind { get; private set; }
    internal Color LightColor { get; private set; }
    internal float LightRange { get; private set; }
    internal float LightIntensity { get; private set; }
    internal float LightSpotAngle { get; private set; }

    /// <summary>A model loaded through <c>ModelRegistry</c>, by its full name <c>modId/key</c>.</summary>
    public static WorldPropSource Model(string modelName)
    {
        if (!PropRules.SplitModelKey(modelName, out _, out _))
            throw new ArgumentException("modelName must be modId/key, as ModelRegistry names models", nameof(modelName));
        return new WorldPropSource(WorldPropKind.Model) { Key = modelName };
    }

    public static WorldPropSource Primitive(WorldPrimitive primitive)
    {
        if (primitive < WorldPrimitive.Cube || primitive > WorldPrimitive.Plane)
            throw new ArgumentException("primitive must be Cube, Sphere, Cylinder or Plane", nameof(primitive));
        return new WorldPropSource(WorldPropKind.Primitive) { Alt = (int)primitive };
    }

    /// <summary>
    /// The look of a piece of furniture (id, short name or title, as <c>FurnitureKinds.Find</c> takes it),
    /// optionally one of its alternative models (0 is the default). It is only decoration: none of the
    /// furniture's own behaviour comes along.
    /// </summary>
    public static WorldPropSource Furniture(string furnitureId, int alt = 0)
    {
        if (string.IsNullOrEmpty(furnitureId) || furnitureId.Length > PropRules.MaxKeyLength)
            throw new ArgumentException("furnitureId must be 1 to 128 characters", nameof(furnitureId));
        if (alt < 0)
            throw new ArgumentException("alt must not be negative", nameof(alt));
        return new WorldPropSource(WorldPropKind.Furniture) { Key = furnitureId, Alt = alt };
    }

    /// <summary>The world model of an item, as decoration (it can't be picked up).</summary>
    public static WorldPropSource Item(int itemId)
    {
        if (itemId <= 0)
            throw new ArgumentException("itemId must be a positive item id", nameof(itemId));
        return new WorldPropSource(WorldPropKind.Item) { Alt = itemId };
    }

    public static WorldPropSource Light(WorldPropLight kind, Color color, float range, float intensity = 1f, float spotAngle = 30f)
    {
        string problem = PropRules.LightProblem(kind, range, intensity, spotAngle);
        if (problem != null)
            throw new ArgumentException(problem);
        return new WorldPropSource(WorldPropKind.Light)
        {
            LightKind = kind,
            LightColor = color,
            LightRange = range,
            LightIntensity = intensity,
            LightSpotAngle = spotAngle,
        };
    }

    /// <summary>An empty object: a marker, or with a collider and an interact key, an invisible use point.</summary>
    public static WorldPropSource Anchor() => new WorldPropSource(WorldPropKind.Anchor);
}
