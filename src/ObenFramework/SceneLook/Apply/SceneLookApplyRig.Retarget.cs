// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
using System;
using System.Collections.Generic;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ObenFramework.SceneLook;

internal static partial class SceneLookApplyRig
{
    private const int MaxDepth = 256;

    /// <summary>
    /// Builds the look's own skeleton (its node tree at the rest pose of its bind poses) under the body, a
    /// humanoid avatar for it, skins the renderers onto it with the file's bind poses, and adds a driver that
    /// copies the body's humanoid pose onto the look every frame.
    /// </summary>
    public static bool Retarget(SceneLookLook look, GameObject body, SceneLookRigPlan plan, SceneLookAssets assets, SceneLookApplied state, out string reason)
    {
        reason = null;
        var targetAvatar = SceneLookRigTools.HumanAvatar(body);
        var targetAnimator = SceneLookRigTools.HumanAnimator(body);
        if (targetAvatar == null || targetAnimator == null)
        {
            reason = "target is not humanoid";
            return false;
        }
        if (!SceneLookFrame.HolderRatio(look, SceneLookRigTools.BodyScale(body), out float factor, out reason))
            return false;

        var rest = SceneLookNotes.Rest(look);
        var holder = SceneLookRigTools.Holder(look, body, state);
        var names = SceneLookChain.Names(look);
        var nodes = Skeleton(look, names, holder, state);
        var byTransform = SceneLookFrame.ByTransform(look);
        // The rest comes in the space the reference mesh was authored in (the game's clothing stands along Z).
        // The avatar has to stand along Y in the holder's own frame, so the holder stays upright and the bones
        // carry that mesh's place under the root.
        var frame = SceneLookFrame.Holder(look, byTransform);
        holder.transform.localScale *= factor;
        var worlds = new Dictionary<long, SceneLookMatrix>(look.Nodes.Count);
        var locals = new Dictionary<long, Matrix4x4>(look.Nodes.Count);
        foreach (var node in look.Nodes)
            Place(look, node, byTransform, nodes, rest, frame, worlds, locals, 0);
        SceneLookTPose.Apply(holder.transform, RoleBones(look, plan, nodes));
        foreach (var node in look.Nodes)
        {
            if (node.GameObject == look.Root)
                continue;
            var placed = nodes[node.Transform];
            locals[node.Transform] = Matrix4x4.TRS(placed.localPosition, placed.localRotation, placed.localScale);
        }

        var avatar = AvatarBuilder.BuildHumanAvatar(holder, Description(look, plan, names, locals));
        if (avatar == null || !avatar.isValid || !avatar.isHuman)
        {
            if (avatar != null)
                Object.Destroy(avatar);
            reason = "the look skeleton did not build a humanoid avatar";
            return false;
        }
        state.Avatar = avatar;

        var target = SceneLookRigTools.TargetMaterial(body);
        var rootBone = RootNode(look, plan, nodes, holder.transform);
        foreach (var renderer in look.Renderers)
        {
            var bones = new Transform[renderer.Bones.Count];
            for (int i = 0; i < bones.Length; i++)
            {
                if (!nodes.TryGetValue(renderer.Bones[i].Transform.PathId, out bones[i]))
                {
                    reason = "look bone " + renderer.Bones[i].Name + " is not in the look skeleton";
                    return false;
                }
            }
            var node = SceneLookFrame.Node(look, renderer.Source.GameObject.PathId);
            Transform owner = null;
            if (node != null)
                nodes.TryGetValue(node.Transform, out owner);
            var ownerObject = owner != null ? owner.gameObject : SceneLookRigTools.Child(renderer.Name, holder.transform, state);
            if (owner == null && node != null)
                SceneLookRigTools.PlaceMatrix(ownerObject.transform, SceneLookApplyMesh.ToUnity(SceneLookFrame.Relative(look, node, byTransform)));
            var poses = SceneLookApplyMesh.FileBindPoses(renderer.Mesh);
            if (poses == null || poses.Length != bones.Length)
            {
                reason = "the look mesh " + renderer.Name + " has no usable file bind poses";
                return false;
            }
            if (!Attach(ownerObject, renderer, assets, bones, rootBone, target, poses, state, out reason))
                return false;
        }
        RoleBones(look, plan, nodes).TryGetValue("Hips", out var hips);
        return AddDriver(targetAvatar, targetAnimator, avatar, holder, hips, state, out reason);
    }

    // One object per look node, parented as in the file; the root node is the holder itself.
    private static Dictionary<long, Transform> Skeleton(SceneLookLook look, Dictionary<long, string> names, GameObject holder, SceneLookApplied state)
    {
        var nodes = new Dictionary<long, Transform>(look.Nodes.Count);
        foreach (var node in look.Nodes)
            nodes[node.Transform] = node.GameObject == look.Root ? holder.transform : SceneLookRigTools.Child(names[node.Transform], holder.transform, state).transform;
        foreach (var node in look.Nodes)
        {
            if (node.GameObject == look.Root)
                continue;
            var parent = holder.transform;
            if (node.Father.FileId == 0 && nodes.TryGetValue(node.Father.PathId, out var found))
                parent = found;
            nodes[node.Transform].SetParent(parent, false);
        }
        return nodes;
    }

    private static bool AddDriver(Avatar targetAvatar, Animator targetAnimator, Avatar avatar, GameObject holder, Transform hips, SceneLookApplied state, out string reason)
    {
        reason = null;
        HumanPoseHandler from = null;
        HumanPoseHandler to = null;
        try
        {
            from = new HumanPoseHandler(targetAvatar, targetAnimator.transform);
            to = new HumanPoseHandler(avatar, holder.transform);
            var driver = holder.AddComponent<SceneLookPoseDriver>();
            driver.Setup(from, to);
            var bodyHips = targetAnimator.GetBoneTransform(HumanBodyBones.Hips);
            if (hips != null && bodyHips != null)
            {
                float lookHeight = SceneLookTPose.Hips(avatar, holder.transform, hips).y;
                float bodyHeight = SceneLookTPose.Hips(targetAvatar, targetAnimator.transform, bodyHips).y;
                float ratio = lookHeight > 0.05f && bodyHeight > 0.05f ? lookHeight / bodyHeight : 1f;
                driver.Anchor(targetAnimator.transform, bodyHips, holder.transform, hips, ratio);
            }
            state.Driver = driver;
            from = null;
            to = null;
            return true;
        }
        catch (Exception e)
        {
            reason = e.Message;
            return false;
        }
        finally
        {
            from?.Dispose();
            to?.Dispose();
        }
    }

    // Places a node at its rest pose (from the bind poses) or, for nodes no mesh binds, its file pose.
    private static SceneLookMatrix Place(SceneLookLook look, SceneLookNode node, Dictionary<long, SceneLookNode> byTransform, Dictionary<long, Transform> nodes,
        Dictionary<long, SceneLookMatrix> rest, SceneLookMatrix frame, Dictionary<long, SceneLookMatrix> worlds, Dictionary<long, Matrix4x4> locals, int depth)
    {
        if (worlds.TryGetValue(node.Transform, out var cached))
            return cached;
        if (node.GameObject == look.Root || depth > MaxDepth)
        {
            worlds[node.Transform] = SceneLookMatrix.Identity();
            locals[node.Transform] = Matrix4x4.identity;
            return SceneLookMatrix.Identity();
        }
        worlds[node.Transform] = SceneLookMatrix.Identity();
        var parentNode = SceneLookFrame.Parent(node, byTransform);
        var parentWorld = parentNode == null ? SceneLookMatrix.Identity() : Place(look, parentNode, byTransform, nodes, rest, frame, worlds, locals, depth + 1);
        var matrix = rest.TryGetValue(node.Transform, out var restWorld)
            ? SceneLookBind.Local(parentWorld, frame.Multiply(restWorld))
            : SceneLookApplyMesh.FromUnity(SceneLookRigTools.Local(node));
        var local = SceneLookApplyMesh.ToUnity(matrix);
        locals[node.Transform] = local;
        SceneLookRigTools.PlaceMatrix(nodes[node.Transform], local);
        var world = parentWorld.Multiply(matrix);
        worlds[node.Transform] = world;
        return world;
    }

    private static HumanDescription Description(SceneLookLook look, SceneLookRigPlan plan, Dictionary<long, string> names, Dictionary<long, Matrix4x4> locals)
    {
        var description = new HumanDescription
        {
            upperArmTwist = 0.5f,
            lowerArmTwist = 0.5f,
            upperLegTwist = 0.5f,
            lowerLegTwist = 0.5f,
            armStretch = 0.05f,
            legStretch = 0.05f,
        };
        var skeleton = new SkeletonBone[look.Nodes.Count];
        for (int i = 0; i < skeleton.Length; i++)
        {
            var node = look.Nodes[i];
            var matrix = locals[node.Transform];
            skeleton[i] = new SkeletonBone
            {
                name = names[node.Transform],
                position = new Vector3(matrix.m03, matrix.m13, matrix.m23),
                rotation = matrix.rotation,
                scale = matrix.lossyScale,
            };
        }
        description.skeleton = skeleton;
        var human = new HumanBone[plan.Roles.Count];
        for (int i = 0; i < human.Length; i++)
            human[i] = new HumanBone { boneName = plan.Roles[i].LookName, humanName = plan.Roles[i].HumanName, limit = new HumanLimit { useDefaultValues = true } };
        description.human = human;
        return description;
    }

    private static Dictionary<string, Transform> RoleBones(SceneLookLook look, SceneLookRigPlan plan, Dictionary<long, Transform> nodes)
    {
        var byName = new Dictionary<string, Transform>();
        foreach (var node in look.Nodes)
        {
            if (nodes.TryGetValue(node.Transform, out var placed) && !byName.ContainsKey(node.Name))
                byName[node.Name] = placed;
        }
        var roles = new Dictionary<string, Transform>();
        foreach (var role in plan.Roles)
        {
            if (byName.TryGetValue(role.LookName, out var bone))
                roles[role.HumanName] = bone;
        }
        return roles;
    }

    private static Transform RootNode(SceneLookLook look, SceneLookRigPlan plan, Dictionary<long, Transform> nodes, Transform fallback)
    {
        foreach (var role in plan.Roles)
        {
            if (role.HumanName != "Hips")
                continue;
            foreach (var node in look.Nodes)
            {
                if (node.Name == role.LookName && nodes.TryGetValue(node.Transform, out var found))
                    return found;
            }
        }
        return fallback;
    }
}
