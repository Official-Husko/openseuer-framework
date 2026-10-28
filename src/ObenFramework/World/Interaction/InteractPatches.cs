using System;
using HarmonyLib;
using ObenFramework.World.Addressing;
using UnityEngine;

namespace ObenFramework.World;

/// <summary>
/// The one place the local player's interact key ends up: <c>InteractObjects.UseInteractable</c>. Besides
/// raising <see cref="Interactables.Interacted"/> it marks everything that happens during the call as
/// the local player's doing (<see cref="ChangeSource.LocalPlayer"/>).
/// </summary>
[HarmonyPatch(typeof(InteractObjects), nameof(InteractObjects.UseInteractable))]
internal static class InteractPatches
{
    internal struct Use
    {
        public bool Scoped;
        public Component Target;
        public ObjectAddress Address;
    }

    [HarmonyPrefix]
    private static void Before(InteractObjects __instance, out Use __state)
    {
        __state = default;
        try
        {
            WorldChanges.Adjust(ChangeSource.LocalPlayer, 1);
            __state.Scoped = true;

            if (!Interactables.Interacted.HasSubscribers)
                return;

            // The same order the game uses to pick what to interact with.
            Component target = __instance.savedForceInteractable;
            if (target == null)
                target = __instance.interactable as Component;
            if (target == null)
                target = __instance.savedInteractable;
            if (target == null)
                return;

            __state.Target = target;
            __state.Address = ObjectAddress.Of(target);
        }
        catch (Exception e)
        {
            WorldLog.Failed("Interact hook (before)", e);
        }
    }

    // A finalizer, so the player scope is always closed even if the interaction throws.
    [HarmonyFinalizer]
    private static Exception After(Exception __exception, Use __state)
    {
        try
        {
            if (__state.Scoped)
                WorldChanges.Adjust(ChangeSource.LocalPlayer, -1);
            if (__exception == null && !__state.Address.IsEmpty && WorldChanges.Active)
                Interactables.Interacted.Raise(new Interaction(__state.Target, __state.Address, ChangeSource.LocalPlayer));
        }
        catch (Exception e)
        {
            WorldLog.Failed("Interact hook (after)", e);
        }
        return __exception;
    }
}
