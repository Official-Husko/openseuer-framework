// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
using HarmonyLib;
using ObenFramework.Patching;
using UnityEngine;

namespace ObenFramework.Player;

/// <summary>The view the camera had before a save was written, put back right after.</summary>
internal struct SavedView
{
    public bool Changed;
    public bool DisableThirdPerson;
    public bool FirstPersonOnly;
    public bool FirstPerson;
    public bool ViewChangeToFirst;
    public float CurrentDistance;
    public float Distance;
}

/// <summary>
/// <see cref="CameraView"/>'s hooks: which camera triggers force first person right now, keeping the body drawn
/// in first person for a mod, and keeping an unlocked third-person camera out of the save. The camera's lock
/// flags, mode and distance are saved with the game, so while a save is written the camera briefly looks the
/// way the game left it (first person, locked), and a save made in third person loads fine without mods.
/// </summary>
[PatchArea("Camera")]
[HarmonyPatch]
internal static class CameraPatches
{
    [HarmonyPostfix]
    [HarmonyPatch(typeof(PlayerCameraTrigger), nameof(PlayerCameraTrigger.FirstPersonOnly))]
    private static void TriggerSet(PlayerCameraTrigger __instance, bool enabled) => CameraLocks.TriggerChanged(__instance, enabled);

    // Per frame; returns at once unless a mod locks the view. The game's own look-at only exists in first person.
    [HarmonyPostfix]
    [HarmonyPatch(typeof(ThirdPersonCameraController), "LateUpdate")]
    private static void AfterCameraUpdate(ThirdPersonCameraController __instance, bool ___rotationDisabled, bool ___firstpersonSoftLock, Transform ___lookAtTarget)
    {
        if (!CameraLook.Any)
            return;
        var view = ThirdPersonCameraCollision.instance;
        bool gameLooks = ___rotationDisabled
            || (___firstpersonSoftLock && ___lookAtTarget != null && view != null && view.firstperson)
            || (Death.instance != null && Death.instance.dead);
        CameraLook.Steer(__instance, gameLooks);
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(ThirdPersonCameraCollision), nameof(ThirdPersonCameraCollision.HidePlayerModel))]
    private static bool HidingBody() => !CameraView.FirstPersonBodyShown;

    [HarmonyPrefix]
    [HarmonyPatch(typeof(ThirdPersonCameraCollision), nameof(ThirdPersonCameraCollision.OnSavingGame))]
    private static void BeforeSave(ThirdPersonCameraCollision __instance, out SavedView __state)
    {
        __state = default;
        bool unlocked = CameraLocks.GameLocks(__instance, out bool disableThirdPerson, out bool firstPersonOnly);
        if (!unlocked && __instance.firstperson)
            return;

        __state = new SavedView
        {
            Changed = true,
            DisableThirdPerson = __instance.disableThirdPerson,
            FirstPersonOnly = __instance.firstpersonOnly,
            FirstPerson = __instance.firstperson,
            ViewChangeToFirst = __instance.onViewChangeTo1st,
            CurrentDistance = __instance.currentDistance,
            Distance = __instance.distance,
        };
        __instance.disableThirdPerson = disableThirdPerson;
        __instance.firstpersonOnly = firstPersonOnly;
        __instance.firstperson = true;
        __instance.onViewChangeTo1st = false;
        __instance.currentDistance = 0f;
        __instance.distance = 0f;
    }

    [HarmonyFinalizer]
    [HarmonyPatch(typeof(ThirdPersonCameraCollision), nameof(ThirdPersonCameraCollision.OnSavingGame))]
    private static void AfterSave(ThirdPersonCameraCollision __instance, SavedView __state)
    {
        if (!__state.Changed)
            return;
        __instance.disableThirdPerson = __state.DisableThirdPerson;
        __instance.firstpersonOnly = __state.FirstPersonOnly;
        __instance.firstperson = __state.FirstPerson;
        __instance.onViewChangeTo1st = __state.ViewChangeToFirst;
        __instance.currentDistance = __state.CurrentDistance;
        __instance.distance = __state.Distance;
    }
}
