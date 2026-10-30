// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
using System;
using HarmonyLib;
using ObenFramework.Events;
using UnityEngine;
using MeleeWeapon = WieldableItem.MeleeWeapon;

namespace ObenFramework.Player;

/// <summary>
/// Applies <see cref="Weapons.SetHitMultiplier"/> and raises <see cref="Weapons.Hit"/>. A melee hit is
/// the player's MeleeWeapon.Hit calling Damage on what the ray hit, so damage is only scaled while that
/// call runs; damage from anything else is left alone. The prefixes run first so other mods see the
/// scaled damage. Each patch names the Damage(int, Vector3, Vector3, DamageType) overload hits use.
/// </summary>
[HarmonyPatch]
internal static class HitPatches
{
    private static readonly AccessTools.FieldRef<WorkableResourceSource, PlayerStats.PlayerSkillType> NodeSkill =
        AccessTools.FieldRefAccess<WorkableResourceSource, PlayerStats.PlayerSkillType>("skillType");

    private static MeleeWeapon _hitter;

    [HarmonyPrefix]
    [HarmonyPatch(typeof(MeleeWeapon), "Hit")]
    private static void BeforeHit(MeleeWeapon __instance) => _hitter = __instance;

    [HarmonyFinalizer]
    [HarmonyPatch(typeof(MeleeWeapon), "Hit")]
    private static void AfterHit() => _hitter = null;

    [HarmonyPrefix]
    [HarmonyPriority(Priority.First)]
    [HarmonyPatch(typeof(WorkableResourceSource), nameof(WorkableResourceSource.Damage), new[] { typeof(int), typeof(Vector3), typeof(Vector3), typeof(DamageReceiver.DamageType) })]
    private static void NodeDamage(WorkableResourceSource __instance, int ___currentStock, ref int damage, DamageReceiver.DamageType type)
    {
        if (_hitter != null && ___currentStock > 0)
            Scale(__instance, NodeTarget(__instance, type), ref damage, type);
    }

    [HarmonyPrefix]
    [HarmonyPriority(Priority.First)]
    [HarmonyPatch(typeof(Breakable), nameof(Breakable.Damage), new[] { typeof(int), typeof(Vector3), typeof(Vector3), typeof(DamageReceiver.DamageType) })]
    private static void BreakableDamage(Breakable __instance, ref int damage, DamageReceiver.DamageType type)
    {
        if (_hitter != null)
            Scale(__instance, HitTarget.Breakable, ref damage, type);
    }

    [HarmonyPrefix]
    [HarmonyPriority(Priority.First)]
    [HarmonyPatch(typeof(BreakableLock), nameof(BreakableLock.Damage), new[] { typeof(int), typeof(Vector3), typeof(Vector3), typeof(DamageReceiver.DamageType) })]
    private static void LockDamage(BreakableLock __instance, ref int damage, DamageReceiver.DamageType type)
    {
        if (_hitter != null)
            Scale(__instance, HitTarget.Breakable, ref damage, type);
    }

    [HarmonyPrefix]
    [HarmonyPriority(Priority.First)]
    [HarmonyPatch(typeof(SpawnItemOnBreak), nameof(SpawnItemOnBreak.Damage), new[] { typeof(int), typeof(Vector3), typeof(Vector3), typeof(DamageReceiver.DamageType) })]
    private static void SmashableDamage(SpawnItemOnBreak __instance, ref int damage, DamageReceiver.DamageType type)
    {
        if (_hitter != null)
            Scale(__instance, __instance.objectType == SpawnItemOnBreak.Type.Animal ? HitTarget.Animal : HitTarget.Breakable, ref damage, type);
    }

    private static HitTarget NodeTarget(WorkableResourceSource node, DamageReceiver.DamageType type)
    {
        if (NodeSkill(node) == PlayerStats.PlayerSkillType.Mining)
            return HitTarget.Mining;
        return type == DamageReceiver.DamageType.Chop ? HitTarget.Chopping : HitTarget.Other;
    }

    private static void Scale(Component receiver, HitTarget target, ref int damage, DamageReceiver.DamageType type)
    {
        try
        {
            if (receiver is IDamageReceiver damageable && !damageable.CheckDamageType(type))
                return;
            int baseDamage = damage;
            float multiplier = Weapons.HitMultiplier(target);
            if (multiplier != 1f)
                damage = DamageMath.Scale(damage, multiplier, UnityEngine.Random.value, Weapons.HitRoundingOf(target));

            var weapon = Weapons.Describe(_hitter, false);
            if (weapon != null)
                Weapons.Hit.Raise(new WeaponHit(weapon, target, receiver.gameObject, baseDamage, damage), GameEvents.Log);
        }
        catch (Exception e)
        {
            GameEvents.Log.Error("Scaling a melee hit failed", e);
        }
    }
}
