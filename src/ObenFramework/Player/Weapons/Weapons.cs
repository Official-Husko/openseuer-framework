// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
using System;
using HarmonyLib;
using ObenFramework.Events;
using ObenFramework.Items;
using ObenFramework.Saves;
using ObenLoader;
using ObenLoader.Runtime;
using OS.Items;
using UnityEngine;
using Detonator = WieldableItem.Detonator;
using Flashlight = WieldableItem.Flashlight;
using MeleeWeapon = WieldableItem.MeleeWeapon;

namespace ObenFramework.Player;

/// <summary>
/// What the player holds: the tool in each hand, ammo, reloading, the flashlight, events for swings,
/// detonations, reloads and hits, and multipliers on the damage of melee hits by what they land on
/// (rock, wood, crates and locks, animals), so a mod can make mining faster without touching the tools.
/// </summary>
public static class Weapons
{
    private static readonly AccessTools.FieldRef<Flashlight, bool> LightOn = AccessTools.FieldRefAccess<Flashlight, bool>("on");

    internal static readonly MultiplierTable HitMultipliers =
        new MultiplierTable(Enum.GetValues(typeof(HitTarget)).Length, PlayerMovement.MaxMultiplier);

    private static readonly HitRoundings Roundings = new HitRoundings();

    private static readonly OwnerWatch Owners = new OwnerWatch(id =>
    {
        HitMultipliers.RemoveOwner(id);
        Roundings.RemoveOwner(id);
    });

    /// <summary>The detonator was pressed with a charge loaded.</summary>
    public static readonly GameEvent<WeaponInfo> Fired = new GameEvent<WeaponInfo>(nameof(Fired));

    /// <summary>A melee swing reached the point where it can hit.</summary>
    public static readonly GameEvent<WeaponInfo> MeleeSwung = new GameEvent<WeaponInfo>(nameof(MeleeSwung));

    /// <summary>The detonator was reloaded.</summary>
    public static readonly GameEvent<WeaponInfo> Reloaded = new GameEvent<WeaponInfo>(nameof(Reloaded));

    /// <summary>A melee hit landed on a node, a breakable or an animal, with the damage before and after the multipliers.</summary>
    public static readonly GameEvent<WeaponHit> Hit = new GameEvent<WeaponHit>(nameof(Hit));

    /// <summary>The tool in the main (right) hand, or null.</summary>
    public static WeaponInfo Held()
    {
        var hands = PlayerHandItems.instance;
        return hands != null ? Describe(hands.righthandItem, false) : null;
    }

    /// <summary>The tool in the off (left) hand, usually a flashlight, or null.</summary>
    public static WeaponInfo HeldLeft()
    {
        var hands = PlayerHandItems.instance;
        return hands != null ? Describe(hands.lefthandItem, true) : null;
    }

    /// <summary>How many of an ammo item the player carries.</summary>
    public static int Ammo(int ammoItemId) => PlayerItems.Count(ammoItemId);

    /// <summary>Reloads the main-hand tool from the inventory like the reload key. False when nothing was loaded.</summary>
    public static bool Reload()
    {
        var hands = PlayerHandItems.instance;
        var held = Held();
        if (hands == null || held == null || held.AmmoCapacity <= 0 || hands.itemUseDisabled || MenuOpen() || SaveGate.IsBusy)
            return false;
        int before = AmmoCarried(hands.righthandItem);
        hands.ReloadUse();
        return AmmoCarried(hands.righthandItem) < before;
    }

    /// <summary>Whether a held flashlight is lit, or null without one.</summary>
    public static bool? FlashlightOn
    {
        get
        {
            var light = HeldFlashlight();
            return light != null ? LightOn(light) : (bool?)null;
        }
    }

    /// <summary>A held flashlight's battery, 0 to 1, or null without one.</summary>
    public static float? FlashlightCharge
    {
        get
        {
            var light = HeldFlashlight();
            var ammo = light != null && light.meta != null ? ItemMetaUtilities.GetMetaOfType<ItemAmmodata>(light.meta) : null;
            return ammo != null && ammo.maxAmmo > 0f ? ammo.ammo / ammo.maxAmmo : (float?)null;
        }
    }

    /// <summary>Switches a held flashlight. False without one or when the player can't use items now.</summary>
    public static bool SetFlashlight(bool on)
    {
        var hands = PlayerHandItems.instance;
        var light = HeldFlashlight();
        if (hands == null || light == null || hands.itemUseDisabled || MenuOpen() || SaveGate.IsBusy)
            return false;
        if (on)
            light.TurnOn();
        else
            light.TurnOff();
        return LightOn(light) == on;
    }

    /// <summary>
    /// Scales the damage of the player's melee hits on one kind of target: 2 breaks rock twice as fast,
    /// 0.5 makes crates sturdier. Fractions round up by chance, so averages stay right. Mods' multipliers
    /// combine by multiplying. Dispose the result to take it away; it also goes when the mod unloads.
    /// </summary>
    public static IDisposable SetHitMultiplier(ObenMod owner, HitTarget target, float multiplier, string key = null) =>
        SetHitMultiplier(owner, target, multiplier, HitRounding.Chance, key);

    /// <summary>
    /// The same with a choice of how the scaled damage is rounded. When mods ask for different roundings on
    /// one target, the multiplier set most recently decides.
    /// </summary>
    public static IDisposable SetHitMultiplier(ObenMod owner, HitTarget target, float multiplier, HitRounding rounding, string key = null)
    {
        if (!Enum.IsDefined(typeof(HitTarget), target))
            throw new ArgumentOutOfRangeException(nameof(target));
        if (!Enum.IsDefined(typeof(HitRounding), rounding))
            throw new ArgumentOutOfRangeException(nameof(rounding));
        if (!HitMultipliers.Accepts(multiplier))
            throw new ArgumentOutOfRangeException(nameof(multiplier), $"A multiplier must be a number from 0 to {HitMultipliers.Max}.");
        string id = Owners.Watch(owner);
        HitMultipliers.Set(id, (int)target, key, multiplier);
        Roundings.Set(id, (int)target, key, rounding);
        return Disposable.Create(() =>
        {
            HitMultipliers.Remove(id, (int)target, key);
            Roundings.Remove(id, (int)target, key);
        });
    }

    /// <summary>The combined multiplier on a target (1 when none).</summary>
    public static float HitMultiplier(HitTarget target) => HitMultipliers.Product((int)target);

    /// <summary>How hits on a target are rounded right now (<see cref="HitRounding.Chance"/> when no mod chose).</summary>
    public static HitRounding HitRoundingOf(HitTarget target) => Roundings.For((int)target);

    /// <summary>Every hit multiplier mods have set. <see cref="MultiplierEntry.Channel"/> is a <see cref="HitTarget"/>.</summary>
    public static MultiplierEntry[] ActiveHitMultipliers => HitMultipliers.Entries;

    internal static void Init(ObenMod framework) => framework.Track(Disposable.Create(() =>
    {
        HitMultipliers.Clear();
        Roundings.Clear();
        Owners.Clear();
    }));

    internal static WeaponInfo Describe(PlayerHandItems.IPlayerHandItem hand, bool leftHand)
    {
        var component = hand as Component;
        if (component == null || hand.item == null)
            return null;
        return new WeaponInfo(hand, leftHand);
    }

    internal static WeaponKind KindOf(PlayerHandItems.IPlayerHandItem hand)
    {
        if (hand is MeleeWeapon)
            return WeaponKind.Melee;
        return hand is Detonator ? WeaponKind.Explosive : WeaponKind.None;
    }

    private static Flashlight HeldFlashlight()
    {
        var hands = PlayerHandItems.instance;
        if (hands == null)
            return null;
        if (hands.righthandItem is Flashlight right && right != null)
            return right;
        return hands.lefthandItem is Flashlight left && left != null ? left : null;
    }

    private static int AmmoCarried(PlayerHandItems.IPlayerHandItem hand)
    {
        var ammo = hand != null && hand.meta != null ? ItemMetaUtilities.GetMetaOfType<ItemAmmodata>(hand.meta) : null;
        if (ammo == null || ammo.ammoItems == null)
            return 0;
        int total = 0;
        foreach (var reference in ammo.ammoItems)
            total += PlayerItems.Count(reference.ID);
        return total;
    }

    private static bool MenuOpen() => GameUIController.instance != null && GameUIController.instance.IsMenuVisible;
}
