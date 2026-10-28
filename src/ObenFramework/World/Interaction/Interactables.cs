using System.Collections.Generic;
using ObenFramework.World.Addressing;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ObenFramework.World;

/// <summary>An interactable and what a mod usually wants to know about it.</summary>
public readonly struct InteractableInfo
{
    public InteractableInfo(Component component, ObjectAddress address, string title, bool disabled)
    {
        Component = component;
        Address = address;
        Title = title;
        Disabled = disabled;
    }

    /// <summary>The component that implements <c>IInteractable</c> (a Door, a Button, a Storage...).</summary>
    public Component Component { get; }

    /// <summary>A stable address, see <see cref="ObjectAddress"/>.</summary>
    public ObjectAddress Address { get; }

    /// <summary>The name the game shows when looking at it.</summary>
    public string Title { get; }

    public bool Disabled { get; }

    public string TypeName => Component != null ? Component.GetType().Name : "";
}

/// <summary>A use of an interactable, raised by <see cref="Interactables.Interacted"/>.</summary>
public readonly struct Interaction
{
    public Interaction(Component target, ObjectAddress address, ChangeSource source)
    {
        Target = target;
        Address = address;
        Source = source;
    }

    /// <summary>What was used. Unity-null if using it destroyed it (picking something up); use <see cref="Address"/> then.</summary>
    public Component Target { get; }

    /// <summary>The target's address, taken before the use.</summary>
    public ObjectAddress Address { get; }

    public ChangeSource Source { get; }

    /// <summary>The local player pressed the interact key on it. True for everything the game raises.</summary>
    public bool ByLocalPlayer => Source == ChangeSource.LocalPlayer;
}

/// <summary>
/// Interactables: what the player is looking at, a way to name any of them that survives a restart, and
/// an event for every use. In the game every usable thing (doors, switches, storage, pickups, machines)
/// implements <c>IInteractable</c>; the player's single raycast picks one and the interact key calls it.
/// </summary>
public static class Interactables
{
    /// <summary>
    /// The local player used something: raised after the game's own interact call for it returned, with the
    /// target and its address. It is not raised for things the game or a mod interacts with by code.
    /// </summary>
    public static readonly WorldEvent<Interaction> Interacted = new WorldEvent<Interaction>(nameof(Interacted));

    /// <summary>
    /// The interactable under the crosshair, or null. The game updates this every frame in first person;
    /// reading it is a field read. In third person it is whatever the interaction list has selected.
    /// </summary>
    public static Component Target
    {
        get
        {
            var instance = InteractObjects.instance;
            return instance != null ? instance.interactable as Component : null;
        }
    }

    /// <summary>Details of <see cref="Target"/>, or null when nothing is targeted.</summary>
    public static InteractableInfo? TargetInfo
    {
        get
        {
            var target = Target;
            return target != null ? Describe(target) : (InteractableInfo?)null;
        }
    }

    public static InteractableInfo Describe(Component component)
    {
        bool disabled;
        string title;
        if (component is Interactable interactable)
        {
            title = string.IsNullOrEmpty(interactable.title) ? component.name : interactable.title;
            disabled = interactable.IsInteractDisabled();
        }
        else
        {
            title = component.name;
            disabled = component is IInteractable raw && raw.IsInteractDisabled();
        }
        return new InteractableInfo(component, ObjectAddress.Of(component), title, disabled);
    }

    /// <summary>
    /// The component at an address or id (see <see cref="ObjectResolver.Find{T}"/> for the accepted texts), or
    /// null. Works across sessions: save the address, resolve it after the next load.
    /// </summary>
    public static Component Find(string idOrAddress) => ObjectResolver.Find<Component>(idOrAddress);

    public static Component Find(ObjectAddress address) => address.Resolve();

    /// <summary>
    /// Every interactable of the loaded scenes: all <see cref="Interactable"/> subclasses plus the few things
    /// that implement <c>IInteractable</c> alone (resource nodes). A scan, so not for per-frame use.
    /// </summary>
    public static List<Component> All(bool includeInactive = false)
    {
        var mode = includeInactive ? FindObjectsInactive.Include : FindObjectsInactive.Exclude;
        var result = new List<Component>();
        foreach (var item in Object.FindObjectsByType<Interactable>(mode, FindObjectsSortMode.None))
            result.Add(item);
        foreach (var item in Object.FindObjectsByType<WorkableResourceSource>(mode, FindObjectsSortMode.None))
            result.Add(item);
        return result;
    }
}
