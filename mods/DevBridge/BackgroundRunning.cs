using HarmonyLib;
using ObenLoader;
using ObenLoader.Runtime;
using UnityEngine;

namespace DevBridge;

/// <summary>
/// Keeps the game's loop running while its window is in the background, so tools can talk to it
/// and two copies can run side by side. The game applies its own "Run In Background" option when
/// the options menu starts (every scene) and when the player flips it, so it's set again after both.
/// </summary>
[HarmonyPatch(typeof(OptionsMenu))]
internal static class BackgroundRunning
{
    private static bool _enabled;

    public static void Init(ObenMod bridge, bool enabled)
    {
        _enabled = enabled;
        if (!enabled)
            return;
        bool before = Application.runInBackground;
        Application.runInBackground = true;
        bridge.Track(Disposable.Create(() =>
        {
            _enabled = false;
            Application.runInBackground = before;
        }));
    }

    [HarmonyPostfix]
    [HarmonyPatch(nameof(OptionsMenu.StartInit))]
    private static void AfterStartInit() => Reassert();

    [HarmonyPostfix]
    [HarmonyPatch(nameof(OptionsMenu.SetRunInBackground))]
    private static void AfterToggle() => Reassert();

    private static void Reassert()
    {
        if (_enabled)
            Application.runInBackground = true;
    }
}
