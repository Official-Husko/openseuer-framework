// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
using System;
using System.Collections.Generic;
using HarmonyLib;
using ObenLoader;
using ObenLoader.Runtime;
using UnityEngine;
using UnityStandardAssets.Characters.ThirdPerson;

namespace ObenFramework.Player;

/// <summary>
/// Turns the player's view toward a target and holds it there, like the game does while you talk to someone,
/// in first and third person. The mouse can't turn the view away while a lock holds; walking still works, and in
/// first person the body turns with the view, so the player circles the target. Several mods can lock at once:
/// the newest lock whose target still exists wins, and the one before takes over when it ends.
/// </summary>
/// <remarks>
/// The game's own look-at (a talk, lockpicking, a card game's tutorial) and its frozen rotation go first; a lock
/// waits while they run. Nothing of the camera's saved state is changed, so a save made during a lock loads as
/// usual without the mod.
/// </remarks>
public static class CameraLook
{
    /// <summary>How fast the game turns the view toward what you talk to, in degrees per second.</summary>
    public const float GameTurnSpeed = 100f;

    private sealed class Hold
    {
        public string Owner;
        public Transform Target;
        public float Speed;
        public bool Snap;
    }

    private static readonly List<Hold> Holds = new List<Hold>();

    private static readonly Action<ThirdPersonCameraController> SetRotValues =
        AccessTools.MethodDelegate<Action<ThirdPersonCameraController>>(AccessTools.Method(typeof(ThirdPersonCameraController), "SetRotValues"));

    // Where the lock left the rig last frame, on which camera.
    private static Quaternion? _lastRotation;
    private static ThirdPersonCameraController _lastCamera;

    /// <summary>A mod holds a lock on a target that still exists (it waits while the game's own look-at runs).</summary>
    public static bool IsLocked => Current() != null;

    /// <summary>What the view is locked on, or null.</summary>
    public static Transform Target => Current()?.Target;

    /// <summary>
    /// Turns the view toward <paramref name="target"/> at <paramref name="degreesPerSecond"/> (the game's
    /// speed by default), or at once with <paramref name="instantly"/>, and keeps it there. Dispose the result
    /// to let go. The lock also ends when the target is destroyed (a level change destroys the scene's objects)
    /// and when the mod unloads.
    /// </summary>
    public static IDisposable LockOn(ObenMod owner, Transform target, bool instantly = false, float degreesPerSecond = GameTurnSpeed)
    {
        if (owner == null) throw new ArgumentNullException(nameof(owner));
        if (target == null) throw new ArgumentNullException(nameof(target));
        if (!(degreesPerSecond > 0f) || float.IsInfinity(degreesPerSecond))
            throw new ArgumentOutOfRangeException(nameof(degreesPerSecond), "The turn speed must be a positive number of degrees per second.");

        var hold = new Hold { Owner = owner.Info.Id, Target = target, Speed = degreesPerSecond, Snap = instantly };
        if (Current() == null)
            _lastRotation = null;
        Holds.Add(hold);
        return owner.Track(Disposable.Create(() => Holds.Remove(hold)));
    }

    /// <summary>Ends every lock of <paramref name="owner"/>. Returns how many there were.</summary>
    public static int Release(ObenMod owner)
    {
        if (owner == null) throw new ArgumentNullException(nameof(owner));
        return Holds.RemoveAll(h => h.Owner == owner.Info.Id);
    }

    internal static bool Any => Holds.Count > 0;

    /// <summary>
    /// Called after the camera's own LateUpdate; turns the rig toward the newest live target. Stands aside while
    /// <paramref name="gameLooks"/> (the game's own look-at or frozen rotation).
    /// </summary>
    internal static void Steer(ThirdPersonCameraController camera, bool gameLooks)
    {
        var hold = Current();
        if (hold == null || gameLooks || camera != _lastCamera)
        {
            _lastRotation = null;
            _lastCamera = camera;
            if (hold == null || gameLooks)
                return;
        }

        var rig = camera.transform;
        // The rig faces away from what the view looks at; the game builds the same rotation for its own look-at.
        var to = Quaternion.LookRotation(rig.position - hold.Target.position);
        if (PlayerCamera.instance != null)
            to *= Quaternion.Euler(PlayerCamera.instance.transform.localEulerAngles.x, 0f, 0f);
        float step = hold.Snap ? 360f : hold.Speed * Time.deltaTime;
        hold.Snap = false;
        // From last frame's rotation, not the one the game just built from the mouse, so the mouse can't pull it away.
        rig.rotation = Quaternion.RotateTowards(_lastRotation ?? rig.rotation, to, step);
        _lastRotation = rig.rotation;
        // The game's angles follow, so the view stays put when the lock ends.
        SetRotValues(camera);
        if (ThirdPersonCameraCollision.instance != null && ThirdPersonCameraCollision.instance.firstperson && FirstPersonSettings.instance != null)
            FirstPersonSettings.instance.RotateCharacter();
    }

    private static Hold Current()
    {
        for (int i = Holds.Count - 1; i >= 0; i--)
        {
            if (Holds[i].Target != null)
                return Holds[i];
            Holds.RemoveAt(i);
        }
        return null;
    }
}
