# UI, input, menus and the developer console

Notes on how Obenseuer handles input, UI, menus, the dev console and localization, written for mod authors. Unity 6000.0.77f1, Mono, Windows x64 build under Proton.

Paths like `DeveloperConsole.cs:40` are relative to `decompiled/Assembly-CSharp/`. All types live in the global namespace of `Assembly-CSharp`. Things I could not confirm from the decompile are marked "unverified".

## 1. What the game ships (from `Managed/`)

| Package | Present | Actually used by game code |
|---|---|---|
| `Unity.InputSystem.dll`, `Unity.InputSystem.ForUI.dll` | yes | Only `ComputerControllerInput.cs` (`Gamepad.current` sticks/buttons for in-world computer screens). Nothing else. |
| Legacy `UnityEngine.InputLegacyModule` | yes | Everything else: `Input.GetKey*`, `Input.GetAxis*`, `Input.anyKeyDown`. |
| `Unity.TextMeshPro.dll` | yes | All text. 121 files `using TMPro`. |
| `UnityEngine.UI.dll` (UGUI) | yes | All menus and HUD (`Button`, `Slider`, `CanvasGroup`, `ScrollRect`, `GraphicRaycaster`...). |
| `UnityEngine.UIElementsModule.dll` | yes (engine module) | Not used. No `UnityEngine.UIElements` in game code. |
| `Unity.Localization.dll` | yes | Language selection + a few tables. See section 6. |
| `Unity.Addressables.dll` | yes | String tables, some assets (`Addressables.LoadAssetAsync<TaskItem>` in the console). |
| `UnityEngine.IMGUIModule.dll` | yes | `OnGUI` used for key capture only (`InputManager.cs:133`, `KeyBindingsMenu.cs:178`). |
| Other | | `LitJson` for settings JSON, `FullSerializer` for saves, `PixelCrushers.DialogueSystem` for dialogue, `DOTween`, `Cinemachine`. |

Which UI input module the scene's `EventSystem` uses is unverified (`InputSystemUIInputModule` vs `StandaloneInputModule`). Check at runtime with `EventSystem.current.currentInputModule`. Either way the game's own menu navigation does not go through the EventSystem's navigation; it drives selection by hand (section 4).

## 2. Input

### 2.1 Overall model

The game uses the **legacy Input manager** end to end. There is no `InputActionAsset`, no `PlayerInput`, no action maps. Gameplay code asks `InputManager.instance` for an action **by string name**, and `InputManager` maps that name to a `KeyCode` (keyboard) and a second `KeyCode` (controller button), then calls `UnityEngine.Input.GetKey*`.

Axes from `ProjectSettings/InputManager` that the game reads directly: `Horizontal`, `Vertical`, `Mouse X`, `Mouse Y`, `Mouse ScrollWheel`, `LeftStickInventoryHorizontal/Vertical`, `RightStickHorizontal/Vertical`, `DPadHorizontal/Vertical`, `LeftTrigger`, `RightTrigger`, `InventoryHorizontal/Vertical`. These all exist in `globalgamemanagers`.

`CrossPlatformInputManager`, `VirtualInput`, `StandaloneInput`, `MobileInput` are leftovers from Unity Standard Assets. Nothing in gameplay reads them. Ignore.

### 2.2 `InputManager` (`InputManager.cs`)

Singleton, `DontDestroyOnLoad`, created at boot (`InputManager.cs:67`, `Awake`). `InputManager.instance` is available before the main menu appears.

```csharp
public static InputManager instance;
public KeyBindings keyBindings;            // ScriptableObject holding current KeyCodes
public KeyBindingsSave keyBindingsSave;    // JSON persistence
public InputType inputType;                // Keyboard | Controller
public event OnInputTypeChanged InputTypeChanged;
public bool GetKeyDown(string key);        // :188  keyboard OR controller binding
public bool GetKey(string key);
public bool GetKeyUp(string key);
public bool GetKeyDownKeyboardOnly(string key);
public bool GetKeyWithHold(string key, int resetValue = 4);   // :220 key repeat for menus
public void SetKey(string keyName, KeyCode key);
public KeyCode GetKeyCode(string key, bool ignoreController = false);   // :244
public string GetKeyCodeDisplayText(string key / KeyCode, ...);
public void ChangeInputType(InputType t);   // :62
public void ResetToDefaults();              // :334
```

How a read works (`InputManager.cs:188`): `IsKeyDown(keyBindings.CheckKey(name)) || IsKeyDown(keyBindings.CheckKeyController(name))`. `IsKeyDown` is `Input.GetKeyDown(k) || guiKeysDown.Contains(k)`.

The `OnGUI` hook (`:133`) records `Event.current` key events into `guiKeysDown/Held/Up` and skips "layout sensitive" keys (letters, digits, keypad digits, punctuation, `BackQuote`). It is a fallback for keys where `Input.GetKey` misses events; the sets are cleared in `LateUpdate`. It means non-layout keys such as F-keys, arrows, Enter, Space, modifiers can be seen via either path.

Side effect to know about: `KeyBindings.CheckKey` and `CheckKeyController` (`KeyBindings.cs:252`, `:316`) call `InputManager.ChangeInputType` whenever the bound key is currently held. Querying a game action name from a mod while that key is down can flip `inputType` and fire `InputTypeChanged`. It is harmless, just be aware if you poll game action names every frame.

### 2.3 Action names

The valid names are hard-coded in `KeyBindings.GetKey` (`KeyBindings.cs:262`) and `GetControllerKey` (`:323`). An unknown name resolves to `KeyCode.None`, so `GetKeyDown("MyAction")` is silently false. You cannot register new actions without patching those two private methods plus `ChangeKey` (`:359`) and `KeyBindingsSave`.

| Name | Default key | Controller |
|---|---|---|
| Forward / Backward / Left / Right | W / S / A / D | stick (axes) |
| Walk | LeftShift | |
| Crouch | LeftControl | R-stick click |
| Jump | Space | L-stick click |
| Interact | E | A |
| Menu | Tab | B |
| Inventory / Map / Tasks / Tenement / Character / Notes | I / M / T / H / J / N | |
| Pause Menu | Escape | Menu (Button7) |
| Hide Hud | P | |
| Hints | K | View (Button6) |
| Primary Use | Mouse0 | RB |
| Auxiliary Use | F | LB |
| Head Use | B | |
| Primary Reload | G | X |
| Auxiliary Reload | R | Y |
| Building Menu | L | View |
| Move Left / Move Right | Q / E | LB / RB |
| Place Take | Mouse0 | A |
| Rotate Object | Mouse1 | |
| Rotation Mode | V | Y |
| Snap Object | C | X |
| Buy Sell | F | A |
| Resource Slots | N | Y |
| Move Item | F | X |
| Drag And Drop | C | R-stick click |
| Drop | R | Y |
| Switch Tab Left / Right | None / Q | LB / RB |
| Use Item | X | A |
| Exit Cancel | Tab | B |
| Up / Down | not bound by default | (noclip only) |
| screenshot / screenshotNoUI | field only | |
| Backspace / Space / Type | | controller virtual keyboard only |

Defaults are in `InputManager.SetDefaultKeys` (`:291`). `GameController.cs:41` asks for `"Screenshot"` (capital S) which is not a known name, so screenshots effectively only work through the hard-coded F11 and F12.

Hard-coded keys that bypass `InputManager`, so rebinding does not affect them and a mod must avoid them:

| Key | Where | Purpose |
|---|---|---|
| F1 (key up), pad View+Menu chord | `DeveloperConsole.cs:358` | toggle dev console |
| F11 / F12 | `GameController.cs:41,45` | screenshot / screenshot without UI |
| Alpha1..Alpha4 | `GameController.cs` Update | primary, auxiliary, backpack, head slot |
| X, Return, Escape, arrows | `MainMenuNavigationHandler.cs:124`, `PauseNavigationHandler.cs:96`, `UINavigationCore` | menu confirm / back |
| Joystick1Button0..9 | many | controller |

Keys nothing in the game reads: F2 to F10, `BackQuote`, Insert, Delete, Home, End, PageUp, PageDown, ScrollLock, Pause. Pick mod hotkeys from those and compare against `InputManager.instance.keyBindings` at startup, since the player can bind any key to any action with no conflict check (`KeyBindingsMenu.cs:178` takes any `Event.keyCode`).

### 2.4 Persistence and rebinding

| File (in `Application.persistentDataPath`) | Class | Content |
|---|---|---|
| `KeyBindings.json` | `KeyBindingsSave.cs` (`SaveKeyBindings :13`, `GetKeyBindings :139`) | one `KeyCode.ToString()` per action, fixed set of fields in `KeyBindingsList.cs` |
| `Controls.json` | `ControlsSave.cs` (`SaveControls :26`, `GetControlsSettings :47`) | `mouseSensitivity`, `invertMouse`, `toggleSprint`, `toggleCrouch` only |
| `Display.json`, `Quality.json`, `Volume.json` | `DisplaySave`, `VolumeSave` | display settings including `language` |

Under Proton `persistentDataPath` is `AppData\LocalLow\<company>\<product>` inside the prefix.

Rebinding flow: `KeyBindingsMenu.ChangeKey(GameObject)` marks a row as pending, then `KeyBindingsMenu.OnGUI` (`:178`) takes the next key event, calls `InputManager.SetKey(name, keyCode)` then `keyBindingsSave.SaveKeyBindings(name)`. Escape cancels. `GetKeyBinding` parses with `Enum.Parse(typeof(KeyCode), s)`. A mod cannot add rows to this UI without cloning prefabs, and the JSON schema has no place for extra actions.

`KeyBindingsMenu.instance.HasKeySelected()` is true while a row waits for a key. `HintOverlay` and `UINavigationHelperBase.GetNextSelectedObjectAndChangeStateIfNecessary` both check it. A mod hotkey handler should do the same so it does not fire while the player is rebinding.

### 2.5 Recommended approach for a mod

1. Read mod hotkeys directly with `UnityEngine.Input.GetKeyDown(KeyCode)` in your own `Update`. Do not use `InputManager` names for mod actions. Store your own bindings in your own config file (for example `<persistentDataPath>/mods/keybinds.json`).
2. Gate every mod hotkey on the same conditions the game uses:
   - `GameController.instance != null && !GameController.instance.keysDisabled && !GameController.instance.GameIsPaused && !GameController.instance.writeLock`
   - `!DeveloperConsole.Instance.consoleOpened`
   - `!(GameUIController.instance?.IsMenuVisible ?? false)` if the hotkey should not work in inventory
   - `KeyBindingsMenu.instance == null || !KeyBindingsMenu.instance.HasKeySelected()`
   - no focused `TMP_InputField` (`EventSystem.current.currentSelectedGameObject` has a `TMP_InputField` with `isFocused`)
3. To react to a game action (for example a mod that adds behaviour on Interact), prefer a Harmony postfix at the game's own call site, or poll `InputManager.instance.GetKeyDown("Interact")`. That respects rebinding and controller.
4. If you need the glyph for a bound action, use `InputManager.instance.GetKeyCodeDisplayText("Interact")`. `HintContent.ResolveKeys` does this for `{key:Name}` placeholders.
5. Controller support for a mod's own hotkeys: read `Input.GetKeyDown(KeyCode.Joystick1ButtonN)`. The game assumes an Xbox layout: A=0, B=1, X=2, Y=3, LB=4, RB=5, View=6, Menu=7, L-stick=8, R-stick=9. Only Joystick1 is read.

### 2.6 Blocking game input while a mod UI has focus

`GameController` (`GameController.cs`) is a scene singleton (`GameController.instance`) that exists only in gameplay scenes. It is **null in the main menu**. Every call must be null-guarded.

```csharp
public bool GameIsPaused, keysDisabled, writeLock, inventoryDisabled, cursorVisible;   // :5-:17
public void ControlsDisabled(GameObject owner, bool ShowCursor = false, bool disableKeys = true,
    bool writeLock = false, bool lockedFirsperson = false, bool firstpersonSoftLock = false,
    Transform softLockTarget = null, bool disableCamera = true, bool disableMovement = true);  // :285
public void ControlsEnabled(GameObject owner, bool disableFirstpersonSoftLock = false);        // :344
public void DisableInventory(GameObject owner);  public void EnableInventory(GameObject owner);
public void ChangeCursorStatus(CursorLockMode mode, bool visible);                              // :379
public void UpdateCursorVisibility();                                                           // :386
```

The pattern is a reference-counted lock keyed by `GameObject`. Each system calls `ControlsDisabled(itsOwnGameObject, ...)` when it opens and `ControlsEnabled(sameGameObject)` when it closes. Lists `disabledList`, `disabledKeysList`, `disabledWritelockList`, `disabledCursorList` are per-flag. The flags only clear when the matching list is empty, so a mod can stack its own lock with the game's without breaking anything. Use a stable GameObject of your own as the key (not `GameController.gameObject`, which the console uses).

What `ControlsDisabled(modGo, ShowCursor: true)` does with defaults:
- `keysDisabled = true`. `GameController.Update` then returns before the gameplay key handling (`:71`, `keysDisabled && !GameIsPaused`). That blocks Inventory/Map/Tasks/Menu keys, item use, Alpha1..4, Interact, Pause key to open the pause menu, and Hide Hud.
- `ThirdPersonCameraController.controlsDisabled` and `ThirdPersonCameraCollision.controlsDisabled = true`: no mouse look, no zoom.
- `OpenSewerCharacterController.instance.DisableControls(owner, noclip:false)` (`OpenSewerCharacterController.cs:174`): player stops responding to movement.
- Cursor unlocked and visible (`ChangeCursorStatus(None, true)`). The cursor is hidden anyway when `InputManager.inputType == Controller` (`UpdateCursorVisibility`, `:386`), and `MouseBlocker.instance.ToggleBlocker(!Cursor.visible)` toggles a full-screen raycast blocker. A mouse-driven mod UI is unusable while the game thinks a controller is active until a mouse move or bound key press switches `inputType` back (only the Main/Pause/Inventory navigation handlers watch for mouse movement, see `UINavigationBase.ShowCursorIfNecessary`).

Other flags:
- `writeLock`: for text entry. When set, `GameController.Update` only watches Pause Menu or Menu to call `GameUIController.WriteLockClose()` (which tells the notes panel to stop editing, `GameUIController.cs:302`). It is meant for the notes editor. Do not set it for a mod's own text input unless you are also happy for Esc and Tab to call `NoteController.instance.notesPanel.StopEditing()`. The notes code reads `writeLock` in `InventoryNavigationHandler.cs:1009,1610` and `NotesPanelNavigation.cs`. Do not use it.
- `keysDisabled` alone is enough to stop hotkeys, and it is what the console uses.
- `GameIsPaused` is set by the pause menu and by `SavingUI.ShowSavingUI`. While paused, `keysDisabled` is ignored (`keysDisabled && !GameIsPaused`).
- `DisableInventory(go)` only stops the inventory/map/tasks keys.

What `keysDisabled` does **not** stop:
- Code that calls `Input.*` directly: `QuickSlotBar` checks `keysDisabled` itself (`QuickSlotBar.cs:147`) and `BuildingSystem.cs:617` does too. The shop (`ShopController.menuIsOpen` returns early at `GameController.cs:60`) and `InteractableComputer` have their own `Controls()` calls.
- The first lines of `GameController.Update`: `"Pause Menu"` always triggers `GameUIController.ShowGameUI(go)` (restores a hidden HUD) before any lock check.
- Menu navigation handlers: `MainMenuNavigationHandler` has its own gate (`ControlsEnabled`), `PauseNavigationHandler` has none (section 4.3).
- The dev console toggle (F1).

Time: `PauseMenu.PauseMenuOpen` sets `Time.timeScale = 0` (`PauseMenu.cs:216`); `PauseMenu.paused => Time.timeScale == 0f`. Mod UI that animates while paused should use unscaled time. The game uses `WaitForSecondsRealtime` and `Time.unscaledTime` for this.

Minimal mod-UI lock:

```csharp
void Open()  { GameController.instance?.ControlsDisabled(gameObject, ShowCursor: true); }
void Close() { GameController.instance?.ControlsEnabled(gameObject); }
```

Call `Close` from `OnDisable`/`OnDestroy` as well, so a scene change cannot leave a lock behind (the lists hold GameObject references and `GameController` is destroyed with the scene anyway, but `GameIsPaused`/`Time.timeScale` are not).

Handle Esc yourself: because `keysDisabled` suppresses the pause menu, Esc will not reach anything. Read `InputManager.instance.GetKeyDown("Pause Menu")` (rebindable) in your UI to close it. The console uses `Pause Menu` the same way (`DeveloperConsole.cs:366`), and closes with a one-frame delay (`Closelate`, `:415`) so the same keypress does not also open the pause menu.

## 3. UI stack

### 3.1 Canvases and singletons

All of these are scene-placed MonoBehaviours with a static `instance`. The decompile does not contain scene hierarchies, so canvas names/render modes are unverified; discover at runtime with `FindObjectsOfType<Canvas>()`.

| Type | File | Role |
|---|---|---|
| `GameUIController` | `GameUIController.cs` | Owner of the in-game menu (tabbed inventory/map/tasks/...) and HUD visibility. `GameUI` is a `CanvasGroup` (alpha 0/1 for Hide Hud). |
| `GameUIInstance` | `GameUIInstance.cs` | 15 lines. Sits on the main HUD canvas root, caches its `GraphicRaycaster` in `Start`. `GameUIInstance.instance.GetComponent<Canvas>()` is a handy handle to the HUD canvas. |
| `FloorUIManager` | `FloorUIManager.cs` | Not a general UI manager. It is the Tenement tab's per-floor upgrade panel (`UpdateOverview/Yard/Basement/Roof/Floor`). Nothing for mods here. |
| `HintOverlay` | `HintOverlay.cs` | Side panel with context hints (section 3.4). |
| `ToolTip` / `TooltipUI` | `ToolTip.cs`, `TooltipUI.cs` | Hover tooltip for items (section 3.4). |
| `UseText` | `UseText.cs` | Centre-bottom "Object (action)" text shown when looking at an interactable. |
| `Notifications` | `Notifications.cs` | Toast notifications, top corner (section 3.3). |
| `PauseMenu` | `PauseMenu.cs` | Pause screen. |
| `MainMenu` | `MainMenu.cs` | Main menu scene logic. |
| `DeveloperConsole` | `DeveloperConsole.cs` | Own canvas, `DontDestroyOnLoad`. |
| `LoadingScreen`, `SavingUI`, `DeathMessage`, `WaitingUI`, `KeypadUI`, `PhoneUI`, ... | | Feature-specific. |

`GameUIController` API that matters (`GameUIController.cs`):

```csharp
public bool IsMenuVisible { get; set; }            // also true while WaitingUI is visible
public void ToggleGameMenu(bool force, bool playSound, MenuPanels panel, MenuPageNames page, bool ignoreDialogue = false); // :217
public void ShowGameMenu(bool forceOpen, bool playSound, MenuPanels panel, MenuPageNames page, bool ignoreDialogue = false); // :240
public void HideGameMenu(bool forceHide = false);  // :270
public void CloseAllMenus(bool force = false);
public bool IsAnyMenuOpen();
public void ToggleGameUIVisibility(GameObject owner); public void HideGameUI(GameObject owner); public void ShowGameUI(GameObject owner);
public static event OnExitPanels ExitPanels;       // :132 fired when the in-game menu closes
```

`ShowGameMenu` calls `ControlsDisabled(gameUIController.gameObject, ShowCursor:true, disableKeys:false)`, enables blur (`EffectsController.instance.EnableBlur()`), and sets `IsMenuVisible`. `MenuPageNames` is `{None, Inventory, Character, Map, Tenement, Tasks, Notes, Items}` and `MenuPanels` is a 21-value enum of contextual panels (storage, bank, doctor, ...). The tab strip is `UIPageButtons` with a private serialized `MenuPage[] MenuPages` (`UIPageButtons.cs:7`), where each entry binds a name, a key string, a button and a panel. Adding a tab means cloning a `MenuPage` entry through reflection and patching `Select`/`SelectByString`/`GetPageIndex`. It is possible but brittle; a standalone overlay canvas is far easier.

### 3.2 Fonts and assets to match the game's look

There is no code that references fonts (no `TMP_FontAsset` fields in `Assembly-CSharp`), so they are assigned in prefabs. From the string tables of `resources.assets` the TMP assets present are:

| Asset (string seen in `resources.assets`) | Likely use |
|---|---|
| `Erika Ormig SDF` plus materials `Default`, `Outlines`, `Thick Outline`, `Shadow`, `Slot`, `Pause Menu`, `Outlines Time`, `Outlines Notification Title` | Main UI font and the material variants for menus, slots, notification titles |
| `LiberationSans SDF` (`- Outline`, `- Drop Shadow`) | TMP default / fallback atlas |
| `VT323-Regular SDF` | Terminal look (in-world computers, probably console) |
| `Swansea-q3pd SDF` | Unknown, a second display font |
| `MobileMan-4nn4 SDF`, `LCDPHONE SDF Material` | Phone UI |
| `Open Sewer Icons SDF` (`... 2`) | Icon font |
| `EmojiOne` | Emoji sprite asset |

Names are from raw asset strings, so confirm at runtime: `Resources.FindObjectsOfTypeAll<TMP_FontAsset>()` and `Resources.FindObjectsOfTypeAll<Material>()` filtered by name. The easiest way to look native is to **clone an existing TMP text** (for example the text on a pause menu button) and reuse its `font`/`fontSharedMaterial`/`color` rather than building TMP components from scratch.

Colour helpers in code:
- `UIColors.cs`: `greenTextColorCodeLight = "<#66FF60>"`, `redTextColorCodeLight = "<#FF6B60>"`, `redTextColorCodeDark = "<#A8211F>"` (TMP rich text prefixes).
- Selected-key orange `Color32(239, 116, 32, 255)` in `KeyBindingsMenu.cs:106`.
- Console: red `ED0000`, yellow `FFDA00` (`DeveloperConsole.cs`).

Reusable widget components (copy from existing objects, do not `AddComponent` on empty objects since sounds/sprites are serialized fields):
- `DefaultUIButton` (`DefaultUIButton.cs`): hover/press sounds `SoundInfo pressSound/hoverSound`, `Selected()/Unselected()`, `ChangeControllerKey(KeyCode)`, `ChangePressed(bool)`.
- `TextButtonColorEffectTMP`: text-colour hover button used by menu text buttons. `Enable()/Disable()`, `interactable`.
- `OptionsButton`: the options rows with left/right buttons or a slider (`buttonLeft`, `buttonRight`, `slider`, `PressLeft()`, `PressRight()`).
- `HoverSoundEffect`, `InputUI` (key glyph widget configured by `keyCode` string).

### 3.3 Showing a message to the player

There is no single "message" API. These are the options, best first for a mod:

**`Notifications.instance.CreateNotification(string type, string text, bool silent = false)`** (`Notifications.cs:89`). This is the HUD toast used by inventory ("Item added!"), money, crime, skills, recipes, etc. `type` is the small title line, `text` the body. About 80 call sites across the game, so it is the house style.
- Queued: each notification waits until it is first in `upcomingNotifications` and fewer than 3 children are in the panel (`DelayNotification`, `:95`), then `NotificationBox.ShowNotification` fades in, plays a sound unless `silent`, stays 8 seconds of real time (`NotificationBox.cs:63`), fades out and destroys.
- Both fields are TMP text, so rich text tags work. Strings are not localized; the game passes literal English.
- `Notifications` is a `SavableScript` and serializes `visibleNotifications`/`upcomingNotifications` into the global save (`OnSavingGame :37`). A toast pending at save time reappears after load. Not a problem, just noise in saves.
- `Notifications.instance` is null in the main menu and during scene transitions; guard it. `Notifications.EventFired` (static event) fires whenever the visible set changes. `NotificationsPanel` mirrors visible toasts into the in-game menu.

**`UseText.instance.ChangeUseText(string objectName, string useText, bool show = true, bool red = false, bool green = false, bool passive = false, bool autoWrap = true)`** (`UseText.cs:83`), plus `ChangeUseText(string, bool)`, `HideUseText()`, `WarningUseEffect(bool silent)`, `Override(bool)`. This is the crosshair label. Gameplay code overwrites it every frame while looking at things, so it only works for a mod that holds the text with `Override(true)` or re-sets it each frame. Good for "press X to do Y".

**`ToolTip.instance.Activate(string title, string details)`** (`ToolTip.cs:116`) and `Deactivate()` (`:133`). Pops the item tooltip at the cursor with arbitrary text. Only useful with the mouse over something you control.

**`HintOverlay.Show(owner, HintContent, priority, startHidden)`** (`HintOverlay.cs:166`), `Hide(owner)`. Side panel with titled sections; strings can contain `{key:Action Name}` which `HintContent.ResolveKeys` swaps for the current binding. Built as `new HintContent("Title").AddText("Header", "line").AddList("Header", "a", "b")`. The player toggles it with the `Hints` action and the state is in `PlayerPrefs["HintOverlayEnabled"]`. `owner` is a `UnityEngine.Object`; entries are dropped when the owner is destroyed (`PruneDestroyedOwners`). Good for persistent mod help text.

**`ConfirmationNotification.ShowNotification(string title, string content, string confirmText, Action onConfirm)`** (`ConfirmationNotification.cs:42`). A modal yes/no. Not a singleton: it is a serialized field of `ShopBaseUI` (`ShopBaseUI.cs:126`) and only exists inside the shop UI. Not reusable without that scene object.

**`DeveloperConsole.Instance.Print(string)`**. Only visible when the console is open, but also mirrors anything you `Debug.Log` (see 5.4).

Dialogue barks (`SubtitleBarkUI`) and `DialogueController` belong to the Pixel Crushers dialogue system and are tied to NPCs; not suitable for generic messages.

For anything richer (modal dialogs, text input, lists), build your own canvas (section 3.5).

### 3.4 Tooltips and hints, quick reference

```csharp
ToolTip.instance.Activate(string title, string details);   // generic text tooltip
ToolTip.instance.Deactivate();
HintOverlay.Show(UnityEngine.Object owner, HintContent content, int priority = 0, bool startHidden = false);
HintOverlay.Hide(UnityEngine.Object owner);
HintOverlay.Refresh();
```

`HintContext` (a MonoBehaviour with a serialized `HintContent` and `priority`) is how the game attaches hints to scene objects; higher priority wins when several are active.

### 3.5 Building a mod canvas

Nothing in the game creates canvases in code (no `sortingOrder`/`renderMode` use in `Assembly-CSharp`), so there is no helper to copy.
- Create a `Canvas` (ScreenSpaceOverlay, high `sortingOrder`), `CanvasScaler` (ScaleWithScreenSize, reference around 1920x1080 is reasonable), `GraphicRaycaster`, and make sure an `EventSystem` exists (it does in every scene the game uses; `EventSystem.current` is non-null in menus and gameplay, since the navigation handlers construct `PointerEventData` from it).
- Parent it under a `DontDestroyOnLoad` root so it survives scene loads, and re-parent or re-lock on scene change.
- When the cursor is hidden by `UpdateCursorVisibility` (controller active), `MouseBlocker` can eat clicks; if you need the mouse, set `GameController.instance.ChangeCursorStatus(CursorLockMode.None, true)` and `InputManager.instance.ChangeInputType(InputManager.InputType.Keyboard)`.
- Pause blur: `EffectsController.instance.EnableBlur()/DisableBlur()` is what menus use for the dimmed background.

## 4. Menus

### 5.1 The navigation-handler pattern

Controller/keyboard menu navigation is hand-rolled. It does not use `EventSystem` navigation or `Selectable.navigation`.

```
UINavigationCore            (MonoBehaviour)  axis/button constants, HasHorizontalInput(), HorizonalInput()      UINavigationCore.cs
 └ UINavigationBase         abstract         TrySelectingNearbySlot(PanelType), SelectObject(obj, data, sfx)    UINavigationBase.cs:40,:126
    ├ MainMenuNavigationHandler    (singleton, Instance)                                                       MainMenuNavigationHandler.cs
    ├ PauseNavigationHandler       (singleton, Instance)                                                       PauseNavigationHandler.cs
    ├ InventoryNavigationHandler, BankMenuNavigationHandler, KeypadNavigationHandler, VirtualKeyboard...
UINavigationHelperBase      abstract MonoBehaviour per page: CurrentObject, DefaultObject, BackButton,         UINavigationHelperBase.cs
                            GetButtonAbove/Below/OnTheLeft/OnTheRight (Button pages) or
                            HandleObjectInteractionUpwards/Downwards/ToLeft/ToRight (mixed pages)
IUINavigationData           per-handler state: CurrentObject, PointerEventData, CurrentSlotCoordinates         MainMenuNavigationData / PauseMenuNavigationData
```

Each frame a handler reads axes (`DPad*`, `LeftStickInventory*`, or `UINavigationCore.HorizonalInput()` for keyboard), applies a repeat delay (0.3 s then 0.15 s), calls `TryToMove` then `Move(direction, inputType)`, which asks the current page's helper for the next object, then `SelectObject` which un-highlights the old object and **fakes a pointer enter** on the new one (`TextButtonColorEffectTMP.OnPointerEnter`, `DefaultUIButton.OnPointerEnter`, `HoverSoundEffect`). Confirm = `X`, `Return` or pad A calls `button.onClick.Invoke()`. Back = Escape or pad B.

The selectable object types the handler can highlight are a closed set: `UnityEngine.UI.Button`, `OptionsButton`, `TMP_InputField`, `LoadMenuButton`. Anything else throws `ArgumentOutOfRangeException` (`UINavigationBase.cs:126`). A mod page's selectable must be one of those.

Each page is a helper component that hard-codes its layout, for example `MainMenuNavigationHelper` maps each button to its neighbours with an if-chain. This is the part that makes adding things to existing pages awkward.

### 5.2 Main menu

`MainMenu` (`MainMenu.cs`, `MainMenu.Instance`) tracks `currentMenu` of type `MainMenu.CurrentWindow`:
`Main, CharacterMenuInfo/Body/Face/Clothes/Difficulty, Load, Unknown, Credits, LoadRemoveWarning, LoadRemoveSlotWarning, LoadVersionWarning, OptionsDisplay/Quality/Audio/Controls/KeyBindings, Quit`.

Pages move by animator booleans (`MoveRight`, `MoveLeft`, `MoveDown`, `Hide`) and by `SetActive`; button clicks are serialized UnityEvents on scene buttons that call `MainMenu.NewGame()`, `LoadGame()`, `Options()`, `Credits()`, `Quit()`, `Back()`.

Top-level buttons are a 2x3 grid wired in `MainMenuNavigationHelper` (`MainMenuNavigationHelper.cs`):

```
   newGame     loadGame    options
   unknown     credits     quit
```

`GetButtonBelow` maps newGame to unknown, loadGame to credits, options to quit (`:66`), and the above/left/right chains mirror that. The `unknown` button is a serialized field with no code; what it looks like or whether it is visible in the scene is unverified. The handler casts `CurrentObject` to `UnityEngine.UI.Button` (`UINavigationHelperBase.cs:69`), so every main-menu button must be a `Button`.

`MainMenuNavigationHandler` (`MainMenuNavigationHandler.cs`):
- `Update` (`:93`) only runs when `ControlsEnabled && !DeveloperConsole.Instance.consoleOpened`. `ControlsEnabled` is a **public settable property** (`:77`). Setting it false suspends all main menu keyboard/controller navigation. The game itself does that while a text field is being edited and in `PressAnyKey`.
- `Move` (`:484`), `HandleDefaultObjectSelection` (`:374`), `HandleBackButtonClick` are big switches on `MainMenu.CurrentWindow` that **throw** on unknown values (`default: throw new ArgumentOutOfRangeException()`), and so does `MainMenu.DetermineNextMenuWhenMovingBack` (`MainMenu.cs:188`). Do not invent a new `CurrentWindow` value and leave it set during navigation.

How to add a "Mods" entry to the main menu, in increasing order of work:
1. **Separate overlay opened from a button you add.** Clone an existing menu Button (for example `credits`) with `Object.Instantiate(button, parent)` so it keeps its `TextButtonColorEffectTMP`, `HoverSoundEffect`, font and sounds. Replace its `onClick` with your open action. Position it under/next to the grid. Mouse works immediately. When your overlay opens, set `MainMenuNavigationHandler.Instance.ControlsEnabled = false` and run your own input; set it back to true on close and do not touch `MainMenu.currentMenu` (leave it at `Main`).
2. **Keyboard/controller reachability of the new button:** Harmony-patch `MainMenuNavigationHelper.GetButtonAbove/GetButtonBelow/GetButtonOnTheLeft/GetButtonOnTheRight` (all `protected override UnityEngine.UI.Button`, no parameters) with prefixes that return your button when `CurrentObject` is the neighbour you chose, and your own neighbours when `CurrentObject == modButton`. The helper keeps `CurrentObject` as a protected-set property, so read it via `Traverse`/`AccessTools.Property`. Also patch confirm if you need pad-A specific behaviour; `PerformButtonClick` just calls `button.onClick.Invoke()` which already works.
3. **Reusing the `unknown` slot** (if it is a real visible button): set its label text and `onClick`; navigation already reaches it. Unverified whether it is rendered.

The Options entry has its own tab strip (`MainMenuOptions`, `OptionsCategoryName` enum {Display, Quality, Audio, Controls, KeyBindings}); `MainMenu.SetCurrentWindowValueAndCategoryIfNeeded` throws on unknown categories. Do not try to add a sixth options tab; add your own page instead.

Hook points:

```csharp
MainMenu.Awake / Start                          // MainMenu.cs:63 / :76   build your button after the scene is up
MainMenuNavigationHelper.GetButtonAbove/Below/OnTheLeft/OnTheRight   // protected override Button
MainMenuNavigationHandler.Update                // private, :93 (prefix to suspend while your page is open, or use ControlsEnabled)
MainMenu.Options / Credits / LoadGame / Quit    // public, no args   good "page opened" signals
```

### 5.3 Pause menu

`PauseMenu` (`PauseMenu.cs`, `PauseMenu.instance`) owns a set of `Animator`s, one per page (`MainAnimator`, `LoadAnimator`, `SaveAnimator`, `OptionsAnimator`, `Quit*`, `Difficulty/Display/Quality/Audio/Controls/KeyBindingsAnimator`) driven by bool params `MoveUp`/`MoveDown`. `currentMenu` (public `GameObject`) identifies the visible page.

```csharp
public void Pause();                    // :189  opens, or navigates "back" if already open
private void PauseMenuOpen();           // :216  CloseAllMenus, GameIsPaused=true, ControlsDisabled(pauseGo, cursor, disableKeys:false), timeScale=0
private void PauseMenuClose();          //       reverse; also AudioListener.pause, blur off
public void Resume();                   // PauseMenuClose
public bool PauseIsActive();
public static bool paused { get; }      // Time.timeScale == 0
public void DisableSave()/EnableSave(), DisableSave(GameObject)/EnableSave(GameObject)   // ref-counted, shows/hides Save button
```

Opening is triggered by `GameController.Update` when `"Pause Menu"` is pressed with none of: dialogue, building menu, shop, lockpicking, game menu visible, console open (`GameController.cs:76-103`). The game does not open the pause menu while `keysDisabled` is set.

`PauseNavigationHandler` (`PauseNavigationHandler.cs`):
- The main page's buttons are collected **automatically**: `InitializeSlotArrays` (`:490`) runs in `OnEnable` and does `GetAllChildrenOfType<Button>(main.transform)` into a single-column grid (`rowCount` = number of children). So a `Button` added as a **direct child** of the pause main page, before that handler's `OnEnable` runs, gets controller/keyboard navigation for free. Only direct children count (`foreach (Transform tr in parent)`).
- Whether the handler's GameObject is enabled only while the pause screen is open (so `OnEnable` runs on every pause) is unverified. If it is always active, rebuild after adding your button by toggling the handler's `gameObject.SetActive(false/true)`.
- Page navigation goes through `CurrentWindow` enum {Pause, Load, Save, Options, OptionsDisplay..., QuitToMenu, QuitToDesktop, OptionsDifficulty} and `SwitchToNextButtonOrSlider` throws on unknown values. Same rule: do not leave `Data.CurrentWindow` on something new.
- **Back handling** (`HandleBackButtonClick`, `:133`) is a chain of `if (PauseMenu.instance.currentMenu == someHelper.gameObject)`. For an unknown `currentMenu` nothing is invoked and it falls through to `ChangeMenuStateIfNecessary()`. If your page is open, patch `HandleBackButtonClick` with a prefix: if your page is active, close it, restore `PauseMenu.instance.currentMenu = PauseMenu.instance... main page` and `return false`.
- **Gotcha: `PauseNavigationHandler.Update` (`:90`) has no gate.** It always runs `HandleNonMovementInput` (`:96`): pressing `X` or `Return`, or pad A, calls `HandleButtonClick` which invokes `onClick` of `Data.CurrentObject`. When the handler was initialised, that is the first main-page button (probably Resume). If your mod page has a text input (typing "x") or uses Enter, you will click a pause button behind it. Prefix `PauseNavigationHandler.Update` (private, no args) and return `false` while your page is open. The main menu handler does not have this problem because of `ControlsEnabled`.
- Mouse clicks anywhere call `ChangeMenuStateIfNecessary` and mouse movement un-highlights the controller selection; harmless.
- Opening your own page from the pause menu: set `PauseMenu.instance.currentMenu = yourPageGameObject` (so that `Pause()`/back logic knows a sub-page is open), hide `MainAnimator` with `SetBool("MoveUp", true)` like the built-in pages do, and show yours.

Hooks: `PauseMenu.Start` (private) for building your button; `PauseMenu.Pause()` / `Resume()` / `PauseMenuOpen` (private) for open/close events; `PauseNavigationHandler.EnabledStateChanged` is a public C# event (`EventHandler<PauseStateChangedEventArgs>`) raised from `OnEnable/OnDisable`, a clean "pause screen shown/hidden" signal.

### 5.4 OptionsMenu

`OptionsMenu` (`OptionsMenu.cs`, ~1380 lines) is used by both menus (`bool mainMenu`) and is one big bag of serialized text/slider references with public setters bound from the scene: `setResolution(bool isLeft)`, `setScreenMode`, `setFOV(float)`, `SetBrightness`, `SetLanguage(bool isLeft)` (`:755`), `setMouseSensitivity(float)`, `SetToggleSprint()`, `setMaster/Sound/Music/Nasty(float)`, `applyDisplaySettings()`, `applyDisplaySettingsMono(MonoBehaviour)` (`:1196`, applies the language change and saves it via `DisplaySave.SaveDisplay("Language", ...)`), etc. `StartInit(MonoBehaviour)` (`:195`) runs once and restores everything from the save files.

Not designed to be extended. For mod settings, make a separate page and persist to your own file. If you want your page to feel like Options, clone an options row (the `OptionsButton` components) and parent them under your page.

### 5.5 In-game menu and others

The tabbed in-game menu (`GameUIController` + `UIPageButtons`, 7 tabs) is described in 3.1. `InventoryNavigationHandler` (large, ~1600 lines) drives it. Adding a tab is not recommended.

`LoadMenu` (`LoadMenu.cs`) is shared by main and pause menus (`LoadMenu.instance`, `LoadGameSlot()`, `SaveGameSlot()`, `GetSaveSlotInfo(...)`) and sets `MainMenu.Instance.currentMenu` for its warning dialogs. Hook `LoadGameSlot` / `SaveGameSlot` if the mod needs save/load events (see `SaveController` for the real save pipeline, outside this doc).

## 5. Developer console

### 6.1 Where it lives and how it opens

- `DeveloperConsole` (`DeveloperConsole.cs`) is a `MonoBehaviour` on its own canvas object. `Awake` (`:301`) makes it a singleton (`Instance`), detaches to root and `DontDestroyOnLoad`s it. It exists in the main menu and in gameplay.
- It is **not furniture or a world object**. The class named `DeveloperConsoleFurniture` (`DeveloperConsoleFurniture.cs`) is just a `List<Furniture> furnitures` with `GetFurniture(name)`, `AddAllFurniture(int)`, `FindFurniture(string)` that backs the `add_furniture*` commands (the `Furniture` here is the building-system prefab type).
- There is no build-flag gate. `Update` (`:337`) checks `ShouldToggleDevConsole` (`:356`): **F1 key-up**, or the controller chord **View held + Menu pressed** (either order), or, while open, **pad B**, or, while open on non-controller input, the **Pause Menu key** (Escape).
- `ToggleDevConsole()` (`:394`, public) opens; closing goes through `CloseDevConsole()` which starts the `Closelate` coroutine (next frame). Opening also does `Achievements.instance.globalDisable = true` and `GameController.instance?.ControlsDisabled(GameController.instance.gameObject, ShowCursor: true)`; closing re-enables achievements and calls `ControlsEnabled`. So achievements are disabled only while it is open (the `enable_achievements` command is redundant with closing).
- State: `bool consoleOpened` (public getter), `TMP_InputField InputField`. Other systems check `consoleOpened` to ignore keys: `MainMenuNavigationHandler.Update`, `GameController` (pause key), `BuildingSystem.cs:621`, `DeveloperMovement`.
- Up/Down arrows browse the last 100 commands (`BrowseOldCommands`, `:444`).
- The text area is a 150-line ring buffer (`_printedMessages`). Clicking a line copies it to the clipboard.
- The input field's submit event calls `ExecuteCommand(TMP_Text inputText)` (`:427`, public, wired in the scene). It trims, stores history, calls private `HandleInput(string)` then clears the field.

### 6.2 Parsing

`HandleInput(string input)` (`:459`, **private**) splits on `"`; even-index chunks are split on spaces (empties dropped), odd-index chunks are kept whole as one argument. So `add_note "My title"` gives two tokens, and unquoted multi-word args are re-joined by individual commands with `string.Join(" ", RemoveFirstIndex(args))`. Then a `switch (array[0])` on **case-sensitive** command names (`:467`); `default:` prints `'<input>' is not a valid debug menu command` and `Type "help" ...`.

Commands do their own argument checking; many parse with `Convert.ToInt32` and rethrow `FormatException` after printing. `HandleInput` has no try/catch, so on an exception `ExecuteCommand` never reaches `inputField.text = ""` and the exception is also echoed by the log mirror.

### 6.3 Command reference

Help text is `_validCommandExamples` (a 113-element `string[]` built in `Awake`, `:314`). The commands below marked (hidden) work but are missing from `help`.

**General**

| Command | Effect |
|---|---|
| `help` | `PrintValidCommands()` prints the help array |
| `god_mode` | Toggles `PlayerStats.instance.Godmode` |
| `kill` | Sets health to 0 and calls `PlayerStats.death(DamageTypeNames.Unspecified)` |
| `noclip` | Toggles `DeveloperMovement` debug flight (`DeveloperConsoleRoutines.NoClip :1737`); movement keys, `Up`/`Down` action names (not bound by default) or pad LB/RB for vertical |
| `unlock` | Unlocks the interactable you are looking at (`Door`, `DoorChangelevel`, `BreakableLock`, `Storage`) |
| `show_unity_console` | Toggles mirroring `Debug.Log` into the console (default on) |
| `test_unity_console` (hidden) | Emits one log, warning, error and exception |
| `show_credits` (hidden) | Starts the credits and closes the console |
| `enable_achievements` (hidden) | `Achievements.instance.globalDisable = false` |
| `difficulty get / list / info / set <name>` (hidden) | Current difficulty, list names, list names with details, change by name (`DifficultyController.ChangeDifficultyByName`) |

**Tasks**

| Command | Effect |
|---|---|
| `list_tracked_tasks` | Prints `ID, Title, Status` for every task in `TaskController.allTasks` |
| `list_task_objectives <taskId>` | Objectives with id/title/status |
| `set_task_status <taskId> <Active/Failed/Finished/Abandoned/AnyStatus>` | `TaskController.EditTaskStatus` |
| `set_objective_status <taskId> <objectiveId> <Abandoned/Active/Failed/Finished>` | `EditTaskObjective` then `FinishIfNoActiveObjectives` |
| `set_objective <task> <objective> <status>` | Same idea but looks up by name via `GetTaskAndObjective`, calls `EditObjective` |

**Items, notes, blueprints**

| Command | Effect |
|---|---|
| `add_item <id> [amount]` | `ItemDatabase.FetchItemByID`, then `ItemOperations.AddItem(item)` `amount` times. A missing id defaults to 0 |
| `find_item <name>` | `ItemDatabase.FindByString`, prints `ID, Title` |
| `add_task_item <name>` | Exact `TaskItem` asset name from `Resources/TaskItems`; `TaskItemsManager.AddTaskItem` |
| `list_task_items` | Names of those assets |
| `add_blueprint <name>` or `<addressable path ending .asset>` | By blueprint name (`FurnitureBlueprintController`) or by Addressables path (`TaskItem.GiveItem()`) |
| `list_all_blueprints` | Name and addressable path per blueprint (the header says "Notes:", a typo) |
| `add_note <title>`, `read_note <title>`, `list_notes` | `NoteInfo` assets from `Resources/Notes`; exact title match |
| `destroy_all_items` | Destroys every `CollectibleItem` in the scene |

**Money**

| Command | Effect |
|---|---|
| `add_money <RM\|OC> <amount>` | `Money.instance.AddMoney(amount, isRM)`; currency must be exactly `RM` or `OC` (case-sensitive) or nothing happens (only a `Debug.Log`) |
| `remove_money <RM\|OC> <amount>` | `Money.instance.RemoveMoney` |

**Locales**

| Command | Effect |
|---|---|
| `locale` / `locales` | Current locale / all available `Locale`s |
| `set_locale <prefix>` | First locale whose `LocaleName` starts with the argument; sets `LocalizationSettings.SelectedLocale` (does **not** save to `Display.json`) |
| `set_localization <key> <translation>` | Looks in the table named `"Test Table"` for the selected locale; only changes an existing key (`:213`). Prints "key does not exist" otherwise |
| `export_translations` | `ExportStringTables.instance.ExportAll()` writes one CSV per table to `<persistentDataPath>/Localization/` |
| `import_translations` | `ImportStringTables.instance.ImportAll()` |

**Scenes, time, weather**

| Command | Effect |
|---|---|
| `list_scenes` | `ReadSceneNames.Instance.scenes` |
| `change_scene <name>` | Exact name match; `SaveController.ChangeLevel` (saves/loads through the normal level-change path) |
| `new_scene <name>` | `LoadingScreen.instance.ChangeScene` directly |
| `change_time <h>` or `<h:mm>` | `TimeOfDayAzure.instance.SetTimeline(hours)`, 0..24 |
| `set_ingame_time_scale <x>` | `TimeOfDayAzure.timeSpeedOverrideFactor` (not Unity `timeScale`), must be >= 0 |
| `list_weathers`, `change_weather <name or index>`, `thunder_storm` | `WeatherManager` |

**Stats**: `player_stats <sub> ...` (`DeveloperConsoleInputHandler.HandlePlayerStatsCommand :126`)
- `get` prints health, regen, bleeding and mushroom stats. `reset_stats`, `clear_needs`, `disable_progress` (toggle `StatsProgressDisabled`).
- `skill <skillName> <amount>` increases a `PlayerStats.PlayerSkillType` (enum names: Lockpicking, Drinking, Cooking, Mashing, Brewing, Distilling, Farming, Malting, Manufacturing, Paperwork, Chemistry, Carpentry, ...). A bad name prints the valid list.
- `<stat> add|subtract|set <amount>` where `<stat>` is one of: `hunger bladder depression overheat thirst tiredness hygiene dose alcohol_addiction alcohol_drunk alcohol_need gambling_addiction gambling_need gambling_satisfaction depression_target hunger_rate mushroom_addiction mushroom_need mushroom_high mushrooms_used overheat_cooldown rest_rate smoking_addiction smoking_need thirst_rate alcohol_absorption_rate alcohol_addiction_change alcohol_burn_rate alcohol_in_blood alcohol_need_base_rate depression_change_rate health health_regeneration_rate health_to_regenerate mushroom_absorption_rate mushroom_addiction_change mushroom_decrease_rate mushroom_need_base_rate sleep_time_speed smoking_addiction_change smoking_need_base_rate thirst_starvation_damage hunger_rate_bowel_factor smv_progression thirst_rate_bladder_factor withdrawal_symptom_dmg sleep_hunger_thirst_rate_factor smv_progression_rate expectation_level`.
  The amount goes through `Convert.ToInt32`, so only integers are accepted. `DeveloperConsoleRoutines` has `...MushroomHighOrange` / `...MushroomsUsedOrange` variants, but no console command reaches them.

**World/tenement**

| Command | Effect |
|---|---|
| `add_crime <n>` / `reset_crime` | `Crime.instance.CommitCrime(null, n)` / `ClearCrime()` |
| `ignore_upgrade_prices_and_items` | Toggles `TenementController.ignoreUpgradePricesAndItems` |
| `complete_upgrades_instantly` | Toggles `completeUpgradesInstantly` and finishes in-progress upgrades now |
| `add_furniture <name> [amount] [skin]` | Name looked up case-insensitively in `DeveloperConsoleFurniture.furnitures`; `BuildingSystem.instance.AddFurniture(furniture, amount, meta)`; trailing numbers are parsed as amount/skin |
| `add_furniture_all <amount>` | Every categorised furniture and every skin |
| `find_furniture <text>` | Substring search, prints skins |
| `reset_train_position` | Resets the `TrackTrain` you are looking at |
| `reset_all_trains_positions`, `break_trains` | `TrainController` |

### 6.4 Registering your own commands

There is **no registry**. Commands are a hard-coded `switch` in a private method. Options:

1. **Prefix `HandleInput` (recommended).**

```csharp
[HarmonyPatch(typeof(DeveloperConsole), "HandleInput")]   // private void HandleInput(string input)
static class ConsoleCommands {
    static bool Prefix(string input) {
        var args = Tokenize(input);              // same quote rule as the game
        if (args.Length == 0) return true;
        if (!ModCommands.TryGet(args[0], out var cmd)) return true;   // fall through to vanilla
        try { cmd(args); } catch (Exception e) { DeveloperConsole.Instance.Print("<#ED0000>" + e.Message + "</color>"); }
        return false;                            // skip vanilla "not a valid command"
    }
}
```

   Copy the game's tokenizer: `input.Split('"')`, odd-index chunks whole, even-index chunks `Split(' ', RemoveEmptyEntries)`. Keep command matching case-sensitive to match vanilla, or lowercase it yourself. Catching exceptions fixes the "input not cleared" behaviour for your commands.
   Because the prefix runs before the vanilla switch, a mod command can also shadow a vanilla one.
2. **Help text.** Either Postfix `PrintValidCommands()` (public, no args, `:703`) and call `DeveloperConsole.Instance.Print(...)` with your lines, or after `Awake` replace the private field `_validCommandExamples` (a `string[]`, entries beginning with `\n` are section headers, e.g. `"\nTRAINS"`) via `AccessTools.Field`.
3. **Output.** `DeveloperConsole.Instance.Print(string)` (public). TMP rich text works (`<#FFDA00>warn</color>`). Each call appends one line to the 150-line ring.
4. **Run a command from code.** Either call `HandleInput` via reflection, or the public `ExecuteCommand(TMP_Text)` (needs a `TMP_Text` carrying the command; also records history and clears the input field, so it is a poor fit).
5. **Open/close from a mod.** `DeveloperConsole.Instance.ToggleDevConsole()`. Re-binding the key means patching the private `ShouldToggleDevConsole()` (`:356`) to return true on your key. Note the vanilla check reads `Input.GetKeyUp(KeyCode.F1)` raw, not via `InputManager`.
6. **Log mirror.** `DeveloperConsole.OnEnable` subscribes `Application.logMessageReceived` (`:811`), so every `Debug.Log/LogWarning/LogError/LogException` from any assembly, including a mod, appears in the console as `Log:`, `Warning:` (yellow), `Error:`, `Exception:` (red) while `show_unity_console` is on. It does not see BepInEx's own logger unless that logs through `UnityEngine.Debug`. The game itself logs a lot, so expect noise.

Console gotchas:
- The console input field takes focus, but the mod must not rely on `GameController.keysDisabled` as proof the console is open; use `DeveloperConsole.Instance.consoleOpened`.
- The pause menu handler ignores the console (section 4.3): typing `x` or pressing Enter while paused clicks the highlighted pause button. The F1 console works while paused (the console `Update` runs at `timeScale 0`), but avoid typing there on the pause screen.
- `Print` is not thread safe; call from the main thread.

## 6. Localization

### 7.1 What exists

- `Unity.Localization` (UnityEngine.Localization) with Addressables-backed tables. Locale/table assets are in `StreamingAssets/aa/StandaloneWindows64/`: `localization-locales_assets_all.bundle`, `localization-assets-shared_assets_all.bundle` (shared table data) and one `localization-string-tables-<language>(<code>)_assets_all.bundle` per locale.
- 18 locales: Chinese Simplified (`zh-hans`), Czech (`cs`), Danish (`da`), English (`en`), Finnish (`fi`), French (`fr`), German (`de`), Italian (`it`), Japanese (`ja`), Korean (`ko`), Norwegian (`no`), Polish (`pl`), Portuguese (`pt`), Russian (`ru`), Slovak (`sk`), Spanish (`es`), Swedish (`sv`), Ukrainian (`uk`).
- The English table bundle is only about 165 KB, which tells you the localized surface is small. Table names seen in the bundle strings include `Menu`, `Items` (hard-coded by `ImportStringTables`), `Task`, `ToolTip`, `UseText` and a `Test Table` (hard-coded by `set_localization`). The list is partial because the bundle is compressed; enumerate at runtime with `LocalizationSettings.StringDatabase.GetAllTables()` (as `ExportStringTables.ExportAll` does).
- Very little of the UI text goes through it. Code-built strings are English literals: notifications, `UseText`, option values ("Enabled"/"Disabled"), console output, hint text. Scene text that is localized does it through Unity's localize components, which are serialized in scenes, not visible in `Assembly-CSharp`.
- `Items.json`, `Recipes.json`, `Characters*.json` in `StreamingAssets` are the item/recipe/character databases; whether titles there are looked up in the `Items` table is unverified.

Code that touches localization (only five files): `OptionsMenu.cs`, `DeveloperConsoleRoutines.cs`, `ExportStringTables.cs`, `ImportStringTables.cs`, `MenuLocalization.cs`.

- `MenuLocalization` (`MenuLocalization.cs`) is **dead code**: `OnEnable/OnDisable` are empty, `Localize(Locale)` is private and never subscribed. It would have swapped each child `TMP_Text.text` for a `"Menu"` table lookup keyed by the English text and cached to `<persistentDataPath>/SavedLocalizationDictionaryData/PersistentDictionary-Menu.json`.
- Language selection: `OptionsMenu.SetLanguage(bool)` picks from `LocalizationSettings.AvailableLocales.Locales`; `applyDisplaySettingsMono` assigns `LocalizationSettings.SelectedLocale` and saves `Display.json` `language` (the `LocaleName`). On startup `StartInit` restores it by matching `LocaleName` (`OptionsMenu.cs:340`).

### 7.2 Export/import tooling

- `ExportStringTables` (`instance`): `Export(string tableName)`, `Export(table, localeCodeFirst, localeCodeSecond)`, `ExportAll()`, `ExportAll(codeA, codeB)`. Writes `<persistentDataPath>/Localization/<table>.csv`, columns `Key,Id,<one column per locale>`, via `LocalizationSettings.StringDatabase.GetAllTables().WaitForCompletion()`.
- `ImportStringTables` (`instance`): `ImportAll()` reads every file in the `Localization` folder, `Import(fileName)` parses the header (`...(xx)` locale codes in columns 3 and 4) and calls `table.AddEntry(key, value)` on the **`Items`** table only (hard-coded) with a naive `Split(',')`, so any value containing a comma breaks. It writes into the in-memory table; nothing is saved back to the bundles, so imports are lost on restart.

### 7.3 Adding or overriding strings from a mod

Runtime table edits survive until the game quits, so a mod re-applies them at startup and after a locale change:

```csharp
var table = LocalizationSettings.StringDatabase.GetTable("Menu", locale);   // StringTable
table.AddEntry("mod.key", "text");     // adds, or replaces an existing key's value for this locale
```

Notes:
- `AddEntry(string key, string value)` creates the shared key if missing and sets the locale's value. Do it once per locale you ship, and re-run it when `LocalizationSettings.SelectedLocaleChanged` fires.
- To override game text that already goes through a table, replace that key's value after tables load (wait for `LocalizationSettings.InitializationOperation`).
- To override text the game builds in code (most of it), there is nothing to hook in the localization system. Harmony-postfix the producing method or the `TMP_Text.text` setter and substitute. For your own UI, bypass Unity Localization and keep a small dictionary keyed by `LocalizationSettings.SelectedLocale.Identifier.Code`, since your strings are not in the game's tables.
- Adding a brand-new language is possible through `LocalizationSettings.AvailableLocales.AddLocale(...)` plus string tables created at runtime, but fonts for the new script would also need TMP fallbacks; not worth it.

## 7. Harmony hook table

All methods are on the types in `Assembly-CSharp` (global namespace). `private` means target by name string.

| Goal | Target | Notes |
|---|---|---|
| Mod startup, early | `InputManager.Awake` (private), `DeveloperConsole.Awake` (private) | Both are boot-time `DontDestroyOnLoad` singletons. A reliable "game started" marker is `InputManager.instance != null`. |
| Main menu ready | `MainMenu.Start()` private; `MainMenuNavigationHandler.InitializeSlotArrays()` is `protected override IEnumerator` (iterator, avoid) | Add buttons in a postfix on `MainMenu.Start`. |
| Main menu nav of added button | `MainMenuNavigationHelper.GetButtonAbove/GetButtonBelow/GetButtonOnTheLeft/GetButtonOnTheRight` (`protected override UnityEngine.UI.Button`) | Prefix with `ref Button __result`, return false to override. |
| Suspend main menu input | `MainMenuNavigationHandler.ControlsEnabled { get; set; }` public | No patch needed. |
| Pause menu opened / closed | `PauseMenu.Pause()` public, `PauseMenu.PauseMenuOpen()` private, `PauseMenu.Resume()` public, `PauseNavigationHandler.EnabledStateChanged` event | Prefer the event for a no-patch hook. |
| Add pause menu button | `PauseMenu.Start()` private (postfix) | Instantiate a clone as a direct child of the main page, see 4.3. |
| Pause back handling for mod page | `PauseNavigationHandler.HandleBackButtonClick()` public | Prefix, return false when your page is open. |
| Stop pause handler reacting to Enter/X | `PauseNavigationHandler.Update()` private | Prefix, return false while your page is open. |
| Lock game input | `GameController.ControlsDisabled(GameObject, bool, bool, bool, bool, bool, Transform, bool, bool)` / `ControlsEnabled(GameObject, bool)` | Call, do not patch. Patch only to observe. |
| Observe any menu open | `GameUIController.ShowGameMenu(...)`, `HideGameMenu(bool)`, static event `GameUIController.ExitPanels` | |
| Console commands | `DeveloperConsole.HandleInput(string)` private | Prefix, section 5.4. |
| Console help | `DeveloperConsole.PrintValidCommands()` public | Postfix. |
| Console toggle key | `DeveloperConsole.ShouldToggleDevConsole()` private bool | Postfix `ref bool __result`. |
| Toast | `Notifications.CreateNotification(string, string, bool)` | Call. Postfix to observe all vanilla toasts. |
| Hints | `HintOverlay.Show(...)` static | Call. |
| React to input device change | `InputManager.InputTypeChanged` event | |
| Observe/alter a game action | `InputManager.GetKeyDown(string)` / `GetKey` / `GetKeyUp` | Postfix with `string key, ref bool __result`. These are hot paths called many times a frame by many systems, so keep the postfix cheap. |
| Keybinding schema extension | `KeyBindings.GetKey` (private, switch), `GetControllerKey` (private), `ChangeKey`, `KeyBindingsSave.SaveKeyBindings/GetKeyBindings` | Not worth it; use mod-owned bindings. |
| Language change | `OptionsMenu.applyDisplaySettingsMono(MonoBehaviour)`, `LocalizationSettings.SelectedLocaleChanged` | |
| Settings applied | `OptionsMenu.StartInit(MonoBehaviour)` | Runs once, after saved settings are loaded. |

## 8. Gotchas not covered above

1. `GameController.instance` is null in the main menu, and `ControlsDisabled` also dereferences `ThirdPersonCameraCollision.instance`, `ThirdPersonCameraController.instance`, `OpenSewerCharacterController.instance` and `MouseBlocker.instance`. Call it only in a loaded gameplay scene.
2. Locks are keyed by GameObject and stay in the lists until `ControlsEnabled` is called with the same object. Release in `OnDisable` and `OnDestroy`.
3. Selectable types in `UINavigationBase.SelectObject` are a closed set: `Button`, `OptionsButton`, `TMP_InputField`, `LoadMenuButton`. A `Toggle`, `Dropdown` or `Slider` in a navigated page throws when selected. A standalone overlay outside the handlers has no such limit.
4. `MainMenu.currentMenu` and `PauseNavigationHandler.Data.CurrentWindow` feed switches that throw on unknown values. Never leave them on a value they do not handle.
5. Rich text is on for notification, hint and console text. Escape user-provided strings.
6. Pause sets `Time.timeScale = 0`; `SavingUI.ShowSavingUI` also sets 0 and `GameIsPaused`. Use `unscaledDeltaTime` and `WaitForSecondsRealtime` in mod UI.
7. Under Proton, window focus loss can drop key-up events. If a mod UI latches state on a held key, also clear it in `OnApplicationFocus(false)`.
