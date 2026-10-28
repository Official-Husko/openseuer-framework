# ObenUIKit

Windows and controls that look like the game's own options pages, and the mod menu. Source: `src/ObenUIKit/`. Design notes: `docs/research/uikit-design.md`.

Add `"ObenUIKit": "0.1.0"` to your mod.json dependencies and a project reference to `src/ObenUIKit/ObenUIKit.csproj`.

## How it looks native

The kit doesn't clone the game's widgets (they carry scene references, sound sources and save ids). It reads the game's look off its own menus and builds its controls with those values:

- at the main menu, from the menu's Options button and the options sign (language row, FOV slider);
- in a level, from the pause menu's Resume button and its options page.

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
- Esc closes the top window. It closes a frame later, so the same key press doesn't also reach the game.
- The window locks game input (movement, mouse look, hotkeys) and shows the cursor while open. In a level that goes through the game's own `GameController` lock; at the main menu the menu's keyboard navigation is paused.
- Over the pause menu, the pause menu's keyboard handler and its Esc toggle are held off until the window closes.
- Windows close on their own when the player changes level.

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
| `Row(r => ...)` | controls side by side, centred | |
| `Space(height)` | empty space | |

`Set` on a handle changes what's shown without calling your callback. Callbacks are guarded: an exception is logged with the window's id and the window stays open.

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

The built-in Mods page lists every installed mod with its version, description and state, whether it loads when the game starts, and an Off/On switch for each of its features (`ModFeatures`).

## Known limitations

- Keyboard and controller navigation inside kit windows isn't there yet; use the mouse. Esc works.
- No text fields, key binding rows, lists with many rows, tabs, tooltips or confirm dialogs yet (planned, see the design notes).
- The window always uses the centred column layout of the pause menu.
- Notifications still go through the game directly (`Notifications.instance.CreateNotification`); there's no kit wrapper yet.
