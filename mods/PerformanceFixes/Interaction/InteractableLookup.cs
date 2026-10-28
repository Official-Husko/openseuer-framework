using System;
using System.Collections.Generic;
using HarmonyLib;
using ObenFramework.Features;
using UnityEngine;

namespace PerformanceFixes.Interaction;

/// <summary>
/// While the crosshair is on something usable, the game works out what can be used on it every
/// frame by asking each component for its interfaces through reflection and LINQ, which throws
/// away several arrays and closures per frame. This does the same walk over the components with
/// a remembered answer per type and no allocations. It still asks the object each frame, so
/// anything that changes (a door unlocking, a component added) is seen immediately.
/// </summary>
internal sealed class InteractableLookup : ModFeature
{
    public const string Key = "InteractableLookup";

    public override string Id => Key;
    public override string Name => "Faster crosshair interaction lookup";
    public override string Description => "Finds what you can use under the crosshair without reflection and per-frame garbage.";

    protected override void OnDisable() => InteractableLookupPatch.Clear();
}

[HarmonyPatchCategory(InteractableLookup.Key)]
[HarmonyPatch(typeof(InteractObjects), nameof(InteractObjects.GetInteractable))]
internal static class InteractableLookupPatch
{
    private static readonly Dictionary<Type, bool> Implements = new Dictionary<Type, bool>();
    private static readonly List<MonoBehaviour> Behaviours = new List<MonoBehaviour>(16);
    private static bool _busy;

    internal static void Clear()
    {
        Implements.Clear();
        Behaviours.Clear();
        _busy = false;
    }

    private static bool Prefix(InteractObjects __instance, GameObject current, RaycastHit ___Hit)
    {
        // A nested call (an IsInteractDisabled that looks things up itself) gets the original.
        if (_busy)
            return true;

        IInteractable found;
        _busy = true;
        try
        {
            found = FirstEnabled(current);
        }
        finally
        {
            Behaviours.Clear();
            _busy = false;
        }

        __instance.interactable = found;
        if (found == null)
        {
            __instance.HideThings();
        }
        else if (UseText.instance != null)
        {
            UseText.instance.ChangeUseText("(Use)");
            found.UpdateUseMessage();
            if (current.TryGetComponent<IQuickSlotTarget>(out var target))
            {
                target.UpdateQuickSlot(___Hit);
            }
            else if (found is grow || found is GrowItem)
            {
                var growing = current.GetComponentInParent<Growing>();
                if (growing != null)
                    growing.UpdateQuickSlot(___Hit);
            }
        }
        return false;
    }

    // Same order and same laziness as the game's LINQ: components in order, stop at the first
    // interactable that isn't disabled.
    private static IInteractable FirstEnabled(GameObject current)
    {
        current.GetComponents(Behaviours);
        for (int i = 0; i < Behaviours.Count; i++)
        {
            var behaviour = Behaviours[i];
            if (behaviour == null || !IsInteractable(behaviour.GetType()))
                continue;
            var candidate = (IInteractable)(object)behaviour;
            if (!candidate.IsInteractDisabled())
                return candidate;
        }
        return null;
    }

    private static bool IsInteractable(Type type)
    {
        if (!Implements.TryGetValue(type, out bool result))
        {
            result = typeof(IInteractable).IsAssignableFrom(type);
            Implements[type] = result;
        }
        return result;
    }
}
