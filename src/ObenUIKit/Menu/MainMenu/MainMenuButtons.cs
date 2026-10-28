// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
using System;
using System.Collections.Generic;
using ObenLoader;
using ObenLoader.Runtime;

namespace ObenUIKit.Menu;

/// <summary>A mod's button on the main menu. Change <see cref="Label"/> or <see cref="Interactable"/> any time.</summary>
public sealed class MainMenuButton
{
    private string _label;
    private bool _interactable = true;

    internal MainMenuButton(string ownerId, string id, string label, Action onClick, int order, int serial)
    {
        OwnerId = ownerId;
        Id = id;
        _label = label;
        OnClick = onClick;
        Order = order;
        Serial = serial;
    }

    /// <summary>The mod that added it.</summary>
    public string OwnerId { get; }

    public string Id { get; }

    /// <summary>Buttons are placed by order, then in the order they were added.</summary>
    public int Order { get; }

    internal int Serial { get; }

    internal Action OnClick { get; }

    /// <summary>The copy of the game's button on the menu right now (null away from the main menu).</summary>
    internal UnityEngine.UI.Button Button { get; set; }

    public string Label
    {
        get => _label;
        set
        {
            _label = value ?? "";
            MainMenuRow.Refresh(this);
        }
    }

    /// <summary>False greys the button out the game's way and ignores clicks.</summary>
    public bool Interactable
    {
        get => _interactable;
        set
        {
            _interactable = value;
            MainMenuRow.Refresh(this);
        }
    }

    /// <summary>It's on the menu right now.</summary>
    public bool IsShown => Button != null;

    /// <summary>Takes the button off the menu.</summary>
    public void Remove() => MainMenuButtons.Remove(this);
}

/// <summary>
/// Adds buttons to the game's main menu, made from the menu's own buttons (same font, colours, hover and
/// click sounds). They go in rows of three under the game's 2x3 grid, and the keyboard and pad reach them:
/// down from the bottom row goes into the mod rows and up comes back. A button goes away when its mod
/// unloads. MODS itself takes the grid's unused "???" slot and isn't one of these.
/// </summary>
public static class MainMenuButtons
{
    private static readonly List<MainMenuButton> Registered = new List<MainMenuButton>();
    private static int _serial;

    /// <summary>Every added button, in the order they are placed.</summary>
    public static IReadOnlyList<MainMenuButton> All => Registered;

    /// <summary>
    /// Adds a button with <paramref name="label"/> that runs <paramref name="onClick"/>, for as long as
    /// <paramref name="owner"/> is loaded. Ids are unique per mod; adding the same id again replaces the old button.
    /// </summary>
    public static MainMenuButton Add(ObenMod owner, string id, string label, Action onClick, int order = 100)
    {
        if (owner == null)
            throw new ArgumentNullException(nameof(owner));
        if (string.IsNullOrEmpty(id))
            throw new ArgumentException("A button needs an id.", nameof(id));
        if (onClick == null)
            throw new ArgumentNullException(nameof(onClick));

        string ownerId = owner.Info.Id;
        var old = Find(ownerId, id);
        if (old != null)
            Remove(old);

        var button = new MainMenuButton(ownerId, id, label ?? id, onClick, order, ++_serial);
        int at = 0;
        while (at < Registered.Count && (Registered[at].Order < order || Registered[at].Order == order))
            at++;
        Registered.Insert(at, button);
        owner.Track(Disposable.Create(() => Remove(button)));
        MainMenuRow.Rebuild();
        return button;
    }

    /// <summary>A mod's button by id, or null.</summary>
    public static MainMenuButton Find(string ownerId, string id)
    {
        foreach (var button in Registered)
        {
            if (button.OwnerId == ownerId && button.Id == id)
                return button;
        }
        return null;
    }

    internal static void Remove(MainMenuButton button)
    {
        if (Registered.Remove(button))
            MainMenuRow.Rebuild();
    }

    internal static int IndexOf(UnityEngine.UI.Button button)
    {
        if (button == null)
            return -1;
        for (int i = 0; i < Registered.Count; i++)
        {
            if (Registered[i].Button == button)
                return i;
        }
        return -1;
    }

    internal static void Init(ObenMod kit)
    {
        Registered.Clear();
        _serial = 0;
        MainMenuRow.Init(kit);
    }
}
