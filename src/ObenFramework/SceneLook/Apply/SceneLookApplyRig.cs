// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
using System;
using System.Collections.Generic;
using UnityEngine;

namespace ObenFramework.SceneLook;

/// <summary>
/// Puts a look's renderers on a body. The name and skeleton paths skin each renderer straight onto the
/// body's own bones, with bind poses rebuilt from the body's rest skeleton so the mesh lines up with it;
/// the retarget path (the other half of this class) builds the look's own skeleton instead.
/// </summary>
internal static partial class SceneLookApplyRig
{
    public static bool Bind(SceneLookLook look, GameObject body, SceneLookRigPlan plan, SceneLookAssets assets, SceneLookApplied state, out string reason)
    {
        reason = null;
        var root = body.transform;
        var map = new Dictionary<string, string>();
        foreach (var bone in plan.Bones)
            map[bone.LookName] = bone.TargetName;

        var resolved = new List<Transform[]>(look.Renderers.Count);
        foreach (var renderer in look.Renderers)
        {
            var bones = new Transform[renderer.Bones.Count];
            for (int i = 0; i < bones.Length; i++)
            {
                string lookName = renderer.Bones[i].Name;
                if (!map.TryGetValue(lookName, out string name))
                {
                    reason = "bone " + lookName + " has no target name";
                    return false;
                }
                bones[i] = SceneLookRigTools.Find(root, name);
                if (bones[i] == null)
                {
                    reason = "bone " + name + " is not under the body root";
                    return false;
                }
            }
            resolved.Add(bones);
        }

        var rest = TargetRest(body);
        var holder = SceneLookRigTools.Holder(look, body, state);
        var frame = SceneLookBind.BindFrame(SceneLookApplyMesh.FromUnity(body.transform.localToWorldMatrix), SceneLookApplyMesh.FromUnity(holder.transform.localToWorldMatrix));
        var byTransform = SceneLookFrame.ByTransform(look);
        var rootBone = MapRoot(plan, map, root);
        var target = SceneLookRigTools.TargetMaterial(body);
        for (int i = 0; i < look.Renderers.Count; i++)
        {
            var renderer = look.Renderers[i];
            var owner = SceneLookRigTools.Child(renderer.Name, holder.transform, state);
            var node = SceneLookFrame.Node(look, renderer.Source.GameObject.PathId);
            if (node != null)
                SceneLookRigTools.PlaceMatrix(owner.transform, SceneLookApplyMesh.ToUnity(SceneLookFrame.Relative(look, node, byTransform)));
            var poses = BindPoses(renderer, map, rest, frame);
            if (!Attach(owner, renderer, assets, resolved[i], rootBone, target, poses, state, out reason))
                return false;
        }
        return true;
    }

    // A bind pose from the body's rest skeleton where the bone is known, else the file's own.
    private static Matrix4x4[] BindPoses(SceneLookLookRenderer renderer, Dictionary<string, string> map, Dictionary<string, SceneLookMatrix> rest, SceneLookMatrix holderFrame)
    {
        var frame = MeshFrame(renderer, map, rest, out bool known);
        if (!known)
            frame = holderFrame;
        var poses = new Matrix4x4[renderer.Bones.Count];
        for (int i = 0; i < poses.Length; i++)
        {
            if (map.TryGetValue(renderer.Bones[i].Name, out string name) && rest.TryGetValue(name, out var boneRest))
                poses[i] = SceneLookApplyMesh.ToUnity(SceneLookBind.BindPose(boneRest, frame));
            else
                poses[i] = SceneLookApplyMesh.FilePose(renderer.Mesh, i);
        }
        return poses;
    }

    // Each look mesh comes in the space it was authored in, which needn't be the body's: the game's clothing
    // stands along Z while its heads stand along Y. Like the body's own meshes when its rest is put together,
    // the mesh is brought into the rest's frame through a bone both know; only a mesh sharing none keeps the
    // holder's frame.
    private static SceneLookMatrix MeshFrame(SceneLookLookRenderer renderer, Dictionary<string, string> map, Dictionary<string, SceneLookMatrix> rest, out bool known)
    {
        var mesh = new SceneLookBindMesh<string>();
        if (renderer.Mesh?.BindPoses == null || renderer.Mesh.BindPoses.Length != renderer.Bones.Count)
        {
            known = false;
            return SceneLookMatrix.Identity();
        }
        for (int i = 0; i < renderer.Bones.Count; i++)
        {
            if (map.TryGetValue(renderer.Bones[i].Name, out string name))
                mesh.Add(name, SceneLookApplyMesh.FromUnity(SceneLookApplyMesh.FilePose(renderer.Mesh, i)));
        }
        return SceneLookBind.Frame(mesh, rest, out known);
    }

    // The body's bones at rest, recovered from the bind poses of its own skinned renderers.
    private static Dictionary<string, SceneLookMatrix> TargetRest(GameObject body)
    {
        var meshes = new List<SceneLookBindMesh<string>>();
        foreach (var renderer in body.GetComponentsInChildren<SkinnedMeshRenderer>(true))
        {
            var mesh = renderer.sharedMesh;
            if (mesh == null)
                continue;
            var poses = mesh.bindposes;
            var bones = renderer.bones;
            if (poses == null || bones == null)
                continue;
            var item = new SceneLookBindMesh<string>();
            int count = Math.Min(poses.Length, bones.Length);
            for (int i = 0; i < count; i++)
            {
                if (bones[i] != null)
                    item.Add(bones[i].name, SceneLookApplyMesh.FromUnity(poses[i]));
            }
            meshes.Add(item);
        }
        return SceneLookBind.Rest(meshes, out _);
    }

    private static Transform MapRoot(SceneLookRigPlan plan, Dictionary<string, string> map, Transform root)
    {
        foreach (var role in plan.Roles)
        {
            if (role.HumanName != "Hips")
                continue;
            if (map.TryGetValue(role.LookName, out string name))
            {
                var mapped = SceneLookRigTools.Find(root, name);
                if (mapped != null)
                    return mapped;
            }
            break;
        }
        var fallback = SceneLookRigTools.Find(root, SceneLookRig.RootBone);
        return fallback != null ? fallback : root;
    }

    /// <summary>Adds the skinned renderer for one look renderer: mesh, bones, materials and blend shape weights.</summary>
    private static bool Attach(GameObject owner, SceneLookLookRenderer renderer, SceneLookAssets assets, Transform[] bones, Transform rootBone,
        Material target, Matrix4x4[] poses, SceneLookApplied state, out string reason)
    {
        var materials = Materials(assets, renderer, target, out reason);
        if (materials == null)
            return false;
        var mesh = SceneLookApplyMesh.Build(renderer.Mesh, bones.Length > 0 ? poses : null, out reason);
        if (mesh == null)
            return false;
        state.Meshes.Add(mesh);
        SceneLookApplyShapes.Add(mesh, renderer.Mesh);

        var smr = owner.AddComponent<SkinnedMeshRenderer>();
        smr.sharedMesh = mesh;
        if (bones.Length > 0)
        {
            smr.bones = bones;
            smr.rootBone = rootBone;
        }
        smr.sharedMaterials = materials;
        smr.updateWhenOffscreen = true;
        SceneLookApplyShapes.Weights(smr, renderer.Source.BlendShapeWeights);
        return true;
    }

    private static Material[] Materials(SceneLookAssets assets, SceneLookLookRenderer renderer, Material target, out string reason)
    {
        reason = null;
        var materials = new List<Material>(renderer.Materials.Count);
        foreach (var material in renderer.Materials)
        {
            if (material == null)
            {
                materials.Add(null);
                continue;
            }
            var built = SceneLookApplyTexture.Material(assets, material, target, out reason);
            if (reason != null)
                return null;
            materials.Add(built);
        }
        if (materials.Count == 0)
        {
            var plain = SceneLookApplyTexture.Plain(assets, target, out reason);
            if (reason != null)
                return null;
            materials.Add(plain);
        }
        return materials.ToArray();
    }
}
