using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using ObenFramework.Features;

namespace ObenPatches.Fixes;

/// <summary>
/// An apartment's cleaning job ends at midnight once the clock's day reaches the job's last day. The clock raises
/// DayChanged with the new day before its own currentTimeAndDay.currentDay moves on, and
/// TenementController.OnCleaningTimerDayChanged compares that old field instead of the day it is handed, so every
/// job took a day longer (the story's 3-day cleaning of the shop took 4). The fix compares the new day.
/// </summary>
internal sealed class CleaningDayLate : ModFeature
{
    public const string Key = "CleaningDayLate";

    public override string Id => Key;
    public override string Name => "Cleaning jobs end on time";
    public override string Description => "An apartment's cleaning job is done after the days it takes, not a day later.";
}

[HarmonyPatchCategory(CleaningDayLate.Key)]
[HarmonyPatch(typeof(TenementController), "OnCleaningTimerDayChanged")]
internal static class CleaningDayLatePatch
{
    private static readonly FieldInfo Instance = AccessTools.Field(typeof(TimeOfDayAzure), nameof(TimeOfDayAzure.instance));
    private static readonly FieldInfo Current = AccessTools.Field(typeof(TimeOfDayAzure), nameof(TimeOfDayAzure.currentTimeAndDay));
    private static readonly FieldInfo CurrentDay = AccessTools.Field(typeof(TimeAndDay), nameof(TimeAndDay.currentDay));

    // TimeOfDayAzure.instance.currentTimeAndDay.currentDay becomes the handler's day argument (arg 1; 0 is this).
    private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> code)
    {
        var list = code.ToList();
        int changed = 0;
        for (int i = 0; i + 2 < list.Count; i++)
        {
            if (!list[i].LoadsField(Instance) || !list[i + 1].LoadsField(Current) || !list[i + 2].LoadsField(CurrentDay))
                continue;
            // Kept in place as no-ops so any jump onto them still lands.
            list[i].opcode = OpCodes.Ldarg_1;
            list[i].operand = null;
            list[i + 1].opcode = OpCodes.Nop;
            list[i + 1].operand = null;
            list[i + 2].opcode = OpCodes.Nop;
            list[i + 2].operand = null;
            changed++;
        }
        if (changed == 0)
            PatchLog.Log.Warn("TenementController.OnCleaningTimerDayChanged: no read of the clock's day found, leaving it as it is");
        return list;
    }
}
