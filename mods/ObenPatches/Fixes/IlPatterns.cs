using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using UnityEngine;

namespace ObenPatches.Fixes;

/// <summary>Small IL rewrites shared by the transpiler fixes. Each one matches by meaning and says so in the log when it finds nothing.</summary>
internal static class IlPatterns
{
    private static readonly MethodInfo RandomRangeInt =
        AccessTools.Method(typeof(Random), nameof(Random.Range), new[] { typeof(int), typeof(int) });

    private static readonly MethodInfo RandomValue = AccessTools.PropertyGetter(typeof(Random), nameof(Random.value));

    /// <summary>
    /// Finds every <c>Random.Range(a, b - 1)</c> and turns it into <c>Random.Range(a, b)</c>. The int
    /// overload leaves out its upper bound already, so the "- 1" the game adds drops the last entry.
    /// The "ldc.i4.1" becomes "ldc.i4.0" rather than being removed so jump targets stay valid.
    /// </summary>
    public static List<CodeInstruction> IncludeLastIndex(IEnumerable<CodeInstruction> code, string where)
    {
        var list = code.ToList();
        int changed = 0;
        for (int i = 2; i < list.Count; i++)
        {
            if (!list[i].Calls(RandomRangeInt) || list[i - 1].opcode != OpCodes.Sub || list[i - 2].opcode != OpCodes.Ldc_I4_1)
                continue;
            list[i - 2].opcode = OpCodes.Ldc_I4_0;
            changed++;
        }
        if (changed == 0)
            PatchLog.Log.Warn($"{where}: no Random.Range(.., x - 1) found, leaving it as it is");
        return list;
    }

    /// <summary>
    /// Finds <c>(float)Random.Range(0, 1)</c>, which is always 0 because the upper bound of the int
    /// overload is left out, and turns it into <c>Random.value</c>, a float from 0 to 1.
    /// </summary>
    public static List<CodeInstruction> ChanceFromRandomValue(IEnumerable<CodeInstruction> code, string where)
    {
        var list = code.ToList();
        int changed = 0;
        for (int i = 0; i + 3 < list.Count; i++)
        {
            if (list[i].opcode != OpCodes.Ldc_I4_0 || list[i + 1].opcode != OpCodes.Ldc_I4_1
                || !list[i + 2].Calls(RandomRangeInt) || list[i + 3].opcode != OpCodes.Conv_R4)
                continue;
            // Any of these may be a jump target, so they stay in place and the extra ones become no-ops.
            list[i].opcode = OpCodes.Nop;
            list[i + 1].opcode = OpCodes.Nop;
            list[i + 2].opcode = OpCodes.Call;
            list[i + 2].operand = RandomValue;
            list[i + 3].opcode = OpCodes.Nop;
            changed++;
        }
        if (changed == 0)
            PatchLog.Log.Warn($"{where}: no (float)Random.Range(0, 1) found, leaving it as it is");
        return list;
    }
}
