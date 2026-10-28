// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
using System;
using HarmonyLib;
using ObenFramework.Events;
using OS.Items;
using Detonator = WieldableItem.Detonator;
using MeleeAnimationEvents = WieldableItem.MeleeAnimationEvents;
using MeleeWeapon = WieldableItem.MeleeWeapon;

namespace ObenFramework.Player;

/// <summary>Raises the <see cref="Weapons"/> events from the detonator and the melee animation.</summary>
[HarmonyPatch]
internal static class WeaponPatches
{
    [HarmonyPrefix]
    [HarmonyPatch(typeof(Detonator), nameof(Detonator.Onpressed))]
    private static void BeforePress(ItemAmmodata ___ammo, out float __state) => __state = ___ammo != null ? ___ammo.ammo : 0f;

    [HarmonyPostfix]
    [HarmonyPatch(typeof(Detonator), nameof(Detonator.Onpressed))]
    private static void AfterPress(Detonator __instance, float __state)
    {
        if (__state > 0f)
            Raise(Weapons.Fired, __instance);
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(Detonator), "Reload")]
    private static void AfterReload(Detonator __instance) => Raise(Weapons.Reloaded, __instance);

    [HarmonyPostfix]
    [HarmonyPatch(typeof(MeleeAnimationEvents), nameof(MeleeAnimationEvents.EnableHit))]
    private static void AfterEnableHit(MeleeWeapon ___meleeWeapon)
    {
        if (___meleeWeapon != null)
            Raise(Weapons.MeleeSwung, ___meleeWeapon);
    }

    private static void Raise(GameEvent<WeaponInfo> evt, PlayerHandItems.IPlayerHandItem hand)
    {
        try
        {
            var hands = PlayerHandItems.instance;
            var info = Weapons.Describe(hand, hands != null && ReferenceEquals(hands.lefthandItem, hand));
            if (info != null)
                evt.Raise(info, GameEvents.Log);
        }
        catch (Exception e)
        {
            GameEvents.Log.Error("Reporting a weapon use failed", e);
        }
    }
}
