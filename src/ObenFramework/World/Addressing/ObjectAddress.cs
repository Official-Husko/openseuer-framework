using System;
using System.Text;
using UnityEngine;

namespace ObenFramework.World.Addressing;

/// <summary>
/// A stable way to point at one component of a scene object, across sessions and between machines.
/// It carries the scene name, the component type, the game's own GUID where the object has one and the
/// hierarchy path. A GUID is tried first (the game sets them in the editor for doors, buttons, lights and
/// most other scene objects); the path is the fallback, with <c>#n</c> after a segment to tell apart
/// siblings that share a name (the second "Door" under a parent is <c>Door#1</c>).
/// </summary>
/// <remarks>
/// The text form is <c>scene|Type|guid|path</c>, so it travels as a plain string in a message or a
/// JSON file. Objects spawned at runtime (placed furniture, dropped items) have neither: placed furniture
/// has its own ids in <see cref="PlacedFurniture"/>.
/// </remarks>
public readonly struct ObjectAddress : IEquatable<ObjectAddress>
{
    public ObjectAddress(string scene, string type, string guid, string path)
    {
        Scene = scene ?? "";
        Type = type ?? "";
        Guid = string.IsNullOrEmpty(guid) ? null : guid;
        Path = path ?? "";
    }

    /// <summary>The scene the object lives in ("DontDestroyOnLoad" for objects that survive level changes).</summary>
    public string Scene { get; }

    /// <summary>The component's class name, such as <c>Door</c>.</summary>
    public string Type { get; }

    /// <summary>The game's GUID of the object, or null when it has none.</summary>
    public string Guid { get; }

    /// <summary>
    /// Hierarchy path from the scene root, segments separated by <c>/</c>. A character that is special
    /// in the text form inside a name (<c>%</c>, <c>|</c>, <c>/</c>, <c>#</c>) is percent-escaped.
    /// </summary>
    public string Path { get; }

    public bool IsEmpty => string.IsNullOrEmpty(Path) && Guid == null;

    /// <summary>The address of a component, or an empty address for null.</summary>
    public static ObjectAddress Of(Component component) => ObjectResolver.AddressOf(component);

    /// <summary>Reads the <c>scene|Type|guid|path</c> form. Returns false for any other text.</summary>
    public static bool TryParse(string text, out ObjectAddress address)
    {
        address = default;
        if (string.IsNullOrEmpty(text))
            return false;

        var parts = text.Split('|');
        if (parts.Length != 4)
            return false;
        address = new ObjectAddress(Unescape(parts[0]), Unescape(parts[1]), Unescape(parts[2]), parts[3]);
        return !address.IsEmpty;
    }

    /// <summary>The component this address points at in the loaded scenes, or null when it is gone.</summary>
    public Component Resolve() => ObjectResolver.Resolve(this);

    /// <summary>Like <see cref="Resolve()"/>, but only returns a component of type <typeparamref name="T"/>.</summary>
    public T Resolve<T>() where T : Component => Resolve() as T;

    /// <summary>The text form, <c>scene|Type|guid|path</c>.</summary>
    public override string ToString() => string.Concat(Escape(Scene), "|", Escape(Type), "|", Escape(Guid ?? ""), "|", Path);

    public bool Equals(ObjectAddress other) =>
        Scene == other.Scene && Type == other.Type && Guid == other.Guid && Path == other.Path;

    public override bool Equals(object obj) => obj is ObjectAddress other && Equals(other);

    public override int GetHashCode() => (Scene, Type, Guid, Path).GetHashCode();

    /// <summary>Escapes the characters the text form and the path use as separators.</summary>
    internal static string Escape(string value)
    {
        if (value.IndexOfAny(Special) < 0)
            return value;

        var sb = new StringBuilder(value.Length + 8);
        foreach (char c in value)
        {
            switch (c)
            {
                case '%': sb.Append("%25"); break;
                case '|': sb.Append("%7C"); break;
                case '/': sb.Append("%2F"); break;
                case '#': sb.Append("%23"); break;
                default: sb.Append(c); break;
            }
        }
        return sb.ToString();
    }

    internal static string Unescape(string value) =>
        value.IndexOf('%') < 0
            ? value
            : value.Replace("%2F", "/").Replace("%23", "#").Replace("%7C", "|").Replace("%25", "%");

    private static readonly char[] Special = { '%', '|', '/', '#' };
}
