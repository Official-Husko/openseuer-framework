// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
using System;
using System.Collections.Generic;
using FullSerializer;
using FullSerializer.Internal;

namespace ObenFramework.Saves;

/// <summary>
/// Rejects blobs that would make the game's deserializer create something other than the saved
/// object's own data. The game's reader instantiates whatever <c>$type</c> a file names, so a
/// blob from outside could otherwise ask for a GameObject, a scene component or a type that
/// holds one. Blobs the framework captured itself always pass.
/// </summary>
internal static class SaveBlobTypes
{
    private const int MaxTypeNameChars = 1024;
    private const int MaxTypeParts = 64;

    /// <summary>Why the parsed entry is unsafe to hand to the game, or null.</summary>
    public static string UnsafeReason(fsData node, Type owner)
    {
        if (node.IsList)
        {
            foreach (var item in node.AsList)
            {
                var reason = UnsafeReason(item, owner);
                if (reason != null)
                    return reason;
            }
            return null;
        }
        if (!node.IsDictionary)
            return null;

        var dict = node.AsDictionary;
        if (dict.Count == 2 && dict.ContainsKey("Type") && dict.ContainsKey("Data"))
            return "an entry has a Type and Data pair, which the game's reader turns into a $type";
        if (dict.TryGetValue("$type", out var typeName) && !TypeAllowed(typeName, owner))
            return "an entry names an unknown type, a type that is or holds a Unity type from another assembly, " +
                   "a type with over " + MaxTypeParts + " parts, or a $type that is not text or is over " + MaxTypeNameChars + " characters";

        foreach (var child in dict.Values)
        {
            var reason = UnsafeReason(child, owner);
            if (reason != null)
                return reason;
        }
        return null;
    }

    private static bool TypeAllowed(fsData typeName, Type owner)
    {
        if (!typeName.IsString || typeName.AsString.Length > MaxTypeNameChars)
            return false;
        var type = fsTypeCache.GetType(typeName.AsString);
        return type != null && PartsAllowed(type, owner);
    }

    // A type is its own parts plus its element type, its generic arguments and the item types of
    // any collection it implements. Every part that is a Unity object must come from the game or
    // from the saved object's own assembly.
    private static bool PartsAllowed(Type type, Type owner)
    {
        var parts = new List<Type> { type };
        for (int i = 0; i < parts.Count; i++)
        {
            if (i == MaxTypeParts)
                return false;
            var part = parts[i];
            if (typeof(UnityEngine.Object).IsAssignableFrom(part) &&
                part.Assembly != owner.Assembly && part.Assembly != typeof(SaveController).Assembly)
                return false;

            if (part.HasElementType)
                AddParts(parts, part.GetElementType());
            for (var parent = part; parent != null && parent != typeof(object); parent = parent.BaseType)
                AddParts(parts, parent.GetGenericArguments());
            foreach (var face in part.GetInterfaces())
            {
                if (!face.IsGenericType)
                    continue;
                var definition = face.GetGenericTypeDefinition();
                if (definition == typeof(IEnumerable<>) || definition == typeof(IDictionary<,>))
                    AddParts(parts, face.GetGenericArguments());
            }
        }
        return true;
    }

    private static void AddParts(List<Type> parts, params Type[] found)
    {
        foreach (var part in found)
        {
            if (!parts.Contains(part))
                parts.Add(part);
        }
    }
}
