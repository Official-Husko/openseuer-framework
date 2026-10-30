using System.Collections.Generic;
using HarmonyLib;
using NPC;
using UnityEngine;

namespace ObenFramework.Npcs;

// The game's AI bodies have no sit or lie-down states: a chair or bed sets its flag on the Animator and the
// body goes on standing where it was put. Once a sit or sleep order has its NPC in place, it plays a pose of
// its own over the body's animation, and takes it off when the order ends.
internal static partial class NpcOrders
{
    private static readonly AccessTools.FieldRef<Act_sit, bool> AtChair = AccessTools.FieldRefAccess<Act_sit, bool>("atChair");
    private static readonly AccessTools.FieldRef<Act_Sleep, bool> AtBed = AccessTools.FieldRefAccess<Act_Sleep, bool>("atBed");

    // Humanoid bodies take the game's humanoid clips (the bed ones are the player's, always loaded); the one
    // generic AI body (Jaana Ahovaara's) has its own sit and sleep clips.
    private static readonly string[] HumanSit = { "os_chair_sit_001", "sitting_idle" };
    private static readonly string[] HumanSleep = { "Sleep_Bed_LeftSide_SleepLoop", "Sleep_Bed_RightSide_SleepLoop" };
    private static readonly Dictionary<string, AnimationClip> Loaded = new Dictionary<string, AnimationClip>();

    // How long a pose takes to fade in before the body is laid on its bed.
    private const float FitAfterSeconds = 0.5f;

    // The hips rest this far above a mattress, and the body's middle is this far from the hips toward the head.
    private const float HipsAboveBed = 0.12f;
    private const float MiddleTowardHead = 0.15f;

    private static void KeepPose(NpcOrder order)
    {
        if (order.PoseTried)
        {
            if (order.Pose != null && !order.Fitted && Time.time - order.PoseStarted >= FitAfterSeconds && order.Activity is Act_Sleep lying)
            {
                order.Fitted = FitToBed(order, order.Pose.Animator, lying.bed != null ? lying.bed.sleep : null);
            }
            return;
        }
        bool seated = order.Activity is Act_sit sit ? AtChair(sit) : order.Activity is Act_Sleep sleep && AtBed(sleep);
        if (!seated)
            return;
        order.PoseTried = true;
        var animator = order.Npc.Root.GetComponentInChildren<Animator>();
        var clip = animator != null ? PoseClip(animator, order.Kind == NpcStepKind.Sit) : null;
        if (clip == null)
        {
            NpcsLog.WarnOnce("pose:" + order.Npc.Body, $"The body of character {order.Npc.Body} has no clip to {(order.Kind == NpcStepKind.Sit ? "sit" : "lie down")} with; it stands at the {(order.Kind == NpcStepKind.Sit ? "chair" : "bed")}.");
            return;
        }
        order.Pose = NpcAnimation.Start(order.Npc.Owner, animator, clip, loop: true, fadeSeconds: 0.4f);
        order.PoseStarted = Time.time;
    }

    private static void DropPose(NpcOrder order)
    {
        order.Pose?.Stop(0.3f);
        order.Pose = null;
        if (order.Model != null)
        {
            order.Model.localPosition = order.ModelPosition;
            order.Model.localRotation = order.ModelRotation;
            order.Model = null;
        }
    }

    // The bed clips are the player's, made for lying down from a bed's side; the bed puts the NPC at its
    // middle. Turn the lying body along the bed's long side, centre it and rest it on top. Only the body's
    // model moves: the NPC itself has to stay where the bed put it, or the sleep walks it back.
    private static bool FitToBed(NpcOrder order, Animator animator, Sleep bed)
    {
        var root = order.Npc.Root != null ? order.Npc.Root.transform : null;
        if (bed == null || animator == null || !animator.isHuman || root == null || animator.transform == root)
            return true;
        // The bed holds its sleeper as a child; only the bed's own meshes count.
        Bounds bounds = default;
        bool found = false;
        foreach (var renderer in bed.GetComponentsInChildren<Renderer>())
        {
            if (renderer.transform.IsChildOf(root))
                continue;
            if (found)
                bounds.Encapsulate(renderer.bounds);
            else
                bounds = renderer.bounds;
            found = true;
        }
        var hips = animator.GetBoneTransform(HumanBodyBones.Hips);
        var head = animator.GetBoneTransform(HumanBodyBones.Head);
        if (!found || hips == null || head == null)
            return true;

        var model = animator.transform;
        order.Model = model;
        order.ModelPosition = model.localPosition;
        order.ModelRotation = model.localRotation;
        var along = head.position - hips.position;
        along.y = 0f;
        var axis = bounds.extents.x >= bounds.extents.z ? Vector3.right : Vector3.forward;
        float angle = Vector3.SignedAngle(along, axis, Vector3.up);
        if (Mathf.Abs(angle) > 90f)
            angle -= 180f * Mathf.Sign(angle);
        model.RotateAround(hips.position, Vector3.up, angle);

        var middle = hips.position + (head.position - hips.position) * MiddleTowardHead;
        model.position += new Vector3(bounds.center.x - middle.x, bounds.max.y + HipsAboveBed - hips.position.y, bounds.center.z - middle.z);
        return true;
    }

    private static AnimationClip PoseClip(Animator animator, bool sit)
    {
        var own = animator.runtimeAnimatorController != null ? animator.runtimeAnimatorController.animationClips : new AnimationClip[0];
        if (!animator.isHuman)
        {
            string word = sit ? "_sit_" : "_sleep_";
            foreach (var clip in own)
            {
                if (clip != null && !clip.humanMotion && clip.name.Contains(word))
                    return clip;
            }
            return null;
        }
        foreach (string name in sit ? HumanSit : HumanSleep)
        {
            foreach (var clip in own)
            {
                if (clip != null && clip.humanMotion && clip.name == name)
                    return clip;
            }
            var found = LoadedClip(name);
            if (found != null)
                return found;
        }
        return null;
    }

    private static AnimationClip LoadedClip(string name)
    {
        if (Loaded.TryGetValue(name, out var cached) && cached != null)
            return cached;
        foreach (var clip in Resources.FindObjectsOfTypeAll<AnimationClip>())
        {
            if (clip != null && clip.humanMotion && clip.name == name)
            {
                Loaded[name] = clip;
                return clip;
            }
        }
        return null;
    }
}
