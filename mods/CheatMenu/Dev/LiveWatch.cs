// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
using System;
using System.Collections.Generic;
using System.Reflection;
using Newtonsoft.Json;
using ObenFramework.Reflection;
using ObenFramework.Values;

namespace CheatMenu.Dev;

/// <summary>
/// A value picked by type and member path, read again whenever the menu refreshes. The first name is a
/// static field or property of the type; an instance member of a game singleton (a type with a static
/// <c>instance</c>) works too and is read through it. Later names follow the value's own type, so
/// "TimeOfDayAzure" with "currentTimeAndDay.hours" works. Lookups go through the framework's cached
/// <see cref="MemberAccess"/>; values are shown the way the bridge shows them (<see cref="JsonValues"/>).
/// </summary>
internal sealed class LiveWatch
{
    private const int MaxText = 160;
    private static readonly string[] SingletonNames = { "instance", "Instance" };

    private readonly MemberInfo _root;
    private readonly string[] _rest;

    private LiveWatch(string key, MemberInfo root, string[] rest)
    {
        Key = key;
        _root = root;
        _rest = rest;
    }

    /// <summary>"Type:path", as kept in the config.</summary>
    public string Key { get; }

    /// <summary>Resolves a watch, or returns null with the reason in <paramref name="error"/>.</summary>
    public static LiveWatch Create(string typeName, string path, out string error)
    {
        string[] parts = DevRules.SplitPath(path, out error);
        if (parts == null)
            return null;
        var type = TypeFinder.Find((typeName ?? "").Trim());
        if (type == null)
        {
            error = $"No type named '{typeName}'.";
            return null;
        }

        MemberInfo first;
        try { first = MemberAccess.FindMember(type, parts[0]); }
        catch (MissingMemberException e)
        {
            error = e.Message;
            return null;
        }

        var rest = new List<string>(parts);
        rest.RemoveAt(0);
        if (!IsStatic(first))
        {
            var singleton = Singleton(type);
            if (singleton == null)
            {
                error = $"{parts[0]} belongs to each {type.Name}, and {type.Name} has no static instance to read it from.";
                return null;
            }
            rest.Insert(0, parts[0]);
            first = singleton;
        }
        return new LiveWatch(DevRules.WatchKey(type.Name, path), first, rest.ToArray());
    }

    /// <summary>The names worth offering for a type: its static members, and an instance's if it is a singleton.</summary>
    public static List<string> MemberNames(string typeName)
    {
        var names = new List<string>();
        var type = TypeFinder.Find((typeName ?? "").Trim());
        if (type == null)
            return names;
        bool singleton = Singleton(type) != null;
        foreach (var member in MemberAccess.Inspectable(type))
        {
            if ((singleton || IsStatic(member)) && !names.Contains(member.Name))
                names.Add(member.Name);
        }
        names.Sort(StringComparer.OrdinalIgnoreCase);
        return names;
    }

    /// <summary>The current value as short text. Never throws.</summary>
    public string Read()
    {
        try
        {
            object value = MemberAccess.Get(null, _root);
            for (int i = 0; i < _rest.Length; i++)
            {
                if (IsMissing(value))
                    return value == null ? "null" : "(destroyed)";
                value = MemberAccess.Get(value, MemberAccess.FindMember(value.GetType(), _rest[i]));
            }
            return DevRules.Cut(JsonValues.ToJson(value, 0).ToString(Formatting.None), MaxText);
        }
        catch (Exception e)
        {
            var inner = e is TargetInvocationException && e.InnerException != null ? e.InnerException : e;
            return DevRules.Cut(inner.GetType().Name + ": " + inner.Message, MaxText);
        }
    }

    private static MemberInfo Singleton(Type type)
    {
        foreach (string name in SingletonNames)
        {
            MemberInfo member;
            try { member = MemberAccess.FindMember(type, name); }
            catch (MissingMemberException) { continue; }
            if (IsStatic(member) && type.IsAssignableFrom(MemberAccess.MemberType(member)))
                return member;
        }
        return null;
    }

    private static bool IsStatic(MemberInfo member) => member switch
    {
        FieldInfo f => f.IsStatic,
        PropertyInfo p => p.GetGetMethod(true)?.IsStatic ?? false,
        _ => false,
    };

    private static bool IsMissing(object value) =>
        value == null || (value is UnityEngine.Object unity && unity == null);
}
