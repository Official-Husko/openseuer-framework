// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
using System;
using ObenLoader;
using ObenLoader.Runtime;

namespace ObenFramework.Player;

/// <summary>What a <see cref="PlayerMovement"/> multiplier scales.</summary>
public enum MovementStat
{
    /// <summary>Every movement speed: walking, running, swimming, sneaking, backwards and sideways. Not the jump.</summary>
    Speed,
    Walk,
    Run,
    Swim,
    /// <summary>Sneaking speed.</summary>
    Crouch,
    /// <summary>Jump height.</summary>
    Jump,
}

/// <summary>
/// The player's movement speeds and jump height. Mods set multipliers instead of writing speeds, so
/// several mods can change movement at once without undoing each other: every mod's multipliers on a
/// stat are multiplied together, and when a mod removes its own (or unloads) the others stay. The
/// result is re-applied to the new controller the game builds on every level load.
/// </summary>
public static class PlayerMovement
{
    /// <summary>Largest multiplier one mod may set, and the cap on the combined value of a stat.</summary>
    public const float MaxMultiplier = 10f;

    internal static readonly MultiplierTable Table =
        new MultiplierTable(Enum.GetValues(typeof(MovementStat)).Length, MaxMultiplier);

    private static readonly OwnerWatch Owners = new OwnerWatch(id =>
    {
        if (Table.RemoveOwner(id) > 0)
            MovementSpeeds.Apply(OpenSewerCharacterController.instance);
    });

    /// <summary>
    /// Sets this mod's multiplier on a stat: 1 changes nothing, 2 doubles, 0.5 halves, 0 stops it. The same
    /// owner, stat and <paramref name="key"/> again replaces the earlier value, so calling this from a
    /// slider is fine. Dispose the result (or call <see cref="Remove"/>) to take it away; everything a mod
    /// set goes away when it unloads. Works at the main menu too and applies once there is a player.
    /// </summary>
    /// <param name="key">Only needed when one mod keeps several multipliers on the same stat.</param>
    public static IDisposable Set(ObenMod owner, MovementStat stat, float multiplier, string key = null)
    {
        CheckStat(stat);
        if (!Table.Accepts(multiplier))
            throw new ArgumentOutOfRangeException(nameof(multiplier), $"A multiplier must be a number from 0 to {MaxMultiplier}.");

        string id = Owners.Watch(owner);
        Table.Set(id, (int)stat, key, multiplier);
        MovementSpeeds.Apply(OpenSewerCharacterController.instance);
        return Disposable.Create(() => Remove(id, stat, key));
    }

    /// <summary>Takes this mod's multiplier on a stat away. False when it had none.</summary>
    public static bool Remove(ObenMod owner, MovementStat stat, string key = null)
    {
        if (owner == null)
            throw new ArgumentNullException(nameof(owner));
        return Remove(owner.Info.Id, stat, key);
    }

    /// <summary>The combined multiplier on a stat (1 when no mod set one).</summary>
    public static float Multiplier(MovementStat stat)
    {
        CheckStat(stat);
        return Table.Product((int)stat);
    }

    /// <summary>Every multiplier mods have set. <see cref="MultiplierEntry.Channel"/> is a <see cref="MovementStat"/>.</summary>
    public static MultiplierEntry[] Active => Table.Entries;

    /// <summary>Speeds and movement flags right now, or null without a player.</summary>
    public static MovementInfo Current
    {
        get
        {
            var controller = OpenSewerCharacterController.instance;
            return controller != null ? MovementInfo.Read(controller) : null;
        }
    }

    /// <summary>The game's own speeds for this level before any multiplier, or null without a player.</summary>
    public static MovementInfo Base
    {
        get
        {
            var controller = OpenSewerCharacterController.instance;
            return controller != null ? MovementInfo.FromBase(controller, MovementSpeeds.Base) : null;
        }
    }

    /// <summary>Standing on something (not in the air or falling).</summary>
    public static bool IsGrounded
    {
        get
        {
            var controller = OpenSewerCharacterController.instance;
            return controller != null && controller.movement != null && controller.isGrounded;
        }
    }

    /// <summary>Running rather than walking when moving: the walk toggle is off (or a gamepad is used) and not sneaking.</summary>
    public static bool IsSprinting
    {
        get
        {
            var controller = OpenSewerCharacterController.instance;
            return controller != null && MovementInfo.Sprints(controller);
        }
    }

    /// <summary>Makes the player sneak. False without a player or when the game refused.</summary>
    public static bool Crouch()
    {
        var controller = OpenSewerCharacterController.instance;
        if (controller == null || !controller.isActiveAndEnabled)
            return false;
        if (!controller.isCrouching)
            controller.Crouch();
        return controller.isCrouching;
    }

    /// <summary>Stands the player up from sneaking. False when there is no room to stand.</summary>
    public static bool StandUp()
    {
        var controller = OpenSewerCharacterController.instance;
        if (controller == null || !controller.isActiveAndEnabled)
            return false;
        if (controller.isCrouching)
            controller.StandUp();
        return !controller.isCrouching;
    }

    internal static void Init(ObenMod framework) => framework.Track(Disposable.Create(() =>
    {
        MovementSpeeds.Restore();
        Table.Clear();
        Owners.Clear();
    }));

    private static bool Remove(string ownerId, MovementStat stat, string key)
    {
        CheckStat(stat);
        if (!Table.Remove(ownerId, (int)stat, key))
            return false;
        MovementSpeeds.Apply(OpenSewerCharacterController.instance);
        return true;
    }

    private static void CheckStat(MovementStat stat)
    {
        if (stat < MovementStat.Speed || stat > MovementStat.Jump)
            throw new ArgumentOutOfRangeException(nameof(stat));
    }
}
