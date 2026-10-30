// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
using System;
using System.Collections.Generic;

namespace ObenFramework.SceneLook;

/// <summary>
/// Decides how a look goes onto a body, from bone names alone: by name when every bone is there, by
/// retargeting when the body is humanoid and the look covers the 15 required roles, or onto the body's
/// own skeleton by name and role. Unity-free; the body side is passed in as names and roles.
/// </summary>
internal static class SceneLookRig
{
    public const string HeadAliasFrom = "mr_duck_head";
    public const string HeadAliasTo = "Bip001 Head";
    public const string RootAlias = "Bip001_root";
    public const string RootBone = "Bip001 Pelvis";
    public const int RequiredRoles = 15;

    /// <summary>The name or retarget path for <paramref name="look"/> on a body with these transform names.</summary>
    public static SceneLookRigPlan Decide(SceneLookLook look, ICollection<string> targetNames, string targetRootName, string avatarKind)
    {
        var plan = new SceneLookRigPlan();
        if (!Usable(look, targetNames, plan))
            return plan;
        targetRootName ??= "";
        var targets = new HashSet<string>(targetNames);
        var nodes = new HashSet<long>();
        if (look.Nodes != null)
        {
            foreach (var node in look.Nodes)
                nodes.Add(node.Transform);
        }

        var names = new HashSet<string>();
        var order = new List<string>();
        foreach (var renderer in look.Renderers)
        {
            foreach (var bone in renderer.Bones)
            {
                plan.LookSlots++;
                if (nodes.Contains(bone.Transform.PathId))
                    plan.InSkeleton++;
                if (names.Add(bone.Name))
                    order.Add(bone.Name);
            }
        }
        plan.LookBones = order.Count;

        foreach (string name in order)
        {
            string target = name;
            bool alias = false;
            if (!targets.Contains(target))
            {
                target = Alias(name, targetRootName);
                alias = target != name;
            }
            if (!targets.Contains(target))
            {
                plan.Unmatched.Add(name);
                continue;
            }
            plan.Bones.Add(new SceneLookRigBone { LookName = name, TargetName = target, Alias = alias });
            if (alias)
                plan.Aliases.Add(name + " -> " + target);
        }
        CoverRoles(plan, names);

        if (plan.Unmatched.Count == 0)
        {
            plan.Path = SceneLookRigPath.Name;
            return plan;
        }
        if (avatarKind != "humanoid")
        {
            plan.Reason = "target is not humanoid and names do not match";
            return plan;
        }
        if (plan.InSkeleton < plan.LookSlots)
        {
            plan.Reason = "bone outside the look skeleton";
            return plan;
        }
        if (plan.RequiredCovered != RequiredRoles)
        {
            plan.Reason = "no rig path: the look covers " + plan.RequiredCovered + " of the 15 humanoid roles";
            return plan;
        }
        Duplicates(look, plan);
        if (plan.Duplicates.Count > 0)
        {
            plan.Reason = "the look skeleton has duplicate object names: " + string.Join(", ", plan.Duplicates.ToArray());
            return plan;
        }
        plan.Path = SceneLookRigPath.Retarget;
        return plan;
    }

    /// <summary>
    /// The skeleton path: every look bone maps to a body bone of the same name or, failing that, to the body
    /// bone the body's avatar uses for the same humanoid role (<paramref name="avatarRoles"/>: role to bone name).
    /// </summary>
    public static SceneLookRigPlan Skeleton(SceneLookLook look, ICollection<string> targetNames, Dictionary<string, string> avatarRoles)
    {
        var plan = new SceneLookRigPlan();
        if (!Usable(look, targetNames, plan))
            return plan;
        var targets = new HashSet<string>(targetNames);
        var names = new HashSet<string>();
        var order = new List<string>();
        foreach (var renderer in look.Renderers)
        {
            foreach (var bone in renderer.Bones)
            {
                if (names.Add(bone.Name))
                    order.Add(bone.Name);
            }
        }
        plan.LookBones = order.Count;

        foreach (string name in order)
        {
            string target = null;
            bool byRole = false;
            if (targets.Contains(name))
            {
                target = name;
            }
            else
            {
                string role = RoleOf(name);
                if (role != null && avatarRoles != null && avatarRoles.TryGetValue(role, out string bodyBone) && targets.Contains(bodyBone))
                {
                    target = bodyBone;
                    byRole = true;
                }
            }
            if (target == null)
            {
                plan.Unmatched.Add(name);
                continue;
            }
            plan.Bones.Add(new SceneLookRigBone { LookName = name, TargetName = target });
            if (byRole)
                plan.RoleMapped++;
            else
                plan.NameMapped++;
        }
        CoverRoles(plan, names);

        if (plan.Unmatched.Count > 0)
        {
            plan.Reason = "the look bones cannot all be mapped to the body skeleton: " + Names(plan.Unmatched);
            return plan;
        }
        plan.Path = SceneLookRigPath.Skeleton;
        return plan;
    }

    /// <summary>Humanoid role to bone name for a set of bone names, by the role table.</summary>
    public static Dictionary<string, string> Roles(ICollection<string> names)
    {
        var roles = new Dictionary<string, string>();
        if (names == null)
            return roles;
        var set = new HashSet<string>(names);
        foreach (var role in SceneLookRoles.Table)
        {
            string bone = Match(role, set);
            if (bone != null)
                roles[role[0]] = bone;
        }
        return roles;
    }

    /// <summary>The humanoid role a bone name fills, or null.</summary>
    public static string RoleOf(string name)
    {
        foreach (var role in SceneLookRoles.Table)
        {
            for (int i = 1; i < role.Length; i++)
            {
                if (role[i] == name)
                    return role[0];
            }
        }
        return null;
    }

    /// <summary>The body bone name a look bone stands for when its own name is missing.</summary>
    public static string Alias(string name, string targetRootName)
    {
        if (name == HeadAliasFrom)
            return HeadAliasTo;
        if (name == RootAlias)
            return targetRootName;
        return name;
    }

    public static bool RequiredRole(string humanName) => Array.IndexOf(SceneLookRoles.Required, humanName) >= 0;

    private static bool Usable(SceneLookLook look, ICollection<string> targetNames, SceneLookRigPlan plan)
    {
        if (look == null)
        {
            plan.Reason = "no look";
            return false;
        }
        if (targetNames == null || targetNames.Count == 0)
        {
            plan.Reason = "the target root has no names";
            return false;
        }
        return true;
    }

    private static void CoverRoles(SceneLookRigPlan plan, HashSet<string> names)
    {
        foreach (var role in SceneLookRoles.Table)
        {
            string lookBone = Match(role, names);
            if (lookBone != null)
            {
                plan.Roles.Add(new SceneLookRigRole { HumanName = role[0], LookName = lookBone });
                plan.Covered.Add(role[0]);
            }
            else if (RequiredRole(role[0]))
            {
                plan.Missing.Add(role[0]);
            }
        }
        plan.RequiredCovered = SceneLookRoles.Required.Length - plan.Missing.Count;
    }

    private static string Names(List<string> names)
    {
        int count = Math.Min(names.Count, 5);
        string text = string.Join(", ", names.GetRange(0, count).ToArray());
        return names.Count > count ? text + ", ..." : text;
    }

    private static string Match(string[] role, HashSet<string> names)
    {
        for (int i = 1; i < role.Length; i++)
        {
            if (names.Contains(role[i]))
                return role[i];
        }
        return null;
    }

    // Only the bones and the nodes above them make the avatar; other objects (footstep audio, the clothes'
    // own objects) may share names, and the rebuilt skeleton numbers those apart (SceneLookChain.Names).
    private static void Duplicates(SceneLookLook look, SceneLookRigPlan plan)
    {
        if (look.Nodes == null)
            return;
        var chain = SceneLookChain.Bones(look);
        var seen = new HashSet<string>();
        foreach (var node in look.Nodes)
        {
            if (chain.Contains(node.Transform) && !seen.Add(node.Name) && !plan.Duplicates.Contains(node.Name))
                plan.Duplicates.Add(node.Name);
        }
    }
}
