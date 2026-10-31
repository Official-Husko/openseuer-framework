using System;
using System.Collections.Generic;
using ObenFramework.Patching;
using ObenFramework.Saves;
using ObenFramework.World.Addressing;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ObenFramework.World;

/// <summary>A switch, lever or button changing between pressed and released, as raised by <see cref="Switches.SwitchChanged"/>.</summary>
public readonly struct SwitchChange
{
    public SwitchChange(Button button, bool pressed, ChangeSource source)
    {
        Button = button;
        Pressed = pressed;
        Source = source;
    }

    public Button Button { get; }

    /// <summary>True when it went in, false when it came back out.</summary>
    public bool Pressed { get; }

    /// <summary>
    /// Who did it. A press the player started counts as the player's for a few seconds, which covers the
    /// game's press delays; an automatic release later (a timed button) is the game's.
    /// </summary>
    public ChangeSource Source { get; }

    public bool ByLocalPlayer => Source == ChangeSource.LocalPlayer;

    public string Id => Switches.IdOf(Button);
}

/// <summary>
/// Wall switches, levers and buttons (the game's <c>Button</c>): the things that drive lights, doors and
/// power through their outputs. A switch only holds its own state; what it switches is wired in the scene.
/// Changes are refused (false) while a save is written or loaded.
/// </summary>
public static class Switches
{
    /// <summary>A switch went in or came out, from the player, a script or a timer. Not raised while a save loads.</summary>
    public static readonly WorldEvent<SwitchChange> SwitchChanged = new WorldEvent<SwitchChange>(nameof(SwitchChanged));

    /// <summary>True when the hooks are in place, so <see cref="SwitchChanged"/> is raised.</summary>
    public static bool IsAvailable => PatchAreas.IsAvailable(PatchAreas.Framework, "World");

    public static List<Button> All(bool includeInactive = false) =>
        new List<Button>(Object.FindObjectsByType<Button>(includeInactive ? FindObjectsInactive.Include : FindObjectsInactive.Exclude, FindObjectsSortMode.None));

    public static Button Find(string id) => ObjectResolver.Find<Button>(id);

    /// <summary>GUID where the editor set one, else the full address text.</summary>
    public static string IdOf(Button button)
    {
        if (button == null)
            return null;
        return string.IsNullOrEmpty(button.GUID) ? ObjectAddress.Of(button).ToString() : button.GUID;
    }

    public static bool IsPressed(Button button) => button.PressedIn;

    public static bool IsLocked(Button button) => button.Locked;

    /// <summary>
    /// Presses or releases the switch through the game's <c>Press()</c> and <c>Unpress()</c>, which fire its
    /// outputs and skip the player's key and item checks. A locked switch does not press. True when it is that
    /// way afterwards.
    /// </summary>
    public static bool Set(Button button, bool pressed)
    {
        if (button == null)
            throw new ArgumentNullException(nameof(button));
        if (button.PressedIn == pressed)
            return true;
        if (SaveGate.IsBusy)
            return false;

        using (WorldChanges.EnterMod())
        {
            if (pressed)
                button.Press();
            else
                button.Unpress();
        }
        return button.PressedIn == pressed;
    }
}
