// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
using System.Collections.Generic;
using NPC;
using ObenFramework.Scenes;
using UnityEngine;

namespace ObenFramework.Npcs;

/// <summary>One of the scene's node groups: the routes and spots the game's own NPCs patrol and stand at.</summary>
public sealed class NodeGroupInfo
{
    internal NodeGroupInfo(NodeGroup group)
    {
        Group = group;
        Id = SceneIds.Of(group);
        Name = group.name;
        Nodes = group.nodes != null ? group.nodes.Count : 0;
        Position = Nodes > 0 && group.nodes[0] != null ? group.nodes[0].transform.position : group.transform.position;
        Loop = group.loop;
        Mode = group.patrolMode.ToString();
        CanStand = group.nodeReference != null && group.nodeReference.node != null;
    }

    /// <summary>The game's object, for <see cref="NpcClaim.Patrol"/> and <see cref="NpcClaim.Stand"/>.</summary>
    public NodeGroup Group { get; }

    /// <summary>A text id that <see cref="NpcNodes.Find"/> takes back.</summary>
    public string Id { get; }

    public string Name { get; }

    /// <summary>How many nodes the patrol goes through.</summary>
    public int Nodes { get; }

    /// <summary>Where the first node is (the group's own position when it has none).</summary>
    public Vector3 Position { get; }

    public bool Loop { get; }

    /// <summary>The order the nodes are walked in: InOrder, Closest or Random.</summary>
    public string Mode { get; }

    /// <summary>It has a stand node, so <see cref="NpcClaim.Stand"/> works with it.</summary>
    public bool CanStand { get; }

    public override string ToString() => $"{Name} ({Nodes} nodes, {Mode}{(Loop ? ", loop" : "")}) {Id}";
}

/// <summary>
/// The node groups of the scene you are in: the patrol routes and standing spots the level gives its NPCs.
/// A claimed NPC with AI walks one with <see cref="NpcClaim.Patrol"/> or stands at one with
/// <see cref="NpcClaim.Stand"/>. Lookups walk the scene, so don't call them every frame.
/// </summary>
public static class NpcNodes
{
    /// <summary>Every node group in the scene, nearest to <paramref name="from"/> first when given.</summary>
    public static List<NodeGroupInfo> Groups(Vector3? from = null)
    {
        var list = new List<NodeGroupInfo>();
        foreach (var group in SceneIds.Active<NodeGroup>(true))
            list.Add(new NodeGroupInfo(group));
        if (from.HasValue)
        {
            var origin = from.Value;
            list.Sort((a, b) => (a.Position - origin).sqrMagnitude.CompareTo((b.Position - origin).sqrMagnitude));
        }
        return list;
    }

    /// <summary>The node group with this id (from <see cref="NodeGroupInfo.Id"/>), a GUID or a path; null when there is none.</summary>
    public static NodeGroup Find(string id) => SceneIds.Find<NodeGroup>(id);
}
