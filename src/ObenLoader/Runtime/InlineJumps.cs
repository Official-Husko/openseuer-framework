using System;
using System.Reflection;
using HarmonyLib;
using ObenLoader.Logging;

namespace ObenLoader.Runtime;

/// <summary>
/// Makes Harmony write its jump entirely inside the patched method, never through a separate
/// memory cell.
/// </summary>
/// <remarks>
/// A patched method starts with a jump to Harmony's replacement. When the replacement is more than
/// 2 GB away, which happens after a few hot reloads once Mono puts new code further out, MonoMod
/// (bundled in 0Harmony) jumps through an 8-byte cell on a page it allocates in free memory near the
/// method. Under Proton those pages don't stay ours: seconds later the range shows up inside another
/// allocator's 512 MB reservation, and the method faults on its first instruction (a
/// NullReferenceException at offset 0, or a hard crash when the game calls it from a delegate).
/// Refusing that one allocation makes MonoMod use its 14-byte absolute jump, which keeps the target
/// address in the method's own code. Executable allocations (MonoMod's alt entry stubs) are left alone.
/// </remarks>
internal static class InlineJumps
{
    private const string AllocatorType = "MonoMod.Core.Platforms.Memory.PagedMemoryAllocator";
    private const string HarmonyId = "ObenLoader.InlineJumps";

    private static PropertyInfo _requestBase;
    private static PropertyInfo _executable;

    public static void Use(ModLogger log)
    {
        try
        {
            var type = typeof(Harmony).Assembly.GetType(AllocatorType);
            var allocate = type == null ? null : AccessTools.Method(type, "TryAllocateInRange");
            var requestType = allocate?.GetParameters()[0].ParameterType;
            _requestBase = requestType == null ? null : AccessTools.Property(requestType, "Base");
            _executable = _requestBase == null ? null : AccessTools.Property(_requestBase.PropertyType, "Executable");
            if (_executable == null)
            {
                log.Warn("Couldn't find MonoMod's jump cell allocation; hot reload may break patched methods.");
                return;
            }
            new Harmony(HarmonyId).Patch(allocate, prefix: new HarmonyMethod(typeof(InlineJumps), nameof(RefuseJumpCells)));
        }
        catch (Exception e)
        {
            log.Error("Couldn't keep Harmony's jumps inline", e);
        }
    }

    // Jump cells are the only non-executable positioned allocations MonoMod makes.
    private static bool RefuseJumpCells(object request, ref bool __result)
    {
        if ((bool)_executable.GetValue(_requestBase.GetValue(request)))
            return true;
        __result = false;
        return false;
    }
}
