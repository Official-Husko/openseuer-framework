// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
using HarmonyLib;
using ObenFramework.Events;

namespace ObenFramework.Player.Survival;

/// <summary>Raises <see cref="SurvivalEvents.PassedOut"/> when the game starts a pass-out.</summary>
[HarmonyPatch]
internal static class PassOutPatches
{
    [HarmonyPrefix]
    [HarmonyPatch(typeof(WaitingController), nameof(WaitingController.PassOut))]
    private static void BeforePassOut(WaitingController __instance, out WaitingController.Type __state) =>
        __state = __instance.waitingType;

    [HarmonyPostfix]
    [HarmonyPatch(typeof(WaitingController), nameof(WaitingController.PassOut))]
    private static void AfterPassOut(WaitingController __instance, WaitingController.Type __state)
    {
        // PassOut does nothing when the player can't start waiting, so only a real change counts.
        if (__state != WaitingController.Type.Passout && __instance.waitingType == WaitingController.Type.Passout)
            SurvivalEvents.PassedOut.Raise(GameEvents.Log);
    }
}
