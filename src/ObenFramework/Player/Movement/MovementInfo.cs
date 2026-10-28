namespace ObenFramework.Player;

/// <summary>The player's movement speeds (metres per second) and what the controller is doing.</summary>
public sealed class MovementInfo
{
    public float WalkSpeed { get; private set; }

    /// <summary>Running speed, including the orange mushroom's boost when that is active.</summary>
    public float RunSpeed { get; private set; }

    public float SwimSpeed { get; private set; }
    public float CrouchSpeed { get; private set; }
    public float BackwardSpeed { get; private set; }
    public float StrafeSpeed { get; private set; }

    /// <summary>Jump height in metres.</summary>
    public float JumpHeight { get; private set; }

    public bool IsCrouching { get; private set; }

    /// <summary>The walk toggle is on (the player walks instead of running).</summary>
    public bool IsWalking { get; private set; }

    public bool IsSprinting { get; private set; }
    public bool IsGrounded { get; private set; }

    /// <summary>Something (a menu, dialogue, sleep, a chair) has the movement locked.</summary>
    public bool ControlsDisabled { get; private set; }

    internal static MovementInfo Read(OpenSewerCharacterController controller)
    {
        float run = MovementSpeeds.RawRun(controller);
        float swim = MovementSpeeds.RawSwim(controller);
        var orange = OrangeMushroomEffect.instance;
        if (orange != null)
        {
            float boost = orange.GetRunSpeedModifier(run);
            run += boost;
            swim += boost;
        }

        return new MovementInfo
        {
            WalkSpeed = controller.walkSpeed,
            RunSpeed = run,
            SwimSpeed = swim,
            CrouchSpeed = controller.crouchSpeed,
            BackwardSpeed = controller.backwardSpeed,
            StrafeSpeed = controller.strafeSpeed,
            JumpHeight = controller.baseJumpHeight,
            IsCrouching = controller.isCrouching,
            IsWalking = MovementSpeeds.Walking(controller),
            IsSprinting = Sprints(controller),
            IsGrounded = controller.movement != null && controller.isGrounded,
            ControlsDisabled = controller.ControlsDisabled(),
        };
    }

    internal static MovementInfo FromBase(OpenSewerCharacterController controller, MovementSpeeds.Speeds speeds) => new MovementInfo
    {
        WalkSpeed = speeds.Walk,
        RunSpeed = speeds.Run,
        SwimSpeed = speeds.Swim,
        CrouchSpeed = speeds.Crouch,
        BackwardSpeed = speeds.Backward,
        StrafeSpeed = speeds.Strafe,
        JumpHeight = speeds.Jump,
        IsCrouching = controller.isCrouching,
        IsWalking = MovementSpeeds.Walking(controller),
        IsSprinting = Sprints(controller),
        IsGrounded = controller.movement != null && controller.isGrounded,
        ControlsDisabled = controller.ControlsDisabled(),
    };

    // The same test the controller's GetTargetSpeed makes before it picks the run speed.
    internal static bool Sprints(OpenSewerCharacterController controller)
    {
        var input = InputManager.instance;
        bool gamepad = input != null && input.inputType == InputManager.InputType.Controller;
        return (!MovementSpeeds.Walking(controller) || gamepad) && !controller.isCrouching;
    }
}
