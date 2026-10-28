using System;
using UnityEngine;

namespace ObenFramework.Player;

/// <summary>
/// Something the player can use on an object a mod chose, from <see cref="CustomInteractions.Add"/>. The
/// crosshair shows its title and verb like any of the game's interactables. Dispose it to take it away;
/// it also goes when the owner unloads, and with the object.
/// </summary>
public sealed class CustomInteraction : IDisposable
{
    private readonly ModInteractable _component;
    private readonly int _originalLayer;

    internal CustomInteraction(string ownerId, GameObject target, ModInteractable component, int originalLayer, Action onUse)
    {
        OwnerId = ownerId;
        Target = target;
        _component = component;
        _originalLayer = originalLayer;
        OnUse = onUse;
    }

    public string OwnerId { get; }
    public GameObject Target { get; }

    /// <summary>The object and the interaction still exist.</summary>
    public bool IsAlive => _component != null;

    /// <summary>The name the prompt shows ("Old radio").</summary>
    public string Title
    {
        get => _component != null ? _component.title : null;
        set
        {
            if (_component != null)
                _component.title = value ?? "";
        }
    }

    /// <summary>What pressing the key does, shown in brackets after the title ("Listen").</summary>
    public string Verb
    {
        get => _component != null ? _component.useMessage : null;
        set
        {
            if (_component != null)
                _component.useMessage = value ?? "";
        }
    }

    /// <summary>
    /// Asked every frame the crosshair is on the object: return a short reason ("Needs batteries") to show
    /// in red and refuse the use, or null when it can be used. Keep it cheap.
    /// </summary>
    public Func<string> Reason { get; set; }

    /// <summary>Off hides the prompt and ignores the key, like a switched-off game interactable.</summary>
    public bool Enabled
    {
        get => _component != null && !_component.IsInteractDisabled();
        set
        {
            if (_component != null)
                _component.SetInteractDisabled(!value);
        }
    }

    internal Action OnUse { get; }

    public void Dispose() => CustomInteractions.Remove(this);

    /// <summary>Removes the component and puts the object's layer back.</summary>
    internal void Destroy()
    {
        if (_component == null)
            return;
        _component.Handle = null;
        _component.DisableInteract();
        if (Target != null && Target.layer != _originalLayer)
            Target.layer = _originalLayer;
        UnityEngine.Object.Destroy(_component);
    }
}
