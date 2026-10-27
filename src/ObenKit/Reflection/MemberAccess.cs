using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace ObenKit.Reflection;

/// <summary>
/// Get, set and call members by name, public or not, instance or static. Lookups are cached per
/// type and name so repeated tool calls stay cheap.
/// </summary>
public static class MemberAccess
{
    public const BindingFlags All = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance |
                                    BindingFlags.Static | BindingFlags.FlattenHierarchy;

    private static readonly Dictionary<(Type, string), MemberInfo> Members = new Dictionary<(Type, string), MemberInfo>();
    private static readonly Dictionary<(Type, string), MethodInfo[]> Methods = new Dictionary<(Type, string), MethodInfo[]>();

    /// <summary>Field or property named <paramref name="name"/>, searching base classes too.</summary>
    public static MemberInfo FindMember(Type type, string name)
    {
        var key = (type, name);
        if (Members.TryGetValue(key, out var member))
            return member;

        for (var t = type; t != null && member == null; t = t.BaseType)
            member = (MemberInfo)t.GetField(name, All | BindingFlags.DeclaredOnly) ?? t.GetProperty(name, All | BindingFlags.DeclaredOnly);

        if (member == null)
            throw new MissingMemberException($"{type.Name} has no field or property '{name}'.");
        Members[key] = member;
        return member;
    }

    public static Type MemberType(MemberInfo member) => member switch
    {
        FieldInfo f => f.FieldType,
        PropertyInfo p => p.PropertyType,
        _ => throw new ArgumentException("Not a field or property."),
    };

    public static object Get(object target, MemberInfo member) => member switch
    {
        FieldInfo f => f.GetValue(target),
        PropertyInfo p => p.GetValue(target, null),
        _ => throw new ArgumentException("Not a field or property."),
    };

    public static void Set(object target, MemberInfo member, object value)
    {
        switch (member)
        {
            case FieldInfo f:
                f.SetValue(target, value);
                break;
            case PropertyInfo p when p.CanWrite:
                p.SetValue(target, value, null);
                break;
            default:
                throw new InvalidOperationException($"{member.Name} is read-only.");
        }
    }

    /// <summary>All overloads of a method, base classes included.</summary>
    public static MethodInfo[] FindMethods(Type type, string name)
    {
        var key = (type, name);
        if (Methods.TryGetValue(key, out var methods))
            return methods;

        var found = new List<MethodInfo>();
        for (var t = type; t != null; t = t.BaseType)
            found.AddRange(t.GetMethods(All | BindingFlags.DeclaredOnly).Where(m => m.Name == name));

        if (found.Count == 0)
            throw new MissingMethodException($"{type.Name} has no method '{name}'.");
        methods = found.ToArray();
        Methods[key] = methods;
        return methods;
    }

    /// <summary>Readable fields and properties declared on the type and its bases (Unity base classes excluded).</summary>
    public static IEnumerable<MemberInfo> Inspectable(Type type)
    {
        for (var t = type; t != null && !IsUnityBase(t); t = t.BaseType)
        {
            foreach (var f in t.GetFields(All | BindingFlags.DeclaredOnly))
            {
                if (!f.Name.Contains("<")) // Skip compiler-generated backing fields.
                    yield return f;
            }
            foreach (var p in t.GetProperties(All | BindingFlags.DeclaredOnly))
            {
                if (p.CanRead && p.GetIndexParameters().Length == 0)
                    yield return p;
            }
        }
    }

    private static bool IsUnityBase(Type t) =>
        t == typeof(UnityEngine.MonoBehaviour) || t == typeof(UnityEngine.Behaviour) ||
        t == typeof(UnityEngine.Component) || t == typeof(UnityEngine.Object) || t == typeof(object) ||
        t == typeof(UnityEngine.ScriptableObject);
}
