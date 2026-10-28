using System;
using System.Linq;
using DevBridge.Server;
using Newtonsoft.Json.Linq;
using ObenFramework.Player;
using static DevBridge.Commands.PlayerAbilityCommands;

namespace DevBridge.Commands;

/// <summary>Held tools, jobs, doctors and achievements. Read-mostly.</summary>
internal static class PlayerGearCommands
{
    [BridgeCommand("player.weapons", "Tools in both hands, the flashlight, and melee hit multipliers; set the bridge's own.",
        Params = new[] { "target?: Other, Mining, Chopping, Breakable or Animal", "multiplier?: 0-10 on target", "flashlight?: true or false", "reload?: true reloads the main hand" })]
    private static object WeaponsCommand(JObject p)
    {
        if (p["target"] != null && p["multiplier"] != null)
            Weapons.SetHitMultiplier(Owner(), ParseEnum<HitTarget>(p.Str("target")), p.Float("multiplier"));
        if (p["flashlight"] != null && !Weapons.SetFlashlight(p.Bool("flashlight")))
            throw new InvalidOperationException("No flashlight in hand, or items can't be used now.");
        bool reloaded = p.Bool("reload", false) && Weapons.Reload();

        var multipliers = new JObject();
        foreach (HitTarget target in Enum.GetValues(typeof(HitTarget)))
            multipliers[target.ToString()] = Weapons.HitMultiplier(target);

        return new JObject
        {
            ["right"] = Describe(Weapons.Held()),
            ["left"] = Describe(Weapons.HeldLeft()),
            ["flashlightOn"] = Weapons.FlashlightOn,
            ["flashlightCharge"] = Weapons.FlashlightCharge,
            ["reloaded"] = reloaded,
            ["hitMultipliers"] = multipliers,
        };
    }

    [BridgeCommand("player.jobs", "Work spots of the level with pay, skill and shift length.")]
    private static object Jobs(JObject p) => new JArray(ObenFramework.Player.Jobs.All().Select(j => new JObject
    {
        ["title"] = j.Title,
        ["address"] = j.Address.ToString(),
        ["coinsPerHour"] = j.CoinsPerHour,
        ["realMoneyPerHour"] = j.RealMoneyPerHour,
        ["skill"] = j.Skill.ToString(),
        ["requiredSkill"] = j.RequiredSkill,
        ["hardness"] = j.Hardness,
        ["shiftMinutes"] = j.ShiftMinutes,
    }).ToArray());

    [BridgeCommand("player.medical", "Treatments the level's doctors sell (only the open doctor's while the menu is up).")]
    private static object Medical(JObject p) => new JObject
    {
        ["menuOpen"] = ObenFramework.Player.Medical.MenuOpen,
        ["treatments"] = new JArray(ObenFramework.Player.Medical.Treatments().Select(t => new JObject
        {
            ["kind"] = t.Kind.ToString(),
            ["title"] = t.Title,
            ["price"] = t.Price,
            ["currency"] = t.Currency.ToString(),
            ["offered"] = t.Offered,
        }).ToArray()),
    };

    [BridgeCommand("player.achievements", "The game's achievements with progress and state. Read-only (they are Steam achievements).",
        Params = new[] { "unlocked?: true or false to filter" })]
    private static object Achievements(JObject p)
    {
        var all = GameAchievements.All().AsEnumerable();
        if (p["unlocked"] != null)
        {
            bool unlocked = p.Bool("unlocked");
            all = all.Where(a => a.Unlocked == unlocked);
        }
        return new JArray(all.Select(a => new JObject
        {
            ["id"] = a.Id,
            ["unlocked"] = a.Unlocked,
            ["progress"] = a.Progress,
            ["max"] = a.MaxProgress,
            ["enabled"] = a.Enabled,
        }).ToArray());
    }

    private static JObject Describe(WeaponInfo weapon)
    {
        if (weapon == null)
            return null;
        return new JObject
        {
            ["itemId"] = weapon.ItemId,
            ["title"] = weapon.Title,
            ["kind"] = weapon.Kind.ToString(),
            ["ammo"] = weapon.AmmoLoaded,
            ["capacity"] = weapon.AmmoCapacity,
            ["component"] = weapon.Component != null ? weapon.Component.GetType().Name : null,
        };
    }
}
