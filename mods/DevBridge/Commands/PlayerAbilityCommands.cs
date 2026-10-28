using System;
using DevBridge.Server;
using Newtonsoft.Json.Linq;
using ObenFramework.Player;
using UnityEngine;

namespace DevBridge.Commands;

/// <summary>Movement speeds, skills, effects and conditions. Changes made here belong to the bridge mod.</summary>
internal static class PlayerAbilityCommands
{
    [BridgeCommand("player.speed", "Current and base movement speeds, every mod's multiplier, and set or clear the bridge's own.",
        Params = new[] { "stat?: Speed, Walk, Run, Swim, Crouch or Jump", "multiplier?: 0-10, sets the bridge's multiplier on stat", "clear?: true removes the bridge's multiplier on stat, or all of them without stat" })]
    private static object Speed(JObject p)
    {
        if (p["stat"] != null)
        {
            var stat = ParseEnum<MovementStat>(p.Str("stat"));
            if (p.Bool("clear", false))
                PlayerMovement.Remove(Owner(), stat);
            else if (p["multiplier"] != null)
                PlayerMovement.Set(Owner(), stat, p.Float("multiplier"));
        }
        else if (p.Bool("clear", false))
        {
            foreach (MovementStat stat in Enum.GetValues(typeof(MovementStat)))
                PlayerMovement.Remove(Owner(), stat);
        }

        var multipliers = new JArray();
        foreach (var entry in PlayerMovement.Active)
            multipliers.Add(new JObject { ["mod"] = entry.OwnerId, ["stat"] = ((MovementStat)entry.Channel).ToString(), ["key"] = entry.Key, ["value"] = entry.Value });
        var combined = new JObject();
        foreach (MovementStat stat in Enum.GetValues(typeof(MovementStat)))
            combined[stat.ToString()] = PlayerMovement.Multiplier(stat);

        return new JObject
        {
            ["current"] = Describe(PlayerMovement.Current),
            ["base"] = Describe(PlayerMovement.Base),
            ["combined"] = combined,
            ["multipliers"] = multipliers,
        };
    }

    [BridgeCommand("player.skills", "Every game skill and custom skill with its value; set or raise one, scale learning.",
        Params = new[] { "skill?: a game skill (Cooking) or a custom one (modId:skillId)", "value?: set it quietly", "add?: raise it like practice (banner, achievements)", "learning?: the bridge's learning multiplier for skill (or all with skill=None)" })]
    private static object Skills(JObject p)
    {
        if (p["skill"] != null)
            ChangeSkill(p);

        var game = new JObject();
        foreach (var skill in PlayerSkills.All())
            game[skill.Skill.ToString()] = Round(skill.Value);
        var custom = new JArray();
        foreach (var skill in CustomSkills.All)
            custom.Add(new JObject { ["id"] = skill.ModId + ":" + skill.Id, ["name"] = skill.DisplayName, ["value"] = Round(skill.Value), ["level"] = skill.Level, ["max"] = skill.Max });
        var learning = new JArray();
        foreach (var entry in PlayerSkills.LearningMultipliers)
            learning.Add(new JObject { ["mod"] = entry.OwnerId, ["skill"] = ((PlayerStats.PlayerSkillType)entry.Channel).ToString(), ["value"] = entry.Value });

        return new JObject { ["maxLevel"] = PlayerSkills.MaxLevel, ["skills"] = game, ["custom"] = custom, ["learning"] = learning };
    }

    [BridgeCommand("player.effects", "Active effects with their strength (0-1); start or stop the wasteland.",
        Params = new[] { "wasteland?: true sends the player there, false brings them back" })]
    private static object Effects(JObject p)
    {
        if (p["wasteland"] != null)
        {
            bool ok = p.Bool("wasteland") ? PlayerEffects.StartWasteland() : PlayerEffects.StopWasteland();
            if (!ok)
                throw new InvalidOperationException("The wasteland can't be changed now.");
        }

        var effects = new JObject();
        foreach (PlayerEffect effect in Enum.GetValues(typeof(PlayerEffect)))
            effects[effect.ToString()] = new JObject { ["active"] = PlayerEffects.IsActive(effect), ["strength"] = Round(PlayerEffects.Strength(effect)) };
        return effects;
    }

    [BridgeCommand("player.conditions", "Health conditions the player has or can get; add or remove one by asset name.",
        Params = new[] { "add?: condition asset name", "remove?: condition asset name" })]
    private static object Conditions(JObject p)
    {
        if (p["add"] != null && !PlayerConditions.Add(p.Str("add")))
            throw new InvalidOperationException($"Couldn't add '{p.Str("add")}' (unknown name or no player).");
        if (p["remove"] != null && !PlayerConditions.Remove(p.Str("remove")))
            throw new InvalidOperationException($"Couldn't remove '{p.Str("remove")}' (not active).");

        var list = new JArray();
        foreach (var condition in PlayerConditions.All())
        {
            list.Add(new JObject
            {
                ["name"] = condition.Name,
                ["title"] = condition.DisplayName,
                ["active"] = condition.Active,
                ["count"] = condition.Count,
                ["strength"] = Round(condition.Strength),
                ["stacks"] = condition.CanStack,
                ["hidden"] = condition.Hidden,
            });
        }
        return list;
    }

    private static void ChangeSkill(JObject p)
    {
        string name = p.Str("skill");
        int colon = name.LastIndexOf(':');
        if (colon > 0)
        {
            var custom = CustomSkills.Find(name.Substring(0, colon), name.Substring(colon + 1))
                         ?? throw new ArgumentException($"No custom skill '{name}' is registered.");
            if (p["value"] != null)
                custom.Value = p.Float("value");
            if (p["add"] != null)
                custom.Add(p.Float("add"));
            return;
        }

        var skill = ParseEnum<PlayerStats.PlayerSkillType>(name);
        if (p["learning"] != null)
            PlayerSkills.SetLearningMultiplier(Owner(), p.Float("learning"), skill);
        if (skill == PlayerStats.PlayerSkillType.None)
            return;
        if (p["value"] != null && PlayerSkills.Set(skill, p.Float("value")) == null)
            throw new InvalidOperationException("There is no player right now (main menu or loading).");
        if (p["add"] != null)
            PlayerSkills.Add(skill, p.Float("add"));
    }

    private static JObject Describe(MovementInfo info)
    {
        if (info == null)
            return null;
        return new JObject
        {
            ["walk"] = Round(info.WalkSpeed),
            ["run"] = Round(info.RunSpeed),
            ["swim"] = Round(info.SwimSpeed),
            ["crouch"] = Round(info.CrouchSpeed),
            ["backward"] = Round(info.BackwardSpeed),
            ["strafe"] = Round(info.StrafeSpeed),
            ["jump"] = Round(info.JumpHeight),
            ["crouching"] = info.IsCrouching,
            ["walking"] = info.IsWalking,
            ["sprinting"] = info.IsSprinting,
            ["grounded"] = info.IsGrounded,
            ["controlsDisabled"] = info.ControlsDisabled,
        };
    }

    internal static T ParseEnum<T>(string text) where T : struct, Enum =>
        Enum.TryParse(text, true, out T value) && Enum.IsDefined(typeof(T), value)
            ? value
            : throw new ArgumentException($"Unknown {typeof(T).Name} '{text}'. Known: {string.Join(", ", Enum.GetNames(typeof(T)))}");

    internal static DevBridgeMod Owner() =>
        DevBridgeMod.Instance ?? throw new InvalidOperationException("The bridge mod isn't loaded.");

    internal static float Round(float value) => Mathf.Round(value * 1000f) / 1000f;
}
