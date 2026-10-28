using System;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ObenFramework.World;

/// <summary>What kind of object a lock belongs to.</summary>
public enum LockKind
{
    Door,

    /// <summary>A door that leads to another level (<c>DoorChangelevel</c>).</summary>
    LevelDoor,

    Storage,
    LiquidStorage,

    /// <summary>A crafting station or other machine (<c>CraftingBase</c>).</summary>
    Station,

    /// <summary>A switch or button in the world (the game's own <c>Button</c>).</summary>
    Button,

    SelectionButton,
    PutItemSelection,

    /// <summary>A padlock that can also be broken.</summary>
    BreakableLock,

    /// <summary>A market stall's shutter (<c>MarketShop</c>).</summary>
    MarketStall,
}

/// <summary>
/// The game classes that carry a <c>Lock</c> and how to reach it. The lock is a plain object inside the
/// component, so the list is fixed and each field is read through a cached accessor. Also keeps an index from
/// lock back to its component for the events, built on first use per scene.
/// </summary>
internal static class LockHolders
{
    private const float MissRebuildSeconds = 2f;

    private sealed class Holder
    {
        public Type Type;
        public LockKind Kind;
        public Func<Component, Lock> Get;
    }

    private static readonly Holder[] Table =
    {
        Make<Door>(LockKind.Door, "_Lock"),
        Make<DoorChangelevel>(LockKind.LevelDoor, "_Lock"),
        Make<Storage>(LockKind.Storage, "_Lock"),
        Make<LiquidStorage>(LockKind.LiquidStorage, "_Lock"),
        Make<CraftingBase>(LockKind.Station, "_Lock"),
        Make<Button>(LockKind.Button, "_Lock"),
        Make<SelectionButton>(LockKind.SelectionButton, "_Lock"),
        Make<PutItemSelection>(LockKind.PutItemSelection, "_Lock"),
        Make<BreakableLock>(LockKind.BreakableLock, "_Lock"),
        Make<MarketShop>(LockKind.MarketStall, "marketLock"),
    };

    private static readonly Dictionary<Lock, Component> Owners = new Dictionary<Lock, Component>();
    private static bool _dirty = true;
    private static float _builtAt = float.NegativeInfinity;

    private static Holder Make<T>(LockKind kind, string field) where T : Component
    {
        var get = AccessTools.FieldRefAccess<T, Lock>(field);
        return new Holder { Type = typeof(T), Kind = kind, Get = c => get((T)c) };
    }

    /// <summary>The lock of a component of one of the holder types (exact type or a subclass), or null.</summary>
    public static Lock LockOf(Component component, out LockKind kind)
    {
        kind = default;
        if (component == null)
            return null;
        // Most derived first: DoorChangelevel is not a Door, but a subclass of a holder could be listed later.
        for (int i = 0; i < Table.Length; i++)
        {
            if (Table[i].Type.IsInstanceOfType(component))
            {
                kind = Table[i].Kind;
                return Table[i].Get(component);
            }
        }
        return null;
    }

    /// <summary>The first lock-holding component on a GameObject, or null.</summary>
    public static Component HolderOn(GameObject go)
    {
        if (go == null)
            return null;
        for (int i = 0; i < Table.Length; i++)
        {
            var component = go.GetComponent(Table[i].Type);
            if (component != null && Table[i].Get(component) != null)
                return component;
        }
        return null;
    }

    /// <summary>Every active component with a lock in the loaded scenes. A scan of ten types.</summary>
    public static List<Component> All()
    {
        var list = new List<Component>();
        foreach (var holder in Table)
        {
            foreach (var found in Object.FindObjectsByType(holder.Type, FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            {
                var component = (Component)found;
                if (holder.Get(component) != null)
                    list.Add(component);
            }
        }
        return list;
    }

    /// <summary>The component a lock belongs to, from the per-scene index. Null for a lock it can't place.</summary>
    public static Component OwnerOf(Lock target)
    {
        if (target == null)
            return null;
        if (_dirty)
            Rebuild();
        if (Owners.TryGetValue(target, out var owner) && owner != null)
            return owner;
        // A piece of furniture placed since the index was built; rebuild, but not on every miss.
        if (Time.realtimeSinceStartup - _builtAt >= MissRebuildSeconds)
        {
            Rebuild();
            if (Owners.TryGetValue(target, out owner) && owner != null)
                return owner;
        }
        return null;
    }

    /// <summary>Remembers an owner the game just told us about (a lock being used by its component).</summary>
    public static void Remember(Lock target, Component owner)
    {
        if (target != null && owner != null)
            Owners[target] = owner;
    }

    public static void Reset()
    {
        Owners.Clear();
        _dirty = true;
    }

    private static void Rebuild()
    {
        Owners.Clear();
        foreach (var component in All())
        {
            var found = LockOf(component, out _);
            if (found != null)
                Owners[found] = component;
        }
        _dirty = false;
        _builtAt = Time.realtimeSinceStartup;
    }
}
