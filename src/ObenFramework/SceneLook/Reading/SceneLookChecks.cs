// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
using System;
using System.Collections.Generic;

namespace ObenFramework.SceneLook;

/// <summary>
/// Checks whether a skinned renderer can be rebuilt from the files: its bones resolve, its mesh buffers
/// are inline and consistent, its materials and textures are in formats that load raw. Results per mesh,
/// material and texture are shared within one <see cref="State"/>, since many renderers use the same ones.
/// </summary>
internal static class SceneLookChecks
{
    internal sealed class RendererCheck
    {
        public long GameObject;
        public SceneLookRenderer Source;
        public readonly List<string> Bones = new List<string>();
        public readonly List<string> Reasons = new List<string>();
        public readonly List<string> Shaders = new List<string>();
        public readonly List<int> Formats = new List<int>();
        public long Vertices;
        public long Triangles;
    }

    internal sealed class State
    {
        public readonly Dictionary<string, MeshCheck> Meshes = new Dictionary<string, MeshCheck>();
        public readonly Dictionary<string, MaterialCheck> Materials = new Dictionary<string, MaterialCheck>();
        public readonly Dictionary<string, TextureCheck> Textures = new Dictionary<string, TextureCheck>();
    }

    internal sealed class MeshCheck
    {
        public readonly List<string> Reasons = new List<string>();
        public long Vertices;
        public long Triangles;
    }

    internal sealed class MaterialCheck
    {
        public readonly List<string> Reasons = new List<string>();
        public string Shader;
        public readonly List<TextureCheck> Textures = new List<TextureCheck>();
    }

    internal sealed class TextureCheck
    {
        public readonly List<string> Reasons = new List<string>();
        public int Format = -1;
    }

    public static RendererCheck Renderer(SceneLookFiles files, SceneLookFile file, long pathId, State state)
    {
        var check = new RendererCheck();
        SceneLookRenderer renderer;
        try { renderer = SceneLookMeshReader.ParseRenderer(pathId, file.ReadObject(pathId)); }
        catch (Exception)
        {
            check.Reasons.Add("smr layout error");
            return check;
        }
        check.Source = renderer;
        if (file.Contains(renderer.GameObject.PathId))
            check.GameObject = renderer.GameObject.PathId;

        foreach (var bone in renderer.Bones)
        {
            var target = file.Target(bone);
            if (target.Error != null)
            {
                check.Reasons.Add("bone " + target.Error);
                continue;
            }
            try { check.Bones.Add(SceneLookReader.BoneName(target)); }
            catch (Exception) { check.Reasons.Add("bone layout error"); }
        }

        var mesh = file.Target(renderer.Mesh);
        if (mesh.Error != null)
        {
            check.Reasons.Add("mesh " + mesh.Error);
        }
        else
        {
            var meshCheck = Mesh(state, mesh.File, mesh.PathId);
            check.Reasons.AddRange(meshCheck.Reasons);
            check.Vertices = meshCheck.Vertices;
            check.Triangles = meshCheck.Triangles;
        }

        foreach (var material in renderer.Materials)
        {
            var target = file.Target(material);
            if (target.Error == "null")
                continue;
            if (target.Error != null)
            {
                check.Reasons.Add("material " + target.Error);
                continue;
            }
            var materialCheck = Material(files, state, target.File, target.PathId);
            check.Reasons.AddRange(materialCheck.Reasons);
            if (materialCheck.Shader != null)
                check.Shaders.Add(materialCheck.Shader);
            foreach (var texture in materialCheck.Textures)
            {
                check.Reasons.AddRange(texture.Reasons);
                if (texture.Format >= 0)
                    check.Formats.Add(texture.Format);
            }
        }
        return check;
    }

    private static MeshCheck Mesh(State state, SceneLookFile file, long pathId)
    {
        string key = file.Name + "|" + pathId;
        if (state.Meshes.TryGetValue(key, out var cached))
            return cached;
        var check = new MeshCheck();
        if (file.ClassId(pathId) != SceneLookClass.Mesh)
        {
            check.Reasons.Add("mesh pptr not a Mesh");
        }
        else
        {
            try
            {
                var mesh = SceneLookMeshReader.ParseMesh(file, pathId, false);
                check.Reasons.AddRange(SceneLookMeshReader.MeshReasons(mesh));
                check.Vertices = mesh.VertexCount;
                check.Triangles = SceneLookMeshReader.IndexTotal(mesh) / 3;
            }
            catch (Exception)
            {
                check.Reasons.Add("mesh layout error");
            }
        }
        state.Meshes[key] = check;
        return check;
    }

    private static MaterialCheck Material(SceneLookFiles files, State state, SceneLookFile file, long pathId)
    {
        string key = file.Name + "|" + pathId;
        if (state.Materials.TryGetValue(key, out var cached))
            return cached;
        var check = new MaterialCheck();
        SceneLookMaterial material = null;
        try { material = SceneLookMaterialReader.ParseMaterial(file, pathId); }
        catch (Exception) { check.Reasons.Add("material layout error"); }

        if (material != null)
        {
            var shader = file.Target(material.Shader);
            if (shader.Error == "null")
                check.Shader = "null";
            else if (shader.Error != null)
                check.Reasons.Add("shader " + shader.Error);
            else if (shader.File.ClassId(shader.PathId) != SceneLookClass.Shader)
                check.Reasons.Add("shader pptr not a Shader");
            else
                check.Shader = SceneLookMaterialReader.ShaderLabel(shader.File, shader.PathId);

            foreach (var env in material.TexEnvs)
            {
                var texture = file.Target(env.Texture);
                if (texture.Error == "null")
                    continue;
                if (texture.Error != null)
                {
                    check.Reasons.Add("texture " + texture.Error);
                    continue;
                }
                check.Textures.Add(Texture(files, state, texture.File, texture.PathId));
            }
        }
        state.Materials[key] = check;
        return check;
    }

    private static TextureCheck Texture(SceneLookFiles files, State state, SceneLookFile file, long pathId)
    {
        string key = file.Name + "|" + pathId;
        if (state.Textures.TryGetValue(key, out var cached))
            return cached;
        var check = new TextureCheck();
        if (file.ClassId(pathId) != SceneLookClass.Texture2D)
        {
            check.Reasons.Add("texture pptr not Texture2D");
        }
        else
        {
            try
            {
                var texture = SceneLookTextureReader.ParseTexture(file, pathId, file.ReadObject(pathId));
                check.Format = texture.Format;
                check.Reasons.AddRange(SceneLookTextureReader.TextureReasons(files, texture));
            }
            catch (Exception)
            {
                check.Reasons.Add("texture layout error");
            }
        }
        state.Textures[key] = check;
        return check;
    }
}
