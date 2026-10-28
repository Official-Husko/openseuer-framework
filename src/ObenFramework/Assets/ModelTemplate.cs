using UnityEngine;

namespace ObenFramework.Assets;

/// <summary>
/// A model loaded from an OBJ file: its mesh, materials and a hidden prefab-like object to copy. Get one
/// from <see cref="ModelRegistry"/> and call <see cref="Instantiate(Transform)"/> for each use. It is
/// destroyed when the mod that loaded it unloads, so copies made from it must not outlive that mod.
/// </summary>
public sealed class ModelTemplate
{
    private readonly GameObject _prefab;

    internal ModelTemplate(string modId, string key, string sourcePath, Mesh mesh, Material[] materials, Bounds bounds, int triangles, GameObject prefab)
    {
        ModId = modId;
        Key = key;
        SourcePath = sourcePath;
        Mesh = mesh;
        Materials = materials;
        Bounds = bounds;
        TriangleCount = triangles;
        _prefab = prefab;
    }

    /// <summary>The mod that loaded the model.</summary>
    public string ModId { get; }

    /// <summary>The name the mod gave the model.</summary>
    public string Key { get; }

    /// <summary>Full name, <c>modId/key</c>, as <see cref="ModelRegistry.Find(string)"/> takes it.</summary>
    public string Name => ModId + "/" + Key;

    /// <summary>The OBJ file it came from.</summary>
    public string SourcePath { get; }

    public Mesh Mesh { get; }

    /// <summary>One material per submesh, in the order the OBJ file first used them.</summary>
    public Material[] Materials { get; }

    /// <summary>Bounds of the finished mesh, after scale, rotation and pivot.</summary>
    public Bounds Bounds { get; }

    public int VertexCount => Mesh != null ? Mesh.vertexCount : 0;

    public int TriangleCount { get; }

    /// <summary>False once the owning mod has unloaded.</summary>
    public bool IsAlive => Mesh != null;

    /// <summary>
    /// The inactive object the instances are copied from. Use it to add components before copying, or as
    /// the prefab for code that wants to call <c>Object.Instantiate</c> itself. Don't activate it.
    /// </summary>
    public GameObject Prefab => _prefab;

    /// <summary>Makes an active copy at a position, optionally under a parent.</summary>
    public GameObject Instantiate(Vector3 position, Quaternion rotation, Transform parent = null)
    {
        if (!IsAlive || _prefab == null)
            throw new System.InvalidOperationException($"Model {Name} has been unloaded.");
        var copy = Object.Instantiate(_prefab, position, rotation, parent);
        copy.name = Key;
        copy.SetActive(true);
        return copy;
    }

    /// <summary>Makes an active copy at the parent's origin (or the world origin without a parent).</summary>
    public GameObject Instantiate(Transform parent = null)
    {
        var copy = Instantiate(Vector3.zero, Quaternion.identity, parent);
        if (parent != null)
        {
            copy.transform.localPosition = Vector3.zero;
            copy.transform.localRotation = Quaternion.identity;
        }
        return copy;
    }

    internal void Destroy()
    {
        if (_prefab != null)
            Object.Destroy(_prefab);
        foreach (var material in Materials)
        {
            if (material != null)
                Object.Destroy(material);
        }
        if (Mesh != null)
            Object.Destroy(Mesh);
    }
}
