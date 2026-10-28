// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
using System;
using System.Collections.Generic;
using HarmonyLib;
using ObenFramework.Saves;
using ObenFramework.World;
using UnityEngine;

namespace ObenFramework.Items;

/// <summary>A snapshot of a resource node: something the player hits with a tool for items (scrap piles, trees, rocks).</summary>
public readonly struct ResourceNodeInfo
{
    public ResourceNodeInfo(string id, Vector3 position, List<int> itemIds, int stock, int maxStock, int ownerId, bool permissionToTake, bool respawns)
    {
        Id = id;
        Position = position;
        ItemIds = itemIds;
        Stock = stock;
        MaxStock = maxStock;
        OwnerId = ownerId;
        PermissionToTake = permissionToTake;
        Respawns = respawns;
    }

    public string Id { get; }

    public Vector3 Position { get; }

    /// <summary>What it can yield.</summary>
    public List<int> ItemIds { get; }

    /// <summary>How many more yields it has before it is depleted.</summary>
    public int Stock { get; }

    public int MaxStock { get; }

    /// <summary>The character it belongs to (taking from it is theft), or 0.</summary>
    public int OwnerId { get; }

    public bool PermissionToTake { get; }

    /// <summary>It refills over time.</summary>
    public bool Respawns { get; }
}

/// <summary>A resource node was hit, raised by <see cref="ResourceNodes.Hit"/>.</summary>
public readonly struct ResourceHit
{
    public ResourceHit(string nodeId, int stock, bool yielded)
    {
        NodeId = nodeId;
        Stock = stock;
        Yielded = yielded;
    }

    public string NodeId { get; }

    /// <summary>Stock left after the hit.</summary>
    public int Stock { get; }

    /// <summary>The hit produced items (it used up one stock).</summary>
    public bool Yielded { get; }
}

/// <summary>
/// Resource nodes in the loaded scenes (the game's <c>WorkableResourceSource</c>): listing them, changing their
/// stock, and events when the player hits or depletes one.
/// </summary>
public static class ResourceNodes
{
    private static readonly AccessTools.FieldRef<WorkableResourceSource, int> CurrentStock =
        AccessTools.FieldRefAccess<WorkableResourceSource, int>("currentStock");

    private static readonly AccessTools.FieldRef<WorkableResourceSource, int> MaximumStock =
        AccessTools.FieldRefAccess<WorkableResourceSource, int>("maximumStock");

    private static readonly AccessTools.FieldRef<WorkableResourceSource, List<ItemReference>> OutputItems =
        AccessTools.FieldRefAccess<WorkableResourceSource, List<ItemReference>>("outputItems");

    /// <summary>A node took a hit of a damage type it accepts (it may or may not have yielded).</summary>
    public static readonly WorldEvent<ResourceHit> Hit = new WorldEvent<ResourceHit>(nameof(Hit));

    /// <summary>A node's stock reached zero. The payload is its id.</summary>
    public static readonly WorldEvent<string> Depleted = new WorldEvent<string>(nameof(Depleted));

    public static List<ResourceNodeInfo> All()
    {
        var list = new List<ResourceNodeInfo>();
        foreach (var node in SceneIds.Active<WorkableResourceSource>())
            list.Add(Describe(node));
        return list;
    }

    /// <summary>The nodes within <paramref name="radius"/> of a point.</summary>
    public static List<ResourceNodeInfo> Near(Vector3 position, float radius)
    {
        var list = new List<ResourceNodeInfo>();
        foreach (var node in SceneIds.Active<WorkableResourceSource>())
        {
            if ((node.transform.position - position).sqrMagnitude <= radius * radius)
                list.Add(Describe(node));
        }
        return list;
    }

    public static WorkableResourceSource Find(string id) => SceneIds.Find<WorkableResourceSource>(id);

    public static ResourceNodeInfo? Get(string id)
    {
        var node = Find(id);
        return node != null ? Describe(node) : (ResourceNodeInfo?)null;
    }

    /// <summary>Sets a node's stock (capped at its maximum) and lets it refill from there. Refused while a save loads.</summary>
    public static bool SetStock(string id, int stock)
    {
        if (stock < 0)
            throw new ArgumentOutOfRangeException(nameof(stock));
        var node = Find(id);
        if (node == null || SaveGate.IsBusy)
            return false;

        int max = MaximumStock(node);
        int value = Math.Min(stock, max);
        CurrentStock(node) = value;
        node.ChangeDeltaSecondsStatus(value < max);
        if (node.onStock != null && max > 0)
            node.onStock.triggerOutputs((float)value / max);
        if (node.onCurrentStock != null)
            node.onCurrentStock.triggerOutputs(value);
        node.UpdateState();
        return true;
    }

    public static ResourceNodeInfo Describe(WorkableResourceSource node)
    {
        var items = new List<int>();
        var outputs = OutputItems(node);
        if (outputs != null)
        {
            foreach (var reference in outputs)
            {
                if (reference != null && reference.ID > 0)
                    items.Add(reference.ID);
            }
        }
        var spawn = node.spawnSettings;
        return new ResourceNodeInfo(SceneIds.IdOf(node), node.transform.position, items, CurrentStock(node), MaximumStock(node),
            node.owner != null ? node.owner.ID : 0, node.permissionToTake, spawn != null && spawn.respawnEnabled);
    }
}
