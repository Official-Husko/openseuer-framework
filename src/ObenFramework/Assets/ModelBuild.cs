// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Rendering;

namespace ObenFramework.Assets;

/// <summary>
/// The main-thread half of loading a model, in steps small enough to spread over frames: the mesh, then
/// one material (and its texture) at a time, then the template object. Each step catches its own errors
/// and records them in <see cref="Error"/>.
/// </summary>
internal sealed class ModelBuild
{
    private const int MaxUInt16Vertices = 65535;
    private const float MinColliderEdge = 0.01f;

    private readonly ModAssetSet _set;
    private readonly PreparedModel _model;
    private readonly ModelOptions _options;
    private readonly List<Material> _materials = new List<Material>();
    private Mesh _mesh;
    private Shader _shader;
    private string _textureOverride;

    public ModelBuild(ModAssetSet set, PreparedModel model, ModelOptions options)
    {
        _set = set;
        _model = model;
        _options = options;
    }

    public string Error { get; private set; }

    public bool Failed => Error != null;

    /// <summary>Step 1: the mesh and the shader.</summary>
    public void BuildMesh()
    {
        try
        {
            _shader = ModelMaterials.Pick(_options.Shader);
            if (!string.IsNullOrEmpty(_options.Texture))
                _textureOverride = ModAssets.ResolveExisting(_set.Owner, _options.Texture);
            _mesh = _set.Adopt(CreateMesh());
        }
        catch (Exception e)
        {
            Error = e.Message;
        }
    }

    /// <summary>Step 2, repeated: builds the next material. Returns true while there are more to build.</summary>
    public bool BuildNextMaterial()
    {
        if (Failed || _materials.Count >= _model.Materials.Length)
            return false;
        try
        {
            int index = _materials.Count;
            MtlMaterial mtl = _model.Materials[index];
            string name = mtl != null ? mtl.Name : _model.Key + "_" + index;
            var material = ModelMaterials.Create(_shader, mtl, TextureFor(mtl), _options.Smoothness, _model.Key + ":" + name);
            _materials.Add(_set.Adopt(material));
        }
        catch (Exception e)
        {
            Error = e.Message;
            return false;
        }
        return _materials.Count < _model.Materials.Length;
    }

    /// <summary>Step 3: the hidden prefab, collider and the template that wraps them.</summary>
    public ModelTemplate Assemble()
    {
        if (Failed)
            return null;
        GameObject prefab = null;
        try
        {
            prefab = new GameObject(_model.Key);
            prefab.SetActive(false);
            prefab.layer = _options.Layer;
            UnityEngine.Object.DontDestroyOnLoad(prefab);
            prefab.AddComponent<MeshFilter>().sharedMesh = _mesh;
            prefab.AddComponent<MeshRenderer>().sharedMaterials = _materials.ToArray();
            AddCollider(prefab);
            return new ModelTemplate(_set.ModId, _model.Key, _model.Path, _mesh, _materials.ToArray(), _model.Bounds, _model.Triangles, prefab);
        }
        catch (Exception e)
        {
            Error = e.Message;
            if (prefab != null)
                UnityEngine.Object.Destroy(prefab);
            return null;
        }
    }

    /// <summary>Destroys whatever the steps made, after a failure.</summary>
    public void Abort()
    {
        foreach (var material in _materials)
        {
            if (material != null)
                UnityEngine.Object.Destroy(material);
        }
        if (_mesh != null)
            UnityEngine.Object.Destroy(_mesh);
    }

    private Mesh CreateMesh()
    {
        var mesh = new Mesh { name = _model.Key };
        // Must come first: switching the index format afterwards clears the data.
        mesh.indexFormat = _model.Vertices.Length > MaxUInt16Vertices ? IndexFormat.UInt32 : IndexFormat.UInt16;
        mesh.SetVertices(_model.Vertices);
        mesh.SetNormals(_model.Normals);
        if (_model.Uvs != null)
            mesh.SetUVs(0, _model.Uvs);
        mesh.subMeshCount = _model.Indices.Length;
        for (int i = 0; i < _model.Indices.Length; i++)
            mesh.SetIndices(_model.Indices[i], MeshTopology.Triangles, i, false);
        mesh.bounds = _model.Bounds;
        // A collider cooks from the CPU copy, so only drop it when nothing needs it.
        if (_options.Collider != ModelCollider.Convex && _options.Collider != ModelCollider.Mesh)
            mesh.UploadMeshData(true);
        return mesh;
    }

    private Texture2D TextureFor(MtlMaterial mtl)
    {
        string path = _textureOverride ?? (mtl != null ? mtl.MapKd : null);
        if (path == null)
            return null;
        try
        {
            return ModAssets.LoadTexture(_set, path, new TextureOptions
            {
                MaxSide = _options.MaxTextureSide,
                Wrap = TextureWrapMode.Repeat,
            });
        }
        catch (Exception e) when (e is IOException || e is InvalidDataException || e is UnauthorizedAccessException)
        {
            AssetsLog.Warn($"{_set.ModId}: model {_model.Key} texture skipped, the material keeps its colour: {e.Message}");
            return null;
        }
    }

    private void AddCollider(GameObject prefab)
    {
        switch (_options.Collider)
        {
            case ModelCollider.Box:
                var box = prefab.AddComponent<BoxCollider>();
                box.center = _model.Bounds.center;
                box.size = Vector3.Max(_model.Bounds.size, Vector3.one * MinColliderEdge);
                break;
            case ModelCollider.Convex:
            case ModelCollider.Mesh:
                var collider = prefab.AddComponent<MeshCollider>();
                collider.sharedMesh = _mesh;
                collider.convex = _options.Collider == ModelCollider.Convex;
                break;
        }
    }
}
