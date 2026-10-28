// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
using OS.Items;
using UnityEngine;

namespace ObenFramework.Player;

/// <summary>What sort of tool is in a hand.</summary>
public enum WeaponKind
{
    /// <summary>Not a weapon (a flashlight, a guitar, a plain tool).</summary>
    None,

    /// <summary>Anything swung: bats, axes, pickaxes, knives.</summary>
    Melee,

    /// <summary>The dynamite detonator.</summary>
    Explosive,
}

/// <summary>What a melee hit landed on, for <see cref="Weapons.SetHitMultiplier"/>.</summary>
public enum HitTarget
{
    /// <summary>A resource node that isn't rock or wood.</summary>
    Other,
    /// <summary>Rock and ore nodes.</summary>
    Mining,
    /// <summary>Wood, hit with a chopping tool.</summary>
    Chopping,
    /// <summary>Breakable things: crates, locks, windows.</summary>
    Breakable,
    /// <summary>Animals.</summary>
    Animal,
}

/// <summary>A tool in one of the player's hands, from <see cref="Weapons.Held"/>.</summary>
public sealed class WeaponInfo
{
    internal WeaponInfo(PlayerHandItems.IPlayerHandItem hand, bool leftHand)
    {
        var ammo = hand.meta != null ? ItemMetaUtilities.GetMetaOfType<ItemAmmodata>(hand.meta) : null;
        ItemId = hand.item != null ? hand.item.ID : -1;
        Title = hand.item != null ? hand.item.Title : "";
        Kind = Weapons.KindOf(hand);
        AmmoLoaded = ammo != null ? (int)ammo.ammo : 0;
        AmmoCapacity = ammo != null ? (int)ammo.maxAmmo : 0;
        LeftHand = leftHand;
        Component = hand as Component;
    }

    public int ItemId { get; }
    public string Title { get; }
    public WeaponKind Kind { get; }

    /// <summary>Charges loaded (dynamite sticks, battery for a flashlight in percent).</summary>
    public int AmmoLoaded { get; }

    /// <summary>0 for tools without ammo.</summary>
    public int AmmoCapacity { get; }

    public bool LeftHand { get; }

    /// <summary>The game's hand item component (a MeleeWeapon, Detonator, Flashlight...).</summary>
    public Component Component { get; }
}

/// <summary>A melee hit, from <see cref="Weapons.Hit"/>.</summary>
public readonly struct WeaponHit
{
    public WeaponHit(WeaponInfo weapon, HitTarget target, GameObject receiver, int baseDamage, int damage)
    {
        Weapon = weapon;
        Target = target;
        Receiver = receiver;
        BaseDamage = baseDamage;
        Damage = damage;
    }

    public WeaponInfo Weapon { get; }
    public HitTarget Target { get; }
    public GameObject Receiver { get; }

    /// <summary>The game's damage for the hit.</summary>
    public int BaseDamage { get; }

    /// <summary>The damage dealt after the hit multipliers.</summary>
    public int Damage { get; }
}
