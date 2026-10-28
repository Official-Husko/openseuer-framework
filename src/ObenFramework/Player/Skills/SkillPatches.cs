// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
using System;
using HarmonyLib;
using ObenFramework.Events;

namespace ObenFramework.Player;

/// <summary>
/// Every skill gain in the game goes through <c>PlayerStats.IncreaseSkill</c>: scale positive gains by the
/// learning multipliers before it runs, and report the change after.
/// </summary>
[HarmonyPatch(typeof(PlayerStats), nameof(PlayerStats.IncreaseSkill))]
internal static class SkillPatches
{
    [HarmonyPrefix]
    private static void Before(PlayerStats.PlayerSkillType Type, ref float amount, out float __state)
    {
        __state = float.NaN;
        try
        {
            if (amount > 0f && !PlayerSkills.Gains.IsEmpty)
                amount *= PlayerSkills.LearningMultiplier(Type);
            var entry = PlayerSkills.Entry(Type);
            if (entry != null)
                __state = entry.Skill;
        }
        catch (Exception e)
        {
            GameEvents.Log.Error("Preparing a skill gain failed", e);
        }
    }

    [HarmonyPostfix]
    private static void After(PlayerStats.PlayerSkillType Type, float __state)
    {
        if (float.IsNaN(__state))
            return;
        var entry = PlayerSkills.Entry(Type);
        if (entry != null && entry.Skill != __state)
            PlayerSkills.Changed.Raise(new SkillChange(Type, __state, entry.Skill), GameEvents.Log);
    }
}
