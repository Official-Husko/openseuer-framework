using System;
using System.Collections.Generic;
using ObenFramework.Events;
using ObenLoader;
using ObenLoader.Runtime;
using TMPro;
using UnityEngine;

namespace ObenUIKit.Menu;

/// <summary>A mod's button in the pause menu, from <see cref="PauseMenuButtons.Add"/>.</summary>
public sealed class PauseMenuButton
{
    private string _label;
    private bool _interactable = true;

    internal PauseMenuButton(string ownerId, string id, string label, Action onClick, int order, int serial)
    {
        OwnerId = ownerId;
        Id = id;
        _label = label;
        OnClick = onClick;
        Order = order;
        Serial = serial;
    }

    public string OwnerId { get; }

    public string Id { get; }

    public int Order { get; }

    internal int Serial { get; }

    internal Action OnClick { get; }

    internal GameObject Copy { get; set; }

    public string Label
    {
        get => _label;
        set
        {
            _label = value ?? "";
            PauseMenuButtons.Paint(this);
        }
    }

    /// <summary>Greyed out the game's way while false.</summary>
    public bool Interactable
    {
        get => _interactable;
        set
        {
            _interactable = value;
            PauseMenuButtons.Paint(this);
        }
    }

    /// <summary>The button is in the pause menu right now (only in a level).</summary>
    public bool IsShown => Copy != null;

    public void Remove() => PauseMenuButtons.Remove(this);
}

/// <summary>
/// Buttons mods add to the pause menu: each a copy of the menu's own Resume button (like MODS), placed above MODS,
/// sorted by order and then by when they were added. The pause menu's keyboard and pad handling picks them up by
/// itself. They are made in every level and go away with their mod.
/// </summary>
public static class PauseMenuButtons
{
    private static readonly List<PauseMenuButton> Registered = new List<PauseMenuButton>();
    private static int _serial;
    private static ObenMod _kit;

    public static IReadOnlyList<PauseMenuButton> All => Registered;

    /// <summary>
    /// Adds a button labelled <paramref name="label"/> that runs <paramref name="onClick"/> (errors are logged). The
    /// same <paramref name="id"/> again from the same mod replaces the old button. Lower <paramref name="order"/> goes higher.
    /// </summary>
    public static PauseMenuButton Add(ObenMod owner, string id, string label, Action onClick, int order = 100)
    {
        if (owner == null)
            throw new ArgumentNullException(nameof(owner));
        if (string.IsNullOrEmpty(id))
            throw new ArgumentException("A pause menu button needs an id.", nameof(id));
        Find(owner.Info.Id, id)?.Remove();
        var button = new PauseMenuButton(owner.Info.Id, id, label ?? "", onClick, order, _serial++);
        Registered.Add(button);
        owner.Track(Disposable.Create(button.Remove));
        Attach(button);
        return button;
    }

    public static PauseMenuButton Find(string ownerId, string id)
    {
        foreach (var button in Registered)
        {
            if (button.OwnerId == ownerId && button.Id == id)
                return button;
        }
        return null;
    }

    internal static void Init(ObenMod kit)
    {
        _kit = kit;
        GameEvents.WhenWorldReady(kit, _ => AttachAll());
        kit.Track(Disposable.Create(() =>
        {
            foreach (var button in Registered)
                Detach(button);
            Registered.Clear();
            _kit = null;
        }));
    }

    internal static void Remove(PauseMenuButton button)
    {
        if (!Registered.Remove(button))
            return;
        Detach(button);
    }

    internal static void Paint(PauseMenuButton button)
    {
        if (button.Copy == null)
            return;
        var text = button.Copy.GetComponentInChildren<TMP_Text>(true);
        if (text != null)
            text.text = button.Label;
        PauseMenuEntry.SetInteractable(button.Copy, button.Interactable);
    }

    private static void AttachAll()
    {
        var sorted = new List<PauseMenuButton>(Registered);
        sorted.Sort((a, b) => a.Order != b.Order ? a.Order.CompareTo(b.Order) : a.Serial.CompareTo(b.Serial));
        foreach (var button in sorted)
            Attach(button);
    }

    // Made inside the level, so it dies with it; placed above MODS (or Quit to Menu while MODS isn't there yet).
    private static void Attach(PauseMenuButton button)
    {
        var pause = PauseMenu.instance;
        if (_kit == null || pause == null || pause.resumeMenuButton == null)
            return;
        if (button.Copy != null && button.Copy.scene == pause.gameObject.scene)
            return;
        Detach(button);
        var copy = PauseMenuEntry.CopyResume(pause, "Pause button " + button.OwnerId + "." + button.Id, () => Run(button));
        var parent = copy.transform.parent;
        var below = parent.Find(PauseMenuEntry.ObjectName) ?? parent.Find("Quit to Menu");
        int index = below != null ? below.GetSiblingIndex() : parent.childCount - 1;
        // Keep the order among mod buttons already there.
        for (int i = 0; i < Registered.Count; i++)
        {
            var other = Registered[i];
            if (other == button || other.Copy == null || other.Copy.transform.parent != parent)
                continue;
            bool after = other.Order < button.Order || other.Order == button.Order && other.Serial < button.Serial;
            if (!after && other.Copy.transform.GetSiblingIndex() < index)
                index = other.Copy.transform.GetSiblingIndex();
        }
        copy.transform.SetSiblingIndex(index);
        button.Copy = copy;
        Paint(button);
    }

    private static void Run(PauseMenuButton button)
    {
        try { button.OnClick?.Invoke(); }
        catch (Exception e) { _kit?.Log.Error($"The pause menu button '{button.Label}' of {button.OwnerId} failed", e); }
    }

    private static void Detach(PauseMenuButton button)
    {
        if (button.Copy == null)
            return;
        PauseMenuEntry.Discard(button.Copy);
        button.Copy = null;
    }
}
