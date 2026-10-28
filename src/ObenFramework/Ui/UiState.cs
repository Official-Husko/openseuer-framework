// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
using System;
using ObenFramework.Console;
using ObenFramework.Events;
using ObenFramework.Game;
using ObenLoader.Runtime;
using TMPro;
using UnityEngine.EventSystems;

namespace ObenFramework.Ui;

/// <summary>
/// What the player's screen is busy with right now: the pause menu, one of the game's menus (inventory,
/// map, tasks), a shop, a dialogue, the F1 console, a text field, a mod window. Every property reads the
/// game's own state on the spot, so there's nothing to keep in sync and nothing runs per frame.
/// UI mods report their windows and text fields with <see cref="Report"/>; ObenUIKit does that for its own.
/// </summary>
public static class UiState
{
    /// <summary>The pause menu opened. The payload is true when it opened for the death screen.</summary>
    public static readonly GameEvent<bool> PauseOpened = new GameEvent<bool>(nameof(PauseOpened));

    /// <summary>The pause menu closed.</summary>
    public static readonly GameEvent PauseClosed = new GameEvent(nameof(PauseClosed));

    /// <summary>The first mod window opened or the last one closed, as reported through <see cref="Changed"/>. The payload is <see cref="IsModWindowOpen"/>.</summary>
    public static readonly GameEvent<bool> ModWindowChanged = new GameEvent<bool>(nameof(ModWindowChanged));

    private static bool _lastModWindow;

    /// <summary>The main menu scene is loaded.</summary>
    public static bool InMainMenu => WorldState.InMainMenu;

    /// <summary>The game's pause menu (or its death screen) is up.</summary>
    public static bool IsPauseMenuOpen
    {
        get
        {
            var pause = PauseMenu.instance;
            return pause != null && pause.PauseIsActive();
        }
    }

    /// <summary>Game time is stopped: the pause menu, a saving screen, or anything else that set the time scale to 0.</summary>
    public static bool IsPaused
    {
        get
        {
            var game = GameController.instance;
            return IsPauseMenuOpen || (game != null && game.GameIsPaused) || UnityEngine.Time.timeScale == 0f;
        }
    }

    /// <summary>One of the game's full menus is open: inventory, map, tasks, tenement, character, notes, building.</summary>
    public static bool IsGameMenuOpen
    {
        get
        {
            var ui = GameUIController.instance;
            return ui != null && ui.IsMenuVisible;
        }
    }

    /// <summary>A shop or trade window is open.</summary>
    public static bool IsShopOpen
    {
        get
        {
            var shop = ShopController.instance;
            return shop != null && shop.menuIsOpen;
        }
    }

    /// <summary>A conversation is running.</summary>
    public static bool IsDialogueActive
    {
        get
        {
            var dialogue = DialogueController.instance;
            return dialogue != null && dialogue.DialogueActive;
        }
    }

    /// <summary>The F1 developer console is open.</summary>
    public static bool IsConsoleOpen => GameConsole.IsOpen;

    /// <summary>A mod window that takes the player's input is open (reported by UI mods, ObenUIKit among them).</summary>
    public static bool IsModWindowOpen => UiReporters.Any(UiActivity.ModWindow);

    /// <summary>
    /// The keyboard is taken by typing: a focused text field (the game's, the console's or a mod's), the
    /// game's own write lock (notes), a mod key capture, or the game waiting for a key in its key bindings page.
    /// </summary>
    public static bool IsTyping
    {
        get
        {
            if (TextFieldFocused() || ConsoleFocused() || UiReporters.Any(UiActivity.Typing) || IsRebindingKey)
                return true;
            var game = GameController.instance;
            return game != null && game.writeLock;
        }
    }

    /// <summary>The game's key bindings page waits for the player to press a new key.</summary>
    public static bool IsRebindingKey
    {
        get
        {
            var bindings = KeyBindingsMenu.instance;
            return bindings != null && bindings.HasKeySelected();
        }
    }

    /// <summary>The game holds the player's keys (a menu, a cutscene, sitting at a computer, a mod window in a level).</summary>
    public static bool IsInputLocked
    {
        get
        {
            var game = GameController.instance;
            return game != null && game.keysDisabled;
        }
    }

    /// <summary>The game's HUD is hidden (the hide-HUD key, a full menu).</summary>
    public static bool IsHudHidden
    {
        get
        {
            var ui = GameUIController.instance;
            return ui != null && !ui.GameUIVisible();
        }
    }

    /// <summary>A save is loading.</summary>
    public static bool IsLoading => WorldState.IsLoadingSave;

    /// <summary>Any menu is open: the pause menu, a game menu, a shop, the console or a mod window.</summary>
    public static bool IsMenuOpen =>
        IsPauseMenuOpen || IsGameMenuOpen || IsShopOpen || IsConsoleOpen || IsModWindowOpen || UiReporters.Any(UiActivity.Menu);

    /// <summary>
    /// The player isn't free to play right now: outside a level, loading, a menu or dialogue is open,
    /// they are typing, or the game holds their keys. The usual check before reacting to a mod's own key.
    /// </summary>
    public static bool IsBusy =>
        !WorldState.InWorld || IsLoading || IsMenuOpen || IsDialogueActive || IsTyping || IsInputLocked || IsPaused;

    /// <summary>All of the above at once, for logging or the dev bridge.</summary>
    public static UiSnapshot Read() => new UiSnapshot
    {
        InMainMenu = InMainMenu,
        InWorld = WorldState.InWorld,
        Loading = IsLoading,
        Paused = IsPaused,
        PauseMenu = IsPauseMenuOpen,
        GameMenu = IsGameMenuOpen,
        Shop = IsShopOpen,
        Dialogue = IsDialogueActive,
        Console = IsConsoleOpen,
        ModWindow = IsModWindowOpen,
        Typing = IsTyping,
        RebindingKey = IsRebindingKey,
        InputLocked = IsInputLocked,
        HudHidden = IsHudHidden,
        Menu = IsMenuOpen,
        Busy = IsBusy,
    };

    /// <summary>
    /// Tells the framework about a UI of your own: <paramref name="active"/> is asked whenever one of the
    /// properties above is read (keep it cheap). Goes away when <paramref name="owner"/> unloads or the
    /// result is disposed. Call <see cref="Changed"/> when a reported mod window opens or closes.
    /// </summary>
    public static IDisposable Report(ITracker owner, UiActivity kind, Func<bool> active)
    {
        if (owner == null)
            throw new ArgumentNullException(nameof(owner));
        if (active == null)
            throw new ArgumentNullException(nameof(active));
        var registration = UiReporters.Add(kind, active);
        owner.Track(registration);
        return registration;
    }

    /// <summary>A reporter's mod windows opened or closed: raises <see cref="ModWindowChanged"/> if the overall answer changed.</summary>
    public static void Changed()
    {
        bool open = IsModWindowOpen;
        if (open == _lastModWindow)
            return;
        _lastModWindow = open;
        ModWindowChanged.Raise(open, GameEvents.Log);
    }

    internal static void Init(ObenLoader.ObenMod framework)
    {
        UiReporters.Clear();
        _lastModWindow = false;
        framework.Track(Disposable.Create(UiReporters.Clear));
    }

    internal static void RaisePauseOpened(bool death) => PauseOpened.Raise(death, GameEvents.Log);

    internal static void RaisePauseClosed() => PauseClosed.Raise(GameEvents.Log);

    private static bool TextFieldFocused()
    {
        var events = EventSystem.current;
        if (events == null)
            return false;
        var selected = events.currentSelectedGameObject;
        return selected != null && selected.TryGetComponent(out TMP_InputField field) && field.isFocused;
    }

    private static bool ConsoleFocused()
    {
        var console = DeveloperConsole.Instance;
        return console != null && console.consoleOpened && console.InputField != null && console.InputField.isFocused;
    }
}
