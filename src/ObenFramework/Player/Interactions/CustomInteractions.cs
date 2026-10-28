// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
using System;
using System.Collections.Generic;
using ObenFramework.Events;
using ObenFramework.Saves;
using ObenLoader;
using ObenLoader.Runtime;
using UnityEngine;

namespace ObenFramework.Player;

/// <summary>
/// Makes any object usable: the crosshair shows a title and a verb and the interact key runs the mod's
/// code, the same as for a door or a radio. The object needs a collider of its own (the game's ray hits
/// colliders) and must not already be one of the game's interactables. To hear about uses of the game's
/// own interactables see <c>ObenFramework.World.Interactables.Interacted</c>.
/// </summary>
public static class CustomInteractions
{
    // Layers the game's interact ray accepts, in the order we'd move an object to.
    private static readonly int[] UsableLayers = { 17, 23, 29, 20, 15 };

    private static readonly List<CustomInteraction> Live = new List<CustomInteraction>();

    private static readonly OwnerWatch Owners = new OwnerWatch(RemoveOwner);

    /// <summary>
    /// Adds an interaction to <paramref name="target"/>. Returns null when it can't now (main menu, a
    /// save running, no usable layer); throws for a target without a collider or one the game already uses.
    /// A second call for the same object from the same mod replaces the first.
    /// </summary>
    /// <param name="verb">The action in brackets after the title, for example "Listen".</param>
    public static CustomInteraction Add(ObenMod owner, GameObject target, string title, string verb, Action onUse)
    {
        if (target == null)
            throw new ArgumentNullException(nameof(target));
        if (onUse == null)
            throw new ArgumentNullException(nameof(onUse));
        if (target.GetComponent<Collider>() == null)
            throw new ArgumentException($"{target.name} has no collider, so the interact ray can't hit it.", nameof(target));
        if (HasGameInteractable(target))
            throw new ArgumentException($"{target.name} is already one of the game's interactables.", nameof(target));

        var interact = InteractObjects.instance;
        if (interact == null || SaveGate.IsBusy)
            return null;

        string ownerId = Owners.Watch(owner);
        var existing = Find(target);
        if (existing != null)
        {
            if (existing.OwnerId != ownerId)
                throw new InvalidOperationException($"{target.name} already has an interaction from {existing.OwnerId}.");
            Remove(existing);
        }

        int layer = ChooseLayer(interact, target.layer);
        if (layer < 0)
        {
            GameEvents.Log.Warn("No layer the interact ray accepts was found; the interaction was not added.");
            return null;
        }

        int original = target.layer;
        target.layer = layer;
        var component = target.AddComponent<ModInteractable>();
        component.title = title ?? "";
        component.useMessage = verb ?? "";
        var handle = new CustomInteraction(ownerId, target, component, original, onUse);
        component.Handle = handle;
        Live.Add(handle);
        return handle;
    }

    /// <summary>The interaction on an object, or null.</summary>
    public static CustomInteraction Find(GameObject target)
    {
        if (target == null)
            return null;
        Live.RemoveAll(i => !i.IsAlive);
        foreach (var interaction in Live)
        {
            if (interaction.Target == target)
                return interaction;
        }
        return null;
    }

    /// <summary>Every interaction mods have added that still exists.</summary>
    public static IReadOnlyList<CustomInteraction> All
    {
        get
        {
            Live.RemoveAll(i => !i.IsAlive);
            return Live.ToArray();
        }
    }

    /// <summary>Takes an interaction away. False when it was gone already.</summary>
    public static bool Remove(CustomInteraction interaction)
    {
        if (interaction == null || !Live.Remove(interaction))
            return false;
        interaction.Destroy();
        return true;
    }

    internal static void Init(ObenMod framework) => framework.Track(Disposable.Create(() =>
    {
        foreach (var interaction in Live)
            interaction.Destroy();
        Live.Clear();
        Owners.Clear();
    }));

    private static void RemoveOwner(string ownerId)
    {
        foreach (var interaction in Live.ToArray())
        {
            if (interaction.OwnerId == ownerId)
                Remove(interaction);
        }
    }

    private static int ChooseLayer(InteractObjects interact, int current)
    {
        int mask = interact.MaskList.value;
        if (interact.IsUsableLayer(current) && (mask & (1 << current)) != 0)
            return current;
        foreach (int layer in UsableLayers)
        {
            if (interact.IsUsableLayer(layer) && (mask & (1 << layer)) != 0)
                return layer;
        }
        return -1;
    }

    private static bool HasGameInteractable(GameObject target)
    {
        foreach (var behaviour in target.GetComponents<MonoBehaviour>())
        {
            if (behaviour is IInteractable && !(behaviour is ModInteractable))
                return true;
        }
        return false;
    }
}
