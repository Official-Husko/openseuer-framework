using System;

namespace ObenFramework.Player;

/// <summary>Flags about what the player is doing, plus the god mode switch. Reads are null-safe.</summary>
public static partial class PlayerState
{
    /// <summary>
    /// The game's god mode: death is blocked, but health still drops to 0. The flag is saved, so a
    /// loaded game can come back with it on.
    /// </summary>
    public static bool Godmode
    {
        get => PlayerStats.instance != null && PlayerStats.instance.Godmode;
        set
        {
            if (PlayerStats.instance == null)
                throw new InvalidOperationException("There is no player right now (main menu or loading).");
            PlayerStats.instance.Godmode = value;
        }
    }

    /// <summary>The death screen has been shown. The game has no respawn; loading a save is the way out.</summary>
    public static bool IsDead => Death.instance != null && Death.instance.dead;

    /// <summary>Sitting on a chair, sofa or exam bed.</summary>
    public static bool IsSeated => InteractableChair.PlayerIsSitting;

    /// <summary>Asleep or passed out.</summary>
    public static bool IsSleeping => WaitingController.instance != null && WaitingController.instance.IsSleeping;

    public static bool IsCrouching =>
        OpenSewerCharacterController.instance != null && OpenSewerCharacterController.instance.isCrouching;

    public static bool IsSwimming => WaterController.instance != null && WaterController.instance.playerIsSwimming;

    /// <summary>The player is in first person view.</summary>
    public static bool IsFirstPerson =>
        ThirdPersonCameraCollision.instance != null && ThirdPersonCameraCollision.instance.firstperson;

    /// <summary>
    /// Movement or keyboard input is locked by something (dialogue, sleep, a menu, a chair, a mod).
    /// The game keeps a list of lock owners; this only says whether the list is empty.
    /// </summary>
    public static bool InputLocked
    {
        get
        {
            var controller = GameController.instance;
            if (controller != null && (controller.keysDisabled || controller.writeLock))
                return true;
            var movement = OpenSewerCharacterController.instance;
            return movement != null && movement.ControlsDisabled();
        }
    }

    /// <summary>A level change, a save load or an arrest is under way, so moving the player is unsafe.</summary>
    public static bool IsTransitioning =>
        Changelevel.ChangeLevelActive || SaveController.Loading ||
        (Crime.instance != null && Crime.instance.ArrestInProgress);
}
