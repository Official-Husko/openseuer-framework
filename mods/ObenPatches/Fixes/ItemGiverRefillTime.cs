using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using ObenFramework.Features;

namespace ObenPatches.Fixes;

/// <summary>
/// An item giver that refills over time (a cup dispenser) subscribes its timer to the game clock from a coroutine
/// that Start begins. Loading a level from a save calls Start a second time (InteractableItemGiver.OnLoadingGame),
/// and Start's guard flag is never set, so the timer is subscribed twice: the giver counts down twice per tick and
/// refills twice as fast. OnDestroy takes only one copy off, so leaving the level leaves the other behind, and the
/// next tick logs "InteractableItemGiver is null!". The fix makes the subscription happen at most once.
/// </summary>
internal sealed class ItemGiverRefillTime : ModFeature
{
    public const string Key = "ItemGiverRefillTime";

    public override string Id => Key;
    public override string Name => "Item givers refill on time";
    public override string Description => "Dispensers and other refilling item givers in a loaded level no longer refill twice as fast, and leaving their level no longer logs an error.";
}

[HarmonyPatchCategory(ItemGiverRefillTime.Key)]
[HarmonyPatch]
internal static class ItemGiverRefillTimePatch
{
    private static readonly MethodInfo AddSecondsPassed = AccessTools.Method(typeof(TimeOfDayAzure), "add_SecondsPassed");
    private static readonly MethodInfo SubscribeOnce = AccessTools.Method(typeof(ItemGiverRefillTimePatch), nameof(Subscribe));

    private static MethodBase TargetMethod() =>
        AccessTools.EnumeratorMoveNext(AccessTools.Method(typeof(InteractableItemGiver), "InitCoroutine"));

    private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> code)
    {
        var list = code.ToList();
        int changed = 0;
        foreach (var instruction in list)
        {
            if (!instruction.Calls(AddSecondsPassed))
                continue;
            instruction.opcode = OpCodes.Call;
            instruction.operand = SubscribeOnce;
            changed++;
        }
        if (changed == 0)
            PatchLog.Log.Warn("InteractableItemGiver.InitCoroutine: no SecondsPassed subscription found, leaving it as it is");
        return list;
    }

    // The same giver's handler compares equal, so taking it off first leaves one however often the setup runs.
    private static void Subscribe(TimeOfDayAzure.OnSecondsPassed handler)
    {
        TimeOfDayAzure.SecondsPassed -= handler;
        TimeOfDayAzure.SecondsPassed += handler;
    }
}
