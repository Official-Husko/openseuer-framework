using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using ObenLoader.Config;

namespace ObenFramework.Saves;

/// <summary>
/// Fills in the nested objects a save file left out. A saved member that is null after a load, and whose type is an
/// array or a class that can start empty (a list, a dictionary, a data class with a parameterless constructor), gets
/// an empty one, all the way down through nested objects, list items and dictionary values. Data written by an older
/// version of a mod, or a field without an initializer, so never hands the mod a null where it expects a list.
/// Strings, interfaces and Unity objects stay as they are, and so does a class that holds its own type (filling it
/// would never end). The members are the ones Newtonsoft saves with <see cref="JsonFile.Settings"/>.
/// </summary>
internal static class SaveBlanks
{
    private static readonly IContractResolver Resolver = JsonSerializer.Create(JsonFile.Settings).ContractResolver;
    private static readonly Dictionary<Type, bool> Fillable = new Dictionary<Type, bool>();

    /// <summary>Fills <paramref name="root"/> and everything below it.</summary>
    public static void Fill(object root)
    {
        if (root != null)
            Descend(root, new HashSet<object>(SameObject.Instance));
    }

    /// <summary>Whether a null member of this type gets an empty value.</summary>
    public static bool CanFill(Type type)
    {
        lock (Fillable)
        {
            if (!Fillable.TryGetValue(type, out bool can))
                Fillable[type] = can = Decide(type);
            return can;
        }
    }

    private static void Descend(object value, HashSet<object> seen)
    {
        var type = value.GetType();
        if (value is string || type.IsValueType || value is UnityEngine.Object || !seen.Add(value))
            return;
        switch (Resolver.ResolveContract(type))
        {
            case JsonObjectContract contract when contract.Converter == null:
                FillMembers(value, contract, seen);
                break;
            case JsonDictionaryContract _ when value is IDictionary dictionary:
                foreach (var item in dictionary.Values)
                    DescendItem(item, seen);
                break;
            case JsonArrayContract _ when value is IEnumerable items:
                foreach (var item in items)
                    DescendItem(item, seen);
                break;
        }
    }

    // A null item stays null: a list may hold nulls on purpose, and its element type may not be the one stored.
    private static void DescendItem(object item, HashSet<object> seen)
    {
        if (item != null)
            Descend(item, seen);
    }

    private static void FillMembers(object target, JsonObjectContract contract, HashSet<object> seen)
    {
        foreach (var property in contract.Properties)
        {
            if (property.Ignored || !property.Readable || property.ValueProvider == null)
                continue;
            var member = property.ValueProvider.GetValue(target);
            if (member == null)
            {
                if (!property.Writable || !CanFill(property.PropertyType))
                    continue;
                member = Blank(property.PropertyType);
                property.ValueProvider.SetValue(target, member);
            }
            Descend(member, seen);
        }
    }

    private static object Blank(Type type) =>
        type.IsArray ? Array.CreateInstance(type.GetElementType(), new int[type.GetArrayRank()]) : Activator.CreateInstance(type);

    private static bool Decide(Type type)
    {
        if (type.IsArray)
            return true;
        if (!type.IsClass || type.IsAbstract || type == typeof(string) || type == typeof(object)
            || typeof(UnityEngine.Object).IsAssignableFrom(type) || type.GetConstructor(Type.EmptyTypes) == null)
            return false;
        var contract = Resolver.ResolveContract(type);
        if (contract.Converter != null)
            return false;
        if (contract is JsonArrayContract || contract is JsonDictionaryContract)
            return true;
        return contract is JsonObjectContract members && !Reaches(members, type, new HashSet<Type>());
    }

    // Whether an object of this contract holds a target, directly or through nested classes. Lists and dictionaries
    // don't count: an empty one ends the chain.
    private static bool Reaches(JsonObjectContract contract, Type target, HashSet<Type> seen)
    {
        foreach (var property in contract.Properties)
        {
            var type = property.PropertyType;
            if (property.Ignored || type == null)
                continue;
            if (type == target)
                return true;
            if (type.IsClass && type != typeof(string) && !type.IsArray && seen.Add(type)
                && Resolver.ResolveContract(type) is JsonObjectContract nested && Reaches(nested, target, seen))
                return true;
        }
        return false;
    }

    private sealed class SameObject : IEqualityComparer<object>
    {
        public static readonly SameObject Instance = new SameObject();

        public new bool Equals(object x, object y) => ReferenceEquals(x, y);

        public int GetHashCode(object obj) => RuntimeHelpers.GetHashCode(obj);
    }
}
