using HarmonyLib;

namespace ObenUIKit.Runtime;

/// <summary>
/// Keeps the pause menu quiet while a kit window is open over it: its keyboard and controller
/// handler runs every frame without checking for other UI (Enter would click the button behind
/// the window), and Esc would open or close it.
/// </summary>
[HarmonyPatch]
internal static class InputGates
{
    private static bool WindowOpen => KitRuntime.Current != null && KitRuntime.Current.Kit.Windows.Any;

    [HarmonyPrefix]
    [HarmonyPatch(typeof(PauseMenu), nameof(PauseMenu.Pause))]
    private static bool BeforePause() => !WindowOpen;

    [HarmonyPrefix]
    [HarmonyPatch(typeof(PauseNavigationHandler), "Update")]
    private static bool BeforePauseNavigation() => !WindowOpen;
}
