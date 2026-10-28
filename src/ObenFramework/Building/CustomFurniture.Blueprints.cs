// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
using System;
using System.Collections.Generic;
using ObenFramework.Player;
using ObenFramework.Saves;
using ObenLoader;

namespace ObenFramework.Building;

public static partial class CustomFurniture
{
    /// <summary>The general pool of found blueprints.</summary>
    public const string GeneralPool = FurniturePools.General;

    /// <summary>The pool of the first blueprints a new game hands out.</summary>
    public const string StartPool = FurniturePools.Start;

    /// <summary>
    /// Gives a custom piece a blueprint and returns the blueprint's id (<c>&lt;furniture id&gt;/blueprint</c>). The
    /// blueprint crafts the piece at the game's workbenches, can be found as loot when it names pools, and lasts
    /// as long as the piece's definition. Throws when the definition is invalid or the piece has one already.
    /// </summary>
    public static string DefineBlueprint(ObenMod owner, string furnitureId, BlueprintDefinition definition)
    {
        if (owner == null)
            throw new ArgumentNullException(nameof(owner));
        if (definition == null)
            throw new ArgumentNullException(nameof(definition));
        var target = FurnitureRegistry.Find(furnitureId) ?? throw new ArgumentException($"Furniture {furnitureId} isn't defined.", nameof(furnitureId));
        if (target.ModId != owner.Info.Id)
            throw new ArgumentException($"Furniture {furnitureId} belongs to {target.ModId}; only it can give the piece a blueprint.", nameof(furnitureId));
        if (target.Blueprint != null)
            throw new ArgumentException($"Furniture {furnitureId} already has a blueprint.", nameof(furnitureId));

        if (!Enum.IsDefined(typeof(PlayerStats.PlayerSkillType), definition.Skill))
            throw new ArgumentException("Skill is not a skill.");
        string problem = FurnitureIds.CheckBlueprint(definition.Seconds, definition.Amount, definition.Skill != PlayerStats.PlayerSkillType.None,
                definition.MinimumSkill, PlayerSkills.MaxLevel)
            ?? FurnitureIds.CheckInputs(definition.Items, definition.Furniture)
            ?? FurnitureIds.CheckPools(definition.Pools);
        if (problem != null)
            throw new ArgumentException(problem);

        FurnitureBlueprints.Add(target, new BlueprintDefinition
        {
            Title = definition.Title, Details = definition.Details, Icon = definition.Icon,
            Items = new Dictionary<int, int>(definition.Items), Furniture = new List<string>(definition.Furniture),
            Seconds = definition.Seconds, Skill = definition.Skill, MinimumSkill = definition.MinimumSkill, Amount = definition.Amount,
            Pools = new List<string>(definition.Pools),
        });
        return target.Blueprint.Id;
    }

    /// <summary>The game blueprint of a custom piece (built now if needed), or null.</summary>
    public static TaskItemBlueprint BlueprintOf(string furnitureId)
    {
        var entry = FurnitureRegistry.Find(furnitureId)?.Blueprint;
        return entry != null ? FurnitureBlueprints.Registered(entry) : null;
    }

    /// <summary>True when the player holds the piece's blueprint.</summary>
    public static bool IsBlueprintKnown(string furnitureId)
    {
        string blueprintId = FurnitureIds.BlueprintFor(furnitureId);
        return blueprintId != null && FurnitureBlueprints.IsKnown(blueprintId);
    }

    /// <summary>Hands the player the piece's blueprint, with the game's notification unless <paramref name="quiet"/>. False when they have it already.</summary>
    public static bool GiveBlueprint(string furnitureId, bool quiet = false)
    {
        var blueprint = BlueprintOf(furnitureId);
        if (blueprint == null || SaveGate.IsBusy || TaskItemsManager.instance == null || IsBlueprintKnown(furnitureId))
            return false;
        return quiet ? TaskItemsManager.instance.AddTaskItem(blueprint, 1) : blueprint.GiveItemWithResults();
    }

    /// <summary>Sends the blueprint to the player by mail (or as a parcel). False when they have it already.</summary>
    public static bool MailBlueprint(string furnitureId, string sender, bool parcel = false)
    {
        var blueprint = BlueprintOf(furnitureId);
        if (blueprint == null || SaveGate.IsBusy || MailController.instance == null || IsBlueprintKnown(furnitureId))
            return false;
        MailController.instance.SendMail(string.IsNullOrEmpty(sender) ? blueprint.itemName : sender, blueprint,
            parcel ? MailController.Target.PlayerParcelLocker : MailController.Target.Player);
        return true;
    }
}
