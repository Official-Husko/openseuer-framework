// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
using System.Collections.Generic;

namespace ObenFramework.SceneLook;

/// <summary>How a look is put on a body.</summary>
internal enum SceneLookRigPath
{
    /// <summary>It can't be.</summary>
    Refuse,

    /// <summary>Every look bone has a bone of the same name (or a known alias) on the body; skin straight onto it.</summary>
    Name,

    /// <summary>Build the look's own skeleton and avatar under the body and copy the body's pose onto it each frame.</summary>
    Retarget,

    /// <summary>Skin onto the body's skeleton, matching bones by name or by humanoid role.</summary>
    Skeleton,
}

internal sealed class SceneLookRigBone
{
    public string LookName;
    public string TargetName;
    public bool Alias;
}

internal sealed class SceneLookRigRole
{
    public string HumanName;
    public string LookName;
}

/// <summary>The decision of <see cref="SceneLookRig"/>, with enough detail to explain a refusal.</summary>
internal sealed class SceneLookRigPlan
{
    public SceneLookRigPath Path = SceneLookRigPath.Refuse;
    public string Reason;
    public List<SceneLookRigBone> Bones = new List<SceneLookRigBone>();
    public List<SceneLookRigRole> Roles = new List<SceneLookRigRole>();
    public List<string> Unmatched = new List<string>();
    public List<string> Aliases = new List<string>();
    public List<string> Covered = new List<string>();
    public List<string> Missing = new List<string>();
    public List<string> Duplicates = new List<string>();
    public int LookBones;
    public int LookSlots;
    public int InSkeleton;
    public int RequiredBones = SceneLookRig.RequiredRoles;
    public int RequiredCovered;
    public int NameMapped;
    public int RoleMapped;
}

/// <summary>The humanoid roles the game's rigs use, and which bone names fill them.</summary>
internal static class SceneLookRoles
{
    public static readonly string[] Required =
    {
        "Hips", "Spine", "Head", "LeftUpperLeg", "LeftLowerLeg", "LeftFoot", "RightUpperLeg", "RightLowerLeg", "RightFoot",
        "LeftUpperArm", "LeftLowerArm", "LeftHand", "RightUpperArm", "RightLowerArm", "RightHand",
    };

    /// <summary>
    /// Each row: the humanoid role, then the bone names that fill it (first match wins). The game has two rigs:
    /// the 3ds Max biped (Bip001) and a Blender one with dotted names, whose bone called Spine is the hips (the
    /// thighs hang off it) and whose Ankle bones are the feet; its Hip, Chest and Wrist bones are helpers.
    /// </summary>
    public static readonly string[][] Table =
    {
        new[] { "Hips", "Bip001 Pelvis", "Spine" },
        new[] { "Spine", "Bip001 Spine", "Spine1" },
        new[] { "Chest", "Bip001 Spine1", "Spine2" },
        new[] { "Neck", "Bip001 Neck", "Neck" },
        new[] { "Head", "mr_duck_head", "Bip001 Head", "Head" },
        new[] { "LeftUpperLeg", "Bip001 L Thigh", "Thigh.L" },
        new[] { "LeftLowerLeg", "Bip001 L Calf", "Shin.L" },
        new[] { "LeftFoot", "Bip001 L Foot", "Ankle.L" },
        new[] { "RightUpperLeg", "Bip001 R Thigh", "Thigh.R" },
        new[] { "RightLowerLeg", "Bip001 R Calf", "Shin.R" },
        new[] { "RightFoot", "Bip001 R Foot", "Ankle.R" },
        new[] { "LeftShoulder", "Bip001 L Clavicle", "Shoulder.L" },
        new[] { "LeftUpperArm", "Bip001 L UpperArm", "Arm.L" },
        new[] { "LeftLowerArm", "Bip001 L Forearm", "Forearm.L" },
        new[] { "LeftHand", "Bip001 L Hand", "Hand.L" },
        new[] { "RightShoulder", "Bip001 R Clavicle", "Shoulder.R" },
        new[] { "RightUpperArm", "Bip001 R UpperArm", "Arm.R" },
        new[] { "RightLowerArm", "Bip001 R Forearm", "Forearm.R" },
        new[] { "RightHand", "Bip001 R Hand", "Hand.R" },
    };
}
