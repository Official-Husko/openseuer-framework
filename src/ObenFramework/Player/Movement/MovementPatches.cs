using HarmonyLib;

namespace ObenFramework.Player;

/// <summary>Applies the movement multipliers to each new controller as soon as the game builds it.</summary>
[HarmonyPatch(typeof(OpenSewerCharacterController), nameof(OpenSewerCharacterController.Awake))]
internal static class MovementPatches
{
    [HarmonyPostfix]
    private static void AfterAwake(OpenSewerCharacterController __instance) => MovementSpeeds.Apply(__instance);
}
