// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace ObenFramework.Player;

/// <summary>
/// The game camera's side of <see cref="CameraView"/>: lifting and restoring its <c>disableThirdPerson</c> and
/// <c>firstpersonOnly</c> locks, the camera triggers that force first person, building mode's zoom blockers, and
/// moving the camera in and out.
/// </summary>
internal static class CameraLocks
{
    // Below this the game switches to first person by itself.
    private const float MinShownDistance = 0.5f;

    private static readonly AccessTools.FieldRef<ThirdPersonCameraCollision, float> MinDistance =
        AccessTools.FieldRefAccess<ThirdPersonCameraCollision, float>("minDistance");
    private static readonly AccessTools.FieldRef<ThirdPersonCameraCollision, float> MaxDistance =
        AccessTools.FieldRefAccess<ThirdPersonCameraCollision, float>("maxDistance");
    private static readonly AccessTools.FieldRef<ThirdPersonCameraCollision, List<GameObject>> ZoomBlockers =
        AccessTools.FieldRefAccess<ThirdPersonCameraCollision, List<GameObject>>("disabledZoomList");

    private static readonly List<PlayerCameraTrigger> Forcing = new List<PlayerCameraTrigger>();

    // The camera whose locks were lifted, and the values they had before.
    private static ThirdPersonCameraCollision _unlocked;
    private static bool _hadDisableThirdPerson;
    private static bool _hadFirstPersonOnly;

    /// <summary>A third-person request waits because the game needs first person here.</summary>
    internal static bool HeldBack { get; private set; }

    /// <summary>No trigger forces first person and the player isn't building.</summary>
    internal static bool Allowed(ThirdPersonCameraCollision view) => !Forced() && ZoomBlockers(view).Count == 0;

    internal static void GoOut(ThirdPersonCameraCollision view, float distance)
    {
        HeldBack = false;
        Unlock(view);
        if (view.firstperson)
            view.onViewChangeTo1st = false;
        view.currentDistance = Mathf.Clamp(distance, Mathf.Max(MinDistance(view), MinShownDistance), MaxDistance(view));
    }

    /// <summary>The camera back to the head, and the game's locks back unless <paramref name="keepUnlocked"/>.</summary>
    internal static void ToFirst(bool keepUnlocked)
    {
        HeldBack = false;
        var view = ThirdPersonCameraCollision.instance;
        if (view != null)
            PullIn(view);
        if (!keepUnlocked)
            Relock();
    }

    /// <summary>Per frame while a mod asks for third person or allows scroll zoom.</summary>
    internal static void Enforce(bool wantThird, float distance)
    {
        var view = ThirdPersonCameraCollision.instance;
        if (view == null)
            return;
        if (!Allowed(view))
        {
            // The game needs first person here: let it have it, and keep the request for later.
            if (wantThird && !HeldBack)
            {
                HeldBack = true;
                PullIn(view);
            }
            return;
        }
        if (view.disableThirdPerson || view.firstpersonOnly)
            Unlock(view);
        if (HeldBack)
        {
            HeldBack = false;
            if (wantThird)
                GoOut(view, distance);
        }
    }

    /// <summary>Puts the game's locks back on the camera they were lifted from.</summary>
    internal static void Relock()
    {
        var view = _unlocked;
        _unlocked = null;
        if (view == null)
            return;
        view.disableThirdPerson = _hadDisableThirdPerson;
        view.firstpersonOnly = _hadFirstPersonOnly || Forced();
        if (view.firstpersonOnly || view.disableThirdPerson)
            PullIn(view);
    }

    /// <summary>The level is gone: forget its camera and triggers without touching them.</summary>
    internal static void Forget()
    {
        _unlocked = null;
        HeldBack = false;
        Forcing.Clear();
    }

    internal static void TriggerChanged(PlayerCameraTrigger trigger, bool forcesFirstPerson)
    {
        Forcing.Remove(trigger);
        if (forcesFirstPerson)
            Forcing.Add(trigger);
    }

    /// <summary>The lock values the game had before they were lifted, for writing into a save.</summary>
    internal static bool GameLocks(ThirdPersonCameraCollision view, out bool disableThirdPerson, out bool firstPersonOnly)
    {
        disableThirdPerson = view.disableThirdPerson;
        firstPersonOnly = view.firstpersonOnly;
        if (_unlocked != view)
            return false;
        disableThirdPerson = _hadDisableThirdPerson;
        firstPersonOnly = _hadFirstPersonOnly;
        return true;
    }

    internal static void ShowBody()
    {
        var view = ThirdPersonCameraCollision.instance;
        if (view != null && view.firstperson)
            view.ShowPlayerModel();
    }

    internal static void HideBody()
    {
        var view = ThirdPersonCameraCollision.instance;
        if (view != null && view.firstperson)
            view.HidePlayerModel();
    }

    private static void Unlock(ThirdPersonCameraCollision view)
    {
        if (_unlocked != view)
        {
            _unlocked = view;
            _hadDisableThirdPerson = view.disableThirdPerson;
            _hadFirstPersonOnly = view.firstpersonOnly;
        }
        view.disableThirdPerson = false;
        view.firstpersonOnly = false;
    }

    // Back to the head. The distance matters as much as the mode: near a wall the game shows first person
    // while the distance is still set, and would go back out once there is room.
    private static void PullIn(ThirdPersonCameraCollision view)
    {
        view.currentDistance = 0f;
        if (!view.firstperson)
            view.FirstPerson();
    }

    private static bool Forced()
    {
        for (int i = Forcing.Count - 1; i >= 0; i--)
        {
            if (Forcing[i] == null)
                Forcing.RemoveAt(i);
        }
        return Forcing.Count > 0;
    }
}
