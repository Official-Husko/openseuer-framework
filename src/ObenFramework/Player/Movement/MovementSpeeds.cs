// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
using System;
using ECM.Controllers;
using HarmonyLib;
using ObenFramework.Events;

namespace ObenFramework.Player;

/// <summary>
/// Writes <see cref="PlayerMovement"/>'s multipliers into the player's character controller. The game
/// builds a new controller for every level and never changes its speeds afterwards, so the base values
/// are read once per controller, before anything of ours touches them, and every write is base times
/// multiplier. The backing fields are read through cached field refs; writes go through the
/// controller's own setters.
/// </summary>
internal static class MovementSpeeds
{
    private static readonly AccessTools.FieldRef<OpenSewerCharacterController, float> Walk =
        AccessTools.FieldRefAccess<OpenSewerCharacterController, float>("_walkSpeed");
    private static readonly AccessTools.FieldRef<OpenSewerCharacterController, float> Run =
        AccessTools.FieldRefAccess<OpenSewerCharacterController, float>("_runSpeed");
    private static readonly AccessTools.FieldRef<OpenSewerCharacterController, float> Swim =
        AccessTools.FieldRefAccess<OpenSewerCharacterController, float>("_swimmingSpeed");
    private static readonly AccessTools.FieldRef<OpenSewerCharacterController, float> Crouch =
        AccessTools.FieldRefAccess<OpenSewerCharacterController, float>("_crouchSpeed");
    private static readonly AccessTools.FieldRef<OpenSewerCharacterController, float> Backward =
        AccessTools.FieldRefAccess<OpenSewerCharacterController, float>("_backwardSpeed");
    private static readonly AccessTools.FieldRef<OpenSewerCharacterController, float> Strafe =
        AccessTools.FieldRefAccess<OpenSewerCharacterController, float>("_strafeSpeed");
    private static readonly AccessTools.FieldRef<BaseCharacterController, float> Jump =
        AccessTools.FieldRefAccess<BaseCharacterController, float>("_baseJumpHeight");

    internal static readonly AccessTools.FieldRef<OpenSewerCharacterController, bool> Walking =
        AccessTools.FieldRefAccess<OpenSewerCharacterController, bool>("walking");

    private static OpenSewerCharacterController _applied;
    private static Speeds _base;

    /// <summary>The game's own values for the current controller, before any multiplier.</summary>
    internal static Speeds Base => Capture(OpenSewerCharacterController.instance);

    /// <summary>Raw run speed of a controller, without the orange mushroom bonus the property adds.</summary>
    internal static float RawRun(OpenSewerCharacterController controller) => Run(controller);

    internal static float RawSwim(OpenSewerCharacterController controller) => Swim(controller);

    /// <summary>Brings the controller in line with the multipliers. Safe to call with null.</summary>
    internal static void Apply(OpenSewerCharacterController controller)
    {
        if (controller == null)
            return;
        try
        {
            if (controller != _applied)
            {
                // Nothing to do for a fresh controller while no mod changes anything.
                if (PlayerMovement.Table.IsEmpty)
                    return;
                _base = Read(controller);
                _applied = controller;
            }

            var table = PlayerMovement.Table;
            float all = table.Product((int)MovementStat.Speed);
            controller.walkSpeed = _base.Walk * all * table.Product((int)MovementStat.Walk);
            controller.runSpeed = _base.Run * all * table.Product((int)MovementStat.Run);
            controller.swimmingSpeed = _base.Swim * all * table.Product((int)MovementStat.Swim);
            controller.crouchSpeed = _base.Crouch * all * table.Product((int)MovementStat.Crouch);
            controller.backwardSpeed = _base.Backward * all;
            controller.strafeSpeed = _base.Strafe * all;
            controller.baseJumpHeight = _base.Jump * table.Product((int)MovementStat.Jump);
        }
        catch (Exception e)
        {
            GameEvents.Log.Error("Applying the movement multipliers failed", e);
        }
    }

    /// <summary>Puts the game's own values back on the controller we changed (used on unload).</summary>
    internal static void Restore()
    {
        var controller = _applied;
        _applied = null;
        if (controller == null)
            return;
        controller.walkSpeed = _base.Walk;
        controller.runSpeed = _base.Run;
        controller.swimmingSpeed = _base.Swim;
        controller.crouchSpeed = _base.Crouch;
        controller.backwardSpeed = _base.Backward;
        controller.strafeSpeed = _base.Strafe;
        controller.baseJumpHeight = _base.Jump;
    }

    private static Speeds Capture(OpenSewerCharacterController controller)
    {
        if (controller == null)
            return default;
        return controller == _applied ? _base : Read(controller);
    }

    private static Speeds Read(OpenSewerCharacterController controller) => new Speeds
    {
        Walk = Walk(controller),
        Run = Run(controller),
        Swim = Swim(controller),
        Crouch = Crouch(controller),
        Backward = Backward(controller),
        Strafe = Strafe(controller),
        Jump = Jump(controller),
    };

    internal struct Speeds
    {
        public float Walk;
        public float Run;
        public float Swim;
        public float Crouch;
        public float Backward;
        public float Strafe;
        public float Jump;
    }
}
