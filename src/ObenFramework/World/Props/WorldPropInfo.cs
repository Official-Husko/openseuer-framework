// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
using UnityEngine;

namespace ObenFramework.World.Props;

/// <summary>A prop as kept in the save, and whether it is in the world right now.</summary>
public sealed class WorldPropInfo
{
    internal WorldPropInfo(PropRecord record, GameObject live)
    {
        ModId = record.Mod;
        PropId = record.Id;
        Scene = record.Scene;
        Kind = record.Kind;
        Source = record.Kind == WorldPropKind.Model || record.Kind == WorldPropKind.Furniture ? record.Key : record.Alt.ToString();
        Position = PropPose.Vector(record.Position);
        Rotation = PropPose.Quaternion(record.Rotation);
        Scale = PropPose.Vector(record.Scale);
        Collider = record.Collider;
        IsStatic = record.Static;
        InteractKey = record.InteractKey;
        Title = record.Title;
        Data = record.Data;
        Object = live;
    }

    public string ModId { get; }
    public string PropId { get; }
    public string Scene { get; }
    public WorldPropKind Kind { get; }

    /// <summary>Model name, furniture id, primitive number or item id, by <see cref="Kind"/>.</summary>
    public string Source { get; }

    public Vector3 Position { get; }
    public Quaternion Rotation { get; }
    public Vector3 Scale { get; }
    public bool Collider { get; }
    public bool IsStatic { get; }
    public string InteractKey { get; }
    public string Title { get; }
    public string Data { get; }

    /// <summary>The live object, or null when the prop's scene isn't loaded or it couldn't be built.</summary>
    public GameObject Object { get; }

    public bool Live => Object != null;
}

/// <summary>Raised by <see cref="WorldProps.Use"/> handlers: the player used a prop.</summary>
public sealed class WorldPropUse
{
    internal WorldPropUse(PropRecord record, GameObject live)
    {
        ModId = record.Mod;
        Key = record.InteractKey;
        PropId = record.Id;
        Scene = record.Scene;
        Data = record.Data;
        Object = live;
    }

    public string ModId { get; }

    /// <summary>The interact key the handler was registered for.</summary>
    public string Key { get; }

    public string PropId { get; }
    public string Scene { get; }
    public string Data { get; }
    public GameObject Object { get; }
}

/// <summary>Converts between the saved arrays and Unity vectors.</summary>
internal static class PropPose
{
    public static Vector3 Vector(float[] v) => new Vector3(v[0], v[1], v[2]);

    public static Quaternion Quaternion(float[] q) => new Quaternion(q[0], q[1], q[2], q[3]);

    public static float[] Array(Vector3 v) => new[] { v.x, v.y, v.z };

    public static float[] Array(Quaternion q) => new[] { q.x, q.y, q.z, q.w };

    public static float[] Array(Color c) => new[] { c.r, c.g, c.b, c.a };

    public static Color Color(float[] c) => new Color(c[0], c[1], c[2], c[3]);
}
