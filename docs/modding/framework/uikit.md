# ObenUIKit

Windows and controls that look like the game's own options pages, and the mod menu. Source: `src/ObenUIKit/`. Design notes: `docs/research/uikit-design.md`.

Add `"ObenUIKit": "0.1.0"` to your mod.json dependencies and a project reference to `src/ObenUIKit/ObenUIKit.csproj`.

## How it looks native

The kit doesn't clone the game's widgets (they carry scene references, sound sources and save ids). It reads the game's look off its own menus and builds its controls with those values:

- at the main menu, from the menu's Options button and the options sign (language row, FOV slider);
- in a level, from the pause menu's Resume button and its options page;
- for text fields, from the developer console's input field (box colour, selection colour, caret).

That gives the font (Erika Ormig) and its material, text colours for normal, hover, pressed and disabled, the hover and click sounds and their mixer group, the option row sprite and its dark red hover bar, the arrow font and the slider handle. The look is taken again on every main menu and every level, and `UiKit.Theme` hands out the one that fits where the player is.

The game renders in linear colour space. Colours you set yourself come out brighter than the same numbers in an image editor: a grey meant to look 25% bright is about 0.05.

## Opening a window

```csharp
using ObenUIKit;

UiWindow window = UiKit.Open(this, "mymod.settings", "My Mod", ui =>
{
    ui.Label("Settings take effect right away.", muted: true);
    ui.Toggle("God mode", _config.Value.God, on => { _config.Value.God = on; _config.Save(); });
    ui.Cycle("Difficulty", new[] { "Easy", "Normal", "Hard" }, _config.Value.Level, i => SetLevel(i));
    ui.Slider("Move speed", 0.5f, 4f, _config.Value.Speed, v => SetSpeed(v), "0.0x");
    ui.Header("Status");
    ui.Live(() => $"Money: {Wallet.Coins}");
    ui.Row(r => { r.Button("Apply", Apply); r.Button("Reset", Reset); });
});
```

- The first argument is the owner (your mod, or one of its features). The window closes when the owner unloads.
- The id keeps a window single: opening the same id again returns the open window.
- Without a `footer` the window gets a Back button. Pass `footer: f => { f.Button(...); }` for your own.
- Esc (or the pad's B) closes the top window. It closes a frame later, so the same key press doesn't also reach the game. While a text field or a key binding row has the keyboard, Esc goes to it instead and the window stays.
- The window locks game input (movement, mouse look, hotkeys) and shows the cursor while open. In a level that goes through the game's own `GameController` lock; at the main menu the menu's keyboard navigation is paused.
- Over the pause menu, the pause menu's keyboard handler and its Esc toggle are held off until the window closes.
- Windows close on their own when the player changes level.

A `height` argument (default 0, the full-height column) makes a centred box instead, which is what `UiKit.Confirm` uses.

`UiWindow` has `Title`, `IsOpen`, `Close()`, `Rebuild()` (build the body again with current values), `CloseOnEscape` and a `Closed` event.

## Controls

| Method | Looks like | Returns |
| --- | --- | --- |
| `Header(text)` | the grey bar above each setting in the game's options | `LabelHandle` |
| `Label(text, muted, align)` | plain wrapped text; muted is greyer and smaller | `LabelHandle` |
| `Live(() => text, muted)` | a label read again four times a second while the window is open | `LabelHandle` |
| `Button(text, onClick)` | underlined text button, like Apply and Back | `ButtonHandle` (`Enabled`, `Text`) |
| `Cycle(label, options, index, onChange)` | a header and a `< value >` row; arrows step, the row lights up dark red under the cursor | `CycleHandle` (`Index`, `Set`, `SetOptions`) |
| `Toggle(label, value, onChange)` | a cycle between Off and On; the game has no checkboxes | `ToggleHandle` (`Value`, `Set`) |
| `Slider(label, min, max, value, onChange, format, wholeNumbers)` | a header and a slider with its value on the left | `SliderHandle` (`Value`, `Set`) |
| `TextField(label, value, onChange, onEnd)` | a header and a box styled like the developer console's input field | `FieldHandle` (`Text`, `Set`, `Enabled`, `Focus()`) |
| `KeyBind(label, key, onChange)` | a header and a row showing the key; click or Enter, then press the new key | `KeyBindHandle` (`Key`, `Set`) |
| `Row(r => ...)` | controls side by side, centred | |
| `Space(height)` | empty space | |

`Set` on a handle changes what's shown without calling your callback. Callbacks are guarded: an exception is logged with the window's id and the window stays open.

### Text fields and key bindings

`onChange` runs on every edit and `onEnd` when the player leaves the field (Enter, Esc or a click elsewhere), both with the current text. Esc leaves the field without undoing what was typed, and doesn't close the window. Game keys stay locked while typing because the open window already locks them; the kit never uses `GameController`'s `writeLock` (it closes the notes panel on Esc).

A key binding row waits for the next key after a click or Enter. Esc, the pad's B or a click elsewhere cancels. Mouse buttons and pad buttons can't be bound, modifier keys can. The row only reports the key; saving it and acting on it is up to your mod.

## Keyboard and controller

Every kit window can be used without the mouse. Controls register themselves as they are built, so the order is the order you wrote them, body first and then the footer.

| Input | Does |
| --- | --- |
| Up / Down, W / S, d-pad or left stick | moves the highlight to the previous or next control, wrapping around; held, it repeats after 0.3 s and then every 0.15 s like the game's menus |
| Left / Right, A / D, d-pad or left stick | steps a cycle or toggle, moves a slider by a twentieth of its range (a whole number for whole-number sliders), or moves along a row of buttons (a `Row` or the footer) |
| Enter, keypad Enter, pad A | presses a button, steps a cycle to the next option, puts the cursor in a text field, starts a key binding |
| Esc, pad B | closes the top window |

The highlight is the control's mouse hover look. Moving the mouse over a control moves the highlight there, so only one control is ever lit. The body scrolls to keep the highlighted control in view. Disabled buttons are skipped. When a window rebuilds (a mod menu page change, `Rebuild()`), the highlight stays at the same place in the list.

Navigation reads the keyboard directly, and only runs while a window is open and no text field or key binding has the keyboard. W/S/A/D and the arrow keys are fixed; they don't follow the game's key bindings.

## Questions and notices

```csharp
UiKit.Confirm(this, "Reset everything?", "All settings go back to their defaults.", Reset);
UiKit.Confirm(this, "Delete the save?", "This can't be undone.", Delete, yes: "Delete", no: "Keep");

UiKit.Toast("Settings saved");
UiKit.Toast("The server is full", title: "Co-op");
```

`Confirm` opens a small modal window above any other kit window. `onYes` runs after the window closes and its exceptions are logged. No, Esc and the pad's B close it without calling anything. It returns the `UiWindow`.

`Toast` goes through the game's own notification panel (`Notifications.instance.CreateNotification`, the title is the small heading) when you're in a level, and shows a small notice in the top right corner of the kit's overlay at the main menu, which fades after a few seconds.

## The mod menu

MODS is in the main menu (it takes over the menu's unused "???" button) and in the pause menu (above "Quit to Menu"). Add your own page:

```csharp
using ObenUIKit.Menu;

ModMenu.AddPage(this, new ModPage("My Mod", ui =>
{
    ui.Toggle("Show the overlay", _config.Value.Overlay, on => { _config.Value.Overlay = on; _config.Save(); });
}) { Order = 50 });
```

Pages are sorted by `Order`, then title, and chosen with a `< page >` row at the top. A page goes away when its mod unloads. `ModMenu.Open(pageTitle)` opens the menu, on a page if given; `ModMenu.Close()` and `ModMenu.IsOpen` do what they say.

The built-in pages:

- **Mods** lists every installed mod with its version, description and state, whether it loads when the game starts, and an Off/On switch for each of its features (`ModFeatures`).
- **Settings** shows the config files mods registered (below).
- **What's New** shows the release notes from `changelog.json`, newest release first, grouped by mod and by Added, Changed, Fixed and Removed. See `docs/modding/changelog.md` for where the file comes from.
- **Errors** lists this session's problems from `ErrorReport`: who reported each one, its title, how often and when it last happened, the scene, and the first lines of its details. Clear empties the report, Copy report puts it on the clipboard. Bug reports should attach `ObenLoader/logs/errors.log`, which holds the same list with the game and mod versions.

## Settings pages

Register a config file and the Settings page builds the controls for it:

```csharp
using ObenUIKit.Menu.Settings;

public sealed class MyConfig
{
    [Setting("Move speed", Description = "How fast the player moves.", Min = 0.5f, Max = 4f, Step = 0.05f)]
    public float Speed = 1f;

    [Setting(1, 20)]
    public int MaxMarkers = 5;

    public bool ShowOverlay = true;
    public Difficulty Level = Difficulty.Normal;
    public int Retries = 3;

    [Setting(Hidden = true)]
    public string LastSeenVersion = "";
}

// In OnLoad:
var config = LoadConfig<MyConfig>();
ModSettings.Register(this, config);            // titled with the mod's name
ModSettings.Register(this, other, "Overlay");  // or with your own title
```

Every public instance field becomes a control, in declaration order:

| Field | Control |
| --- | --- |
| `bool` | Off/On switch |
| enum (not `[Flags]`) | cycle through the names, written as words ("PauseMenu" shows as "Pause menu") |
| `int`, `long`, `float`, `double` with bounds in `[Setting]` | slider; whole numbers move by one, decimals are rounded to `Step` (or to a step picked from the size of the range) |
| `int`, `long`, `float`, `double` without bounds | cycle through round values (0, 1, 2, 5, 10, 20, ...) and the current value if it isn't one of them |
| `string` | a read-only line for now; edit it in the config file |
| anything else | a note to edit it in the config file |

`[Setting]` is optional. It carries `label` (default: the field name with spaces), `Description` (a muted line under the control; `System.ComponentModel.DescriptionAttribute` works too), `Min` and `Max` (both are needed for a slider; `[Setting(min, max)]` is the short form), `Step` and `Hidden`. Fields that are `readonly` or `const` are skipped.

Edits are written with `config.Save()` right away, which also raises the config's `Changed` event, so a mod can apply values as they change. Slider drags are written once they settle (about a quarter of a second), when the player switches page or mod, or when the menu closes. The page reads the config's current `Value` each time it is built, so `config.Reload()` is picked up the next time the page is shown. With several registered configs a `Mod` cycle at the top picks the one to edit. A registration goes away when the mod that made it unloads. The kit registers its own config the same way (the option to show What's New after updates).

The list of fields is worked out by reflection once per settings class and cached.

## Known limitations

- No lists with many rows, tabs or tooltips yet (planned, see the design notes).
- The window always uses the centred column layout of the pause menu, or the small box of a dialog.
- A text field can't be typed into with a controller (there is no on-screen keyboard); the rest of a window works with one.
- Keyboard navigation moves in one column, in build order; it doesn't know about left and right placement beyond `Row` and the footer.
- String settings show on the Settings page but are edited in the config file for now; the page doesn't use text fields yet.
