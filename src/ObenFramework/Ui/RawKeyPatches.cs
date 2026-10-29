using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using ObenFramework.Patching;
using UnityEngine;

namespace ObenFramework.Ui;

/// <summary>
/// The few keys the game reads straight from Unity instead of through its <c>InputManager</c>: F1 (console),
/// F11 and F12 (screenshots), 1 to 4 (equipment) and the interact list's X and C. These methods' calls to
/// <c>Input.GetKeyDown/GetKeyUp/GetKey(KeyCode)</c> go through <see cref="RawKeys"/>, so <see cref="InputCapture"/>
/// can hold them back for the game only; a mod reading the same key through Unity still sees it. Its own patch
/// area, so a game update that breaks it leaves the other captures working.
/// </summary>
[PatchArea("RawKeys")]
[HarmonyPatch]
internal static class RawKeyPatches
{
    private static readonly Dictionary<MethodInfo, MethodInfo> Replacements = new Dictionary<MethodInfo, MethodInfo>
    {
        [AccessTools.Method(typeof(Input), nameof(Input.GetKeyDown), new[] { typeof(KeyCode) })] = AccessTools.Method(typeof(RawKeys), nameof(RawKeys.Down)),
        [AccessTools.Method(typeof(Input), nameof(Input.GetKeyUp), new[] { typeof(KeyCode) })] = AccessTools.Method(typeof(RawKeys), nameof(RawKeys.Up)),
        [AccessTools.Method(typeof(Input), nameof(Input.GetKey), new[] { typeof(KeyCode) })] = AccessTools.Method(typeof(RawKeys), nameof(RawKeys.Held)),
    };

    [HarmonyTargetMethods]
    private static IEnumerable<MethodBase> Targets()
    {
        yield return AccessTools.Method(typeof(GameController), "Update");
        yield return AccessTools.Method(typeof(DeveloperConsole), "ShouldToggleDevConsole");
        yield return AccessTools.Method(typeof(InteractableList), "HasVerticalInput");
        yield return AccessTools.Method(typeof(InteractableList), "ShouldCumulateCurrentValues");
        yield return AccessTools.Method(typeof(InteractableList), "HandleVerticalInput");
    }

    [HarmonyTranspiler]
    private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> code, MethodBase original)
    {
        int replaced = 0;
        foreach (var instruction in code)
        {
            if (instruction.operand is MethodInfo called && Replacements.TryGetValue(called, out var ours))
            {
                instruction.operand = ours;
                replaced++;
            }
            yield return instruction;
        }
        if (replaced == 0)
            FrameworkMod.Instance?.Log.Warn($"{original.DeclaringType?.Name}.{original.Name} no longer reads keys from Unity; its keys can't be captured");
    }
}

/// <summary>What the game's direct key reads call instead of Unity's, through <see cref="RawKeyPatches"/>.</summary>
public static class RawKeys
{
    public static bool Down(KeyCode key) => Input.GetKeyDown(key) && !InputCapture.HoldsRaw(key);

    public static bool Up(KeyCode key) => Input.GetKeyUp(key) && !InputCapture.HoldsRaw(key);

    public static bool Held(KeyCode key) => Input.GetKey(key) && !InputCapture.HoldsRaw(key);
}
