using System.Collections.Generic;
using UnityEngine;

namespace ObenFramework.Npcs;

/// <summary>One mesh of an NPC's body, as <see cref="NpcClaim.Meshes"/> reads it.</summary>
public sealed class NpcMesh
{
    /// <summary>The mesh object's name: what the appearance calls take.</summary>
    public string Name { get; }

    /// <summary>The mesh object is switched on.</summary>
    public bool Visible { get; }

    /// <summary>Names of its materials, one per slot (null for an empty slot).</summary>
    public IReadOnlyList<string> Materials { get; }

    /// <summary>Names of its blend shapes, empty for a mesh without any.</summary>
    public IReadOnlyList<string> BlendShapes { get; }

    private NpcMesh(Renderer renderer)
    {
        Name = renderer.gameObject.name;
        Visible = renderer.gameObject.activeSelf;
        var materials = new List<string>();
        foreach (var material in renderer.sharedMaterials)
            materials.Add(material != null ? material.name : null);
        Materials = materials;
        var shapes = new List<string>();
        var mesh = renderer is SkinnedMeshRenderer skinned ? skinned.sharedMesh : null;
        if (mesh != null)
        {
            for (int i = 0; i < mesh.blendShapeCount; i++)
                shapes.Add(mesh.GetBlendShapeName(i));
        }
        BlendShapes = shapes;
    }

    internal static List<NpcMesh> Of(GameObject body)
    {
        var result = new List<NpcMesh>();
        foreach (var renderer in body.GetComponentsInChildren<Renderer>(true))
        {
            if (renderer != null && (renderer is SkinnedMeshRenderer || renderer is MeshRenderer))
                result.Add(new NpcMesh(renderer));
        }
        return result;
    }

    public override string ToString() => $"{Name}{(Visible ? "" : " (hidden)")}: {Materials.Count} materials, {BlendShapes.Count} shapes";
}
