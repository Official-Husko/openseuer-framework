// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
using System;
using System.Collections.Generic;
using UnityEngine;

namespace ObenFramework.SceneLook;

/// <summary>What the apply side needs to know about a live body: its names, avatar, scale and materials.</summary>
internal static class SceneLookRigTools
{
    public static List<string> Names(GameObject body)
    {
        var transforms = body.GetComponentsInChildren<Transform>(true);
        var names = new List<string>(transforms.Length);
        foreach (var transform in transforms)
            names.Add(transform.name);
        return names;
    }

    /// <summary>"humanoid", "generic" or "none", from the body's animators.</summary>
    public static string AvatarKind(GameObject body)
    {
        if (HumanAvatar(body) != null)
            return "humanoid";
        foreach (var animator in body.GetComponentsInChildren<Animator>(true))
        {
            if (animator.avatar != null)
                return "generic";
        }
        return "none";
    }

    public static Avatar HumanAvatar(GameObject body)
    {
        var animator = HumanAnimator(body);
        return animator != null ? animator.avatar : null;
    }

    public static Animator HumanAnimator(GameObject body)
    {
        foreach (var animator in body.GetComponentsInChildren<Animator>(true))
        {
            var avatar = animator.avatar;
            if (avatar != null && avatar.isHuman)
                return animator;
        }
        return null;
    }

    /// <summary>The first transform named <paramref name="name"/> under <paramref name="root"/>, depth first.</summary>
    public static Transform Find(Transform root, string name)
    {
        if (root.name == name)
            return root;
        for (int i = 0; i < root.childCount; i++)
        {
            var found = Find(root.GetChild(i), name);
            if (found != null)
                return found;
        }
        return null;
    }

    /// <summary>Humanoid role to bone name, as the body's avatar maps them.</summary>
    public static Dictionary<string, string> AvatarRoles(Animator animator)
    {
        var roles = new Dictionary<string, string>();
        if (animator == null)
            return roles;
        string[] bones = HumanTrait.BoneName;
        if (bones == null)
            return roles;
        int count = Math.Min(bones.Length, (int)HumanBodyBones.LastBone);
        for (int i = 0; i < count; i++)
        {
            string humanName = bones[i];
            if (string.IsNullOrEmpty(humanName) || roles.ContainsKey(humanName))
                continue;
            var bone = animator.GetBoneTransform((HumanBodyBones)i);
            if (bone != null)
                roles[humanName] = bone.name;
        }
        return roles;
    }

    public static SceneLookRigPlan SkeletonPlan(SceneLookLook look, GameObject body)
    {
        var animator = HumanAnimator(body);
        if (animator == null)
            return new SceneLookRigPlan { Reason = "the body is not humanoid" };
        return SceneLookRig.Skeleton(look, Names(body), AvatarRoles(animator));
    }

    public static SceneLookRigPlan Plan(SceneLookLook look, GameObject body) =>
        SceneLookRig.Decide(look, Names(body), body.name, AvatarKind(body));

    public static float BodyScale(GameObject body)
    {
        var scale = body.transform.lossyScale;
        return (Math.Abs(scale.x) + Math.Abs(scale.y) + Math.Abs(scale.z)) / 3f;
    }

    /// <summary>The first material on any of the body's renderers; its shader is preferred for the look.</summary>
    public static Material TargetMaterial(GameObject body)
    {
        foreach (var renderer in body.GetComponentsInChildren<Renderer>(true))
        {
            var materials = renderer.sharedMaterials;
            if (materials == null)
                continue;
            foreach (var material in materials)
            {
                if (material != null)
                    return material;
            }
        }
        return null;
    }

    /// <summary>The object the look is built under, named after the look's root, as a child of the body.</summary>
    public static GameObject Holder(SceneLookLook look, GameObject body, SceneLookApplied state)
    {
        var node = SceneLookFrame.Node(look, look.Root);
        var holder = Child(node != null ? node.Name : look.FileName, body.transform, state);
        state.Holder = holder;
        return holder;
    }

    /// <summary>A new child object, recorded so it is destroyed when the look comes off.</summary>
    public static GameObject Child(string name, Transform parent, SceneLookApplied state)
    {
        var created = new GameObject(name);
        created.transform.SetParent(parent, false);
        created.layer = parent.gameObject.layer;
        state.Objects.Add(created);
        return created;
    }

    public static void PlaceMatrix(Transform transform, Matrix4x4 matrix)
    {
        transform.localPosition = new Vector3(matrix.m03, matrix.m13, matrix.m23);
        transform.localRotation = matrix.rotation;
        transform.localScale = matrix.lossyScale;
    }

    public static Matrix4x4 Local(SceneLookNode node) =>
        Matrix4x4.TRS(Vector(node.Position), new Quaternion(node.Rotation[0], node.Rotation[1], node.Rotation[2], node.Rotation[3]), Vector(node.Scale));

    private static Vector3 Vector(float[] values) => new Vector3(values[0], values[1], values[2]);
}
