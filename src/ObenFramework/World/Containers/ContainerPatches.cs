using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;

namespace ObenFramework.World;

/// <summary>
/// The game's container entry points. Opening and closing are <c>EnterExternally</c> and
/// <c>ExitExternally</c> (the interact key ends up in <c>UseStorage</c>, which also checks the game's key
/// lock first). Contents change through <c>Storage</c>'s public add and remove methods, or, while the
/// inventory window is open, through the three private callbacks the window's slots call.
/// </summary>
internal static class ContainerPatches
{
    private static Storage _open;
    private static int _depth;

    /// <summary>What a mutating call looked like before it ran, to tell a real change from a refused one.</summary>
    internal struct Before
    {
        public bool Tracked;
        public int Signature;
    }

    public static void Reset()
    {
        _open = null;
        _depth = 0;
    }

    private static void RaiseOpened(Storage storage)
    {
        if (_open == storage)
            return;
        _open = storage;
        if (ContainerLocks.LockWhileLocalPlayerHasOpen && WorldChanges.Current == ChangeSource.LocalPlayer)
            ContainerLocks.TryLock(Containers.IdOf(storage), ContainerLocks.LocalOwner);
        if (Containers.Opened.HasSubscribers && WorldChanges.Active)
            Containers.Opened.Raise(new ContainerEvent(Containers.Describe(storage), WorldChanges.Current));
    }

    private static void RaiseClosed(Storage storage)
    {
        if (_open != storage)
            return;
        _open = null;
        if (ContainerLocks.LockWhileLocalPlayerHasOpen)
            ContainerLocks.Unlock(Containers.IdOf(storage), ContainerLocks.LocalOwner);
        if (Containers.Closed.HasSubscribers && WorldChanges.Active)
            Containers.Closed.Raise(new ContainerEvent(Containers.Describe(storage), WorldChanges.Current));
    }

    // Cheap fingerprint of the slots: enough to see that a call added or removed something.
    private static int SignatureOf(Storage storage)
    {
        int sum = 17;
        var slots = storage.Slots;
        if (slots == null)
            return sum;
        foreach (var slot in slots)
        {
            var stack = slot != null ? slot.itemStack : null;
            if (stack != null)
                sum = sum * 31 + stack.itemId * 7 + stack.itemAmount;
        }
        return sum;
    }

    [HarmonyPatch]
    private static class OpenClose
    {
        [HarmonyPostfix]
        [HarmonyPatch(typeof(Storage), nameof(Storage.EnterExternally))]
        private static void Entered(Storage __instance)
        {
            try { RaiseOpened(__instance); }
            catch (Exception e) { WorldLog.Failed("Container opened hook", e); }
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(Storage), nameof(Storage.ExitExternally))]
        private static void Exited(Storage __instance)
        {
            try { RaiseClosed(__instance); }
            catch (Exception e) { WorldLog.Failed("Container closed hook", e); }
        }

        // A container that is switched off or destroyed while the player has it open never calls ExitExternally.
        [HarmonyPostfix]
        [HarmonyPatch(typeof(Storage), "OnDisable")]
        private static void Disabled(Storage __instance)
        {
            try { RaiseClosed(__instance); }
            catch (Exception e) { WorldLog.Failed("Container closed hook (disabled)", e); }
        }

        // Another user holds the container: the local player gets the "locked" feedback instead of the window.
        [HarmonyPrefix]
        [HarmonyPatch(typeof(Storage), nameof(Storage.UseStorage))]
        private static bool Using(Storage __instance)
        {
            try
            {
                if (!ContainerLocks.Any || !ContainerLocks.IsHeldByOther(Containers.IdOf(__instance), ContainerLocks.LocalOwner))
                    return true;
                __instance.LockedUse();
                return false;
            }
            catch (Exception e)
            {
                WorldLog.Failed("Container use hook", e);
                return true;
            }
        }
    }

    /// <summary>The public methods that change contents. Nested calls (AddItem inside the window's callbacks) report once.</summary>
    [HarmonyPatch]
    private static class Mutators
    {
        [HarmonyTargetMethods]
        private static IEnumerable<MethodBase> Targets()
        {
            yield return AccessTools.Method(typeof(Storage), nameof(Storage.AddItem), new[] { typeof(Item), typeof(int), typeof(int), typeof(object[]), typeof(int), typeof(bool) });
            yield return AccessTools.Method(typeof(Storage), nameof(Storage.AddItemAndReturnRemaining), new[] { typeof(Item), typeof(int), typeof(int), typeof(object[]) });
            yield return AccessTools.Method(typeof(Storage), nameof(Storage.AddItemToStorage), new[] { typeof(ItemStack), typeof(bool) });
            yield return AccessTools.Method(typeof(Storage), nameof(Storage.RemoveItem), new[] { typeof(int), typeof(int).MakeByRefType(), typeof(int), typeof(bool), typeof(bool), typeof(int) });
            yield return AccessTools.Method(typeof(Storage), nameof(Storage.RemoveItem), new[] { typeof(bool) });
            yield return AccessTools.Method(typeof(Storage), nameof(Storage.TakeAll));
            yield return AccessTools.Method(typeof(Storage), nameof(Storage.MoveToOtherStorage));
            yield return AccessTools.Method(typeof(Storage), nameof(Storage.Empty));
        }

        [HarmonyPrefix]
        private static void Starting(Storage __instance, out Before __state)
        {
            __state = default;
            if (!Containers.Changed.HasSubscribers)
                return;
            _depth++;
            __state.Tracked = true;
            if (_depth == 1)
                __state.Signature = SignatureOf(__instance);
        }

        // A finalizer keeps the depth right even when the game's method throws.
        [HarmonyFinalizer]
        private static Exception Finished(Exception __exception, Storage __instance, Before __state)
        {
            if (!__state.Tracked)
                return __exception;

            _depth--;
            try
            {
                if (_depth == 0 && __exception == null && WorldChanges.Active && SignatureOf(__instance) != __state.Signature)
                    Containers.Changed.Raise(new ContainerEvent(Containers.Describe(__instance), WorldChanges.Current));
            }
            catch (Exception e)
            {
                WorldLog.Failed("Container changed hook", e);
            }
            return __exception;
        }
    }

    /// <summary>
    /// What the open inventory window calls when the player drags, drops or quick-moves a stack. These are the
    /// local player's doing even though no interact scope surrounds them.
    /// </summary>
    [HarmonyPatch]
    private static class WindowCallbacks
    {
        [HarmonyTargetMethods]
        private static IEnumerable<MethodBase> Targets()
        {
            foreach (string name in new[] { "OnDroppedToSlot", "OnTakenFromSlot", "OnReturnToSlot" })
                yield return AccessTools.Method(typeof(Storage), name) ?? throw new MissingMethodException(nameof(Storage), name);
        }

        [HarmonyPostfix]
        private static void Moved(Storage __instance)
        {
            if (_depth > 0 || !Containers.Changed.HasSubscribers || !WorldChanges.Active)
                return;
            try
            {
                var source = WorldChanges.Current == ChangeSource.Game ? ChangeSource.LocalPlayer : WorldChanges.Current;
                Containers.Changed.Raise(new ContainerEvent(Containers.Describe(__instance), source));
            }
            catch (Exception e)
            {
                WorldLog.Failed("Container window hook", e);
            }
        }
    }
}
