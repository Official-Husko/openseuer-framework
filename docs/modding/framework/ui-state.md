# UI state and input capture

`ObenFramework.Ui` answers two questions every mod with a key of its own runs into: is the player busy with
a menu right now, and how do I stop the game from reacting to the key I'm using? Source: `src/ObenFramework/Ui/`.

## UiState

Every property reads the game's own state when you ask, so nothing runs per frame and nothing can go
stale.

| Property | True when |
| --- | --- |
| `InMainMenu` | the main menu scene is loaded |
| `IsPauseMenuOpen` | the pause menu (or the death screen) is up |
| `IsPaused` | game time is stopped: the pause menu, a saving screen, anything that set the time scale to 0 |
| `IsGameMenuOpen` | one of the game's full menus is open: inventory, map, tasks, tenement, character, notes, building |
| `IsShopOpen` | a shop or trade window is open |
| `IsDialogueActive` | a conversation is running |
| `IsConsoleOpen` | the F1 console is open |
| `IsModWindowOpen` | a mod window that takes input is open (ObenUIKit's windows, and anything another UI mod reports) |
| `IsTyping` | a text field has the keyboard (the game's, the console's or a mod's), the game's write lock is on (notes), a mod key capture waits, or the game's key bindings page waits for a key |
| `IsRebindingKey` | the game's key bindings page waits for a key |
| `IsInputLocked` | the game holds the player's keys (`GameController.keysDisabled`: menus, cutscenes, a computer, a kit window in a level) |
| `IsHudHidden` | the game's HUD is hidden (the hide-HUD key, a full menu) |
| `IsLoading` | a save is loading |
| `IsMenuOpen` | any of: pause menu, game menu, shop, console, mod window, or a menu a mod reported |
| `IsBusy` | the player isn't free to play: not in a level, loading, a menu or dialogue, typing, keys locked, or paused |

`UiState.Read()` returns all of them at once as a `UiSnapshot` (the dev bridge's `ui.state` prints it).

```csharp
using ObenFramework.Ui;

if (!UiState.IsBusy && Input.GetKeyDown(KeyCode.G))
    OpenMyPanel();

UiState.PauseOpened.Subscribe(this, death => SaveMyStuff());
UiState.PauseClosed.Subscribe(this, () => Resume());
UiState.ModWindowChanged.Subscribe(this, open => Log.Info(open ? "a mod window opened" : "mod windows closed"));
```

- `PauseOpened` (payload: true for the death screen) and `PauseClosed` come from the pause menu's own open and close
  methods. A close is only reported when the menu was really open.
- `ModWindowChanged` fires when the first mod window opens and when the last one closes.

### Reporting your own UI

ObenUIKit reports its windows and text fields already. A mod that draws its own UI tells the framework
about it, so other mods' checks (and the kit's hotkeys and HUD) see it:

```csharp
UiState.Report(this, UiActivity.ModWindow, () => _panel != null && _panel.activeSelf);
UiState.Report(this, UiActivity.Typing, () => _chatField.isFocused);
// after the panel opens or closes:
UiState.Changed();
```

The function is asked whenever one of the properties is read, so keep it to a field check. It goes away
when the owner unloads (or when the returned `IDisposable` is disposed). A reporter that throws is logged
and dropped.

## InputCapture

The game reads its actions ("Interact", "Jump", "Inventory", ...) through its `InputManager`. A capture
makes those reads come back as not pressed, until it is disposed or its owner unloads:

```csharp
// The player presses E at our prompt; the game shouldn't also interact with whatever is in front of them.
IDisposable hold = InputCapture.Key(this, KeyCode.E);
...
hold.Dispose();

InputCapture.Action(this, "Jump");   // one action by its key binding name
InputCapture.All(this);              // every action: no walking, menus or using things
```

- The names are the game's key binding names: `Forward`, `Backward`, `Left`, `Right`, `Walk`, `Crouch`,
  `Jump`, `Interact`, `Menu`, `Inventory`, `Map`, `Tasks`, `Tenement`, `Character`, `Notes`, `Pause Menu`, `Hide Hud`,
  `Hints`, `Primary Use`, `Auxiliary Use`, `Head Use`, `Primary Reload`, `Auxiliary Reload`, `Building Menu`,
  `Use Item`, `Exit Cancel` and a few building and inventory ones (`InputManager.keysdefault`).
- `Key(owner, key)` holds back every action bound to that key on the keyboard (and on the pad while the
  player uses one), and follows the player's bindings as they change.
- Your mod still sees the key: captures only change what the game's `InputManager` answers, not
  `UnityEngine.Input`.
- `InputCapture.Any`, `AllCaptured`, `IsCaptured(action)` and the `Changed` event (true when the first
  capture starts, false when the last ends) tell what is going on.
- While a text field has the keyboard (`UiState.IsTyping`), the game's actions are held back as well, except
  the pause menu, menu, exit and interact keys, so typing in a mod's field doesn't walk the player around.
  Only for the keyboard; the pad keeps working. `InputCapture.WhileTyping = false` turns that off.

What it can't do: a few keys the game reads straight from Unity (`Input.GetKeyDown`) aren't affected. Those
are F1 (console), F11/F12, the number keys of the toolbelt and the main menu's own navigation keys. Esc opens
the pause menu through the `Pause Menu` action, so capturing that action (or Esc) keeps the pause menu shut. To keep the player from moving at all while a window is up, ObenUIKit locks the game's
controls the game's own way (`GameController.ControlsDisabled`) instead.

The patches are postfixes on `InputManager.GetKeyDown`, `GetKeyDownKeyboardOnly`, `GetKeyUp`, `GetKey` and
`GetKeyWithHold`. They only look further when the game's answer was "pressed", so the cost while nothing is
held is one bool check.
