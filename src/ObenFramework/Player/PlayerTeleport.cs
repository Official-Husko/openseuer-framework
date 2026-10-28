using System;
using UnityEngine;

namespace ObenFramework.Player;

/// <summary>
/// Moves the player the way the game's own chair and ladder code does: write the position directly,
/// never copy a target's scale (<c>PlayerLocator.Teleport</c> does, which shrinks or stretches the
/// player), stand up first if seated, and kill momentum. No collision or ground check is done.
/// </summary>
public static class PlayerTeleport
{
    /// <summary>
    /// Puts the player at a world position, optionally turning them and the camera. Throws when
    /// there is no player or a level change, load or arrest is running (see <see cref="CanTeleport"/>).
    /// </summary>
    /// <param name="position">World position of the player's feet.</param>
    /// <param name="yaw">World yaw to face, in degrees. Null keeps the current facing.</param>
    /// <param name="pitch">Camera pitch in degrees, positive looking down. Null keeps it unless a yaw is given (then 0).</param>
    public static void To(Vector3 position, float? yaw = null, float? pitch = null)
    {
        if (!CanTeleport(out var reason))
            throw new InvalidOperationException(reason);

        var locator = PlayerRef.Require();
        StandUp();

        var body = locator.transform;
        body.position = position;
        if (yaw.HasValue)
            body.rotation = Quaternion.Euler(0f, yaw.Value, 0f);

        var rigidbody = locator.GetComponent<Rigidbody>();
        if (rigidbody != null)
        {
            rigidbody.position = body.position;
            rigidbody.rotation = body.rotation;
            // Setting a velocity on a kinematic body (seated, noclip) only logs a warning.
            if (!rigidbody.isKinematic)
            {
                rigidbody.linearVelocity = Vector3.zero;
                rigidbody.angularVelocity = Vector3.zero;
            }
        }

        SnapCamera(yaw, pitch);
        KeepForSave(locator);
        Physics.SyncTransforms();
    }

    /// <summary>
    /// Puts the player at a transform's position, facing the way it faces. The target's scale is never
    /// used, so any marker object is safe to pass.
    /// </summary>
    public static void To(Transform target)
    {
        if (target == null)
            throw new ArgumentNullException(nameof(target));
        To(target.position, target.eulerAngles.y);
    }

    /// <summary>False, with the reason, when moving the player now would be unsafe.</summary>
    public static bool CanTeleport(out string reason)
    {
        if (!PlayerRef.Exists)
            reason = "There is no player right now (main menu or loading).";
        else if (PlayerState.IsTransitioning)
            reason = "A level change, load or arrest is in progress.";
        else
            reason = null;
        return reason == null;
    }

    /// <summary>Casts straight down from above <paramref name="near"/> and returns the first surface hit.</summary>
    public static bool TryFindGround(Vector3 near, out Vector3 ground, float maxDistance = 200f)
    {
        var start = near + Vector3.up * 2f;
        if (Physics.Raycast(start, Vector3.down, out var hit, maxDistance, ~0, QueryTriggerInteraction.Ignore))
        {
            ground = hit.point;
            return true;
        }
        ground = near;
        return false;
    }

    /// <summary>Gets the player off the chair or bed they are on, and back under their normal parent.</summary>
    private static void StandUp()
    {
        if (InteractableChair.PlayerIsSitting && InteractObjects.instance != null)
            (InteractObjects.instance.savedInteractable as InteractableChair)?.ForceStandUp();

        var locator = PlayerLocator.instance;
        if (locator.root != null && locator.transform.parent != locator.root)
            locator.transform.SetParent(locator.root, true);
    }

    private static void SnapCamera(float? yaw, float? pitch)
    {
        var rig = ThirdPersonCameraController.instance;
        if (rig == null)
            return;

        if (yaw.HasValue)
        {
            // The rig looks back along its own yaw: the game's PlayerLocator adds 180 degrees to face a
            // location's direction. The angle is also negated by the invert-X option (see its LateUpdate).
            float rigYaw = yaw.Value + 180f;
            rig.rotY = rig.InvertX ? -rigYaw : rigYaw;
            rig.rotX = pitch ?? 0f;
        }
        else if (pitch.HasValue)
        {
            rig.rotX = pitch.Value;
        }
        rig.MoveToTarget();
    }

    // The player's PersistLocation is what a save right after the teleport would write.
    private static void KeepForSave(PlayerLocator locator)
    {
        var persist = locator.GetComponent<PersistLocation>();
        if (persist == null)
            return;
        var body = locator.transform;
        persist.pos = persist.local ? body.localPosition : body.position;
        persist.rot = persist.local ? body.localEulerAngles : body.eulerAngles;
    }
}
