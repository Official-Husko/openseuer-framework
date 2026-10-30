using System.Collections.Generic;
using UnityEngine;

namespace ObenFramework.SceneLook;

/// <summary>
/// Stands a rebuilt look skeleton in the pose Unity's avatar builder takes for the T-pose: facing +Z with the
/// spine up, the legs straight down and the arms straight out to the sides. Skeletons come from the bind poses
/// of the look's meshes, and the game's Blender rig is bound with its arms down; built as it is, every pose
/// copied onto it would be off by that difference (the arms ending up over the head).
/// </summary>
internal static class SceneLookTPose
{
    private static readonly (string From, string To, Vector3 Direction)[] Chains =
    {
        ("Spine", "Chest", Vector3.up),
        ("Chest", "Neck", Vector3.up),
        ("Neck", "Head", Vector3.up),
        ("LeftUpperLeg", "LeftLowerLeg", Vector3.down),
        ("LeftLowerLeg", "LeftFoot", Vector3.down),
        ("RightUpperLeg", "RightLowerLeg", Vector3.down),
        ("RightLowerLeg", "RightFoot", Vector3.down),
        ("LeftShoulder", "LeftUpperArm", Vector3.left),
        ("LeftUpperArm", "LeftLowerArm", Vector3.left),
        ("LeftLowerArm", "LeftHand", Vector3.left),
        ("RightShoulder", "RightUpperArm", Vector3.right),
        ("RightUpperArm", "RightLowerArm", Vector3.right),
        ("RightLowerArm", "RightHand", Vector3.right),
    };

    /// <summary>Poses the bones (humanoid role to transform) under <paramref name="holder"/>.</summary>
    public static void Apply(Transform holder, IDictionary<string, Transform> roles)
    {
        Face(holder, roles);
        foreach (var (from, to, direction) in Chains)
            Point(holder, roles, from, to, direction);
    }

    // Turns everything under the holder about its up axis until the right hip is on +X, so the body faces +Z.
    private static void Face(Transform holder, IDictionary<string, Transform> roles)
    {
        if (!roles.TryGetValue("LeftUpperLeg", out var left) || !roles.TryGetValue("RightUpperLeg", out var right))
            return;
        var across = holder.InverseTransformDirection(right.position - left.position);
        across.y = 0f;
        if (across.sqrMagnitude < 1e-8f)
            return;
        float angle = Vector3.SignedAngle(across, Vector3.right, Vector3.up);
        if (Mathf.Abs(angle) < 0.1f)
            return;
        var children = new List<Transform>(holder.childCount);
        foreach (Transform child in holder)
            children.Add(child);
        foreach (var child in children)
            child.RotateAround(holder.position, holder.up, angle);
    }

    /// <summary>
    /// Where the hips stand in an avatar's reference pose, in its root's space, from the avatar's own skeleton
    /// description; zero when the hips aren't under <paramref name="root"/> or the description lacks a bone on the way.
    /// </summary>
    public static Vector3 Hips(Avatar avatar, Transform root, Transform hips)
    {
        var entries = new Dictionary<string, SkeletonBone>();
        foreach (var bone in avatar.humanDescription.skeleton)
        {
            if (!entries.ContainsKey(bone.name))
                entries[bone.name] = bone;
        }
        var path = new List<Transform>();
        var current = hips;
        for (; current != null && current != root; current = current.parent)
            path.Add(current);
        if (current == null)
            return Vector3.zero;
        var matrix = Matrix4x4.identity;
        for (int i = path.Count - 1; i >= 0; i--)
        {
            if (!entries.TryGetValue(path[i].name, out var bone))
                return Vector3.zero;
            matrix *= Matrix4x4.TRS(bone.position, bone.rotation, bone.scale);
        }
        return matrix.GetColumn(3);
    }

    private static void Point(Transform holder, IDictionary<string, Transform> roles, string from, string to, Vector3 direction)
    {
        if (!roles.TryGetValue(from, out var bone) || !roles.TryGetValue(to, out var next))
            return;
        var current = next.position - bone.position;
        if (current.sqrMagnitude < 1e-10f)
            return;
        bone.rotation = Quaternion.FromToRotation(current, holder.TransformDirection(direction)) * bone.rotation;
    }
}
