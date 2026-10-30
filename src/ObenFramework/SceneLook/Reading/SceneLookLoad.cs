// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
using System.Collections.Generic;

namespace ObenFramework.SceneLook;

/// <summary>
/// Loads everything a look needs to be rebuilt: its node tree with file poses, and for every skinned
/// renderer the mesh with its buffers and bind poses, the materials with their textures, and the bones.
/// Refuses when any renderer fails its checks or uses bones from outside the look's own file.
/// </summary>
internal static class SceneLookLoad
{
    public static SceneLookLook Load(SceneLookFiles files, string fileName, long root, out string reason)
    {
        var file = files.Get(fileName, out reason);
        if (file == null)
            return null;
        if (!file.Contains(root))
        {
            reason = "root not found";
            return null;
        }
        var renderers = Shown(file, root, SceneLookScan.RenderersUnder(file, root));
        if (renderers.Count == 0)
        {
            reason = "no skinned renderers under the root";
            return null;
        }

        var state = new SceneLookChecks.State();
        var look = new SceneLookLook
        {
            FileName = fileName,
            Root = root,
            Files = files,
            Nodes = Nodes(file, root),
            Renderers = new List<SceneLookLookRenderer>(),
            Textures = new List<SceneLookTexture>(),
        };
        var textures = new Dictionary<string, SceneLookTexture>();
        foreach (long pathId in renderers)
        {
            var check = SceneLookChecks.Renderer(files, file, pathId, state);
            if (check.Reasons.Count > 0)
            {
                reason = "renderer " + pathId + " refused: " + string.Join("; ", check.Reasons);
                return null;
            }
            var item = Renderer(file, check, look, textures, out reason);
            if (item == null)
                return null;
            look.Renderers.Add(item);
        }
        return look;
    }

    // Characters made from the game's clothing kit carry every variant and show a few: the rest sit on
    // switched-off objects or renderers below the body. The look is what the scene shows (the body itself may
    // be off, waiting for its hours), or every renderer when it shows none, as a script then picks them.
    private static List<long> Shown(SceneLookFile file, long root, List<long> renderers)
    {
        var shown = new List<long>(renderers.Count);
        foreach (long pathId in renderers)
        {
            var renderer = SceneLookMeshReader.ParseRenderer(pathId, file.ReadObject(pathId));
            if (renderer.Enabled && renderer.GameObject.FileId == 0 && OnBelow(file, renderer.GameObject.PathId, root))
                shown.Add(pathId);
        }
        return shown.Count > 0 ? shown : renderers;
    }

    private static bool OnBelow(SceneLookFile file, long gameObject, long root)
    {
        foreach (long current in SceneLookReader.Chain(file, gameObject))
        {
            if (current == root)
                return true;
            if (!SceneLookReader.GameObject(file, current).Active)
                return false;
        }
        return true;
    }

    private static SceneLookLookRenderer Renderer(SceneLookFile file, SceneLookChecks.RendererCheck check, SceneLookLook look,
        Dictionary<string, SceneLookTexture> textures, out string reason)
    {
        reason = null;
        var item = new SceneLookLookRenderer
        {
            Name = SceneLookReader.GameObject(file, check.GameObject).Name,
            Source = check.Source,
            Materials = new List<SceneLookMaterial>(),
            Bones = new List<SceneLookBone>(),
        };
        var meshTarget = file.Target(check.Source.Mesh);
        item.Mesh = SceneLookMeshReader.ParseMesh(meshTarget.File, meshTarget.PathId, true);

        foreach (var materialRef in check.Source.Materials)
        {
            var materialTarget = file.Target(materialRef);
            if (materialTarget.Error == "null")
            {
                item.Materials.Add(null);
                continue;
            }
            var material = SceneLookMaterialReader.ParseMaterial(materialTarget.File, materialTarget.PathId);
            item.Materials.Add(material);
            foreach (var env in material.TexEnvs)
            {
                var texture = materialTarget.File.Target(env.Texture);
                if (texture.Error == "null")
                    continue;
                string key = texture.File.Name + "|" + texture.PathId;
                if (!textures.TryGetValue(key, out var resolved))
                {
                    resolved = SceneLookTextureReader.ParseTexture(texture.File, texture.PathId, texture.File.ReadObject(texture.PathId));
                    textures[key] = resolved;
                    look.Textures.Add(resolved);
                }
                env.Resolved = resolved;
            }
        }

        foreach (var boneRef in check.Source.Bones)
        {
            if (file.Target(boneRef).File != file)
            {
                reason = "bone outside the look file";
                return null;
            }
            item.Bones.Add(Bone(file, boneRef));
        }

        var rootBone = file.Target(check.Source.RootBone);
        if (rootBone.Error == "null")
        {
            item.RootBone = null;
        }
        else if (rootBone.Error != null || rootBone.File != file)
        {
            reason = "root bone " + (rootBone.Error ?? "outside the look file");
            return null;
        }
        else
        {
            item.RootBone = Bone(file, check.Source.RootBone);
        }
        return item;
    }

    private static List<SceneLookNode> Nodes(SceneLookFile file, long root)
    {
        var nodes = new List<SceneLookNode>();
        foreach (long transformPid in SceneLookReader.SubtreeTransforms(file, root))
        {
            var transform = SceneLookReader.Transform(file, transformPid);
            nodes.Add(new SceneLookNode
            {
                GameObject = transform.GameObject.PathId,
                Transform = transformPid,
                Name = SceneLookReader.GameObject(file, transform.GameObject.PathId).Name,
                Father = transform.Father,
                Children = transform.Children,
                Rotation = transform.Rotation,
                Position = transform.Position,
                Scale = transform.Scale,
            });
        }
        return nodes;
    }

    private static SceneLookBone Bone(SceneLookFile file, SceneLookRef transform) =>
        new SceneLookBone { Name = SceneLookReader.BoneName(file.Target(transform)), Transform = transform };
}
