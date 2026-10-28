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
- **Hotkeys** lists every mod's registered hotkeys with controls to rebind them (see Hotkeys below).
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

## HUD hints, subtitles and bars

`Hud` puts short things on the game's HUD. Each belongs to the mod (or feature) that adds it and goes when that unloads.

```csharp
using ObenUIKit;

Hud.Hint(this, "Press {key:Interact} to open the crate.", seconds: 8f);
Hud.Hint(this, "Rain is coming.", title: "Weather");
Hud.Subtitle("Somebody is following you.", seconds: 4f);

HudBar bar = Hud.Bar(this, "Stamina", () => Stamina.Fraction);
bar.Color = Color.cyan;   // optional; the theme's slider colour otherwise
bar.Remove();
```

- **Hints** go through the game's own `HintOverlay` side panel in levels, so they look and animate like its scene hints, and `{key:Action Name}` becomes the player's current key (`HintContent.ResolveKeys`). Our entry goes above the scene's own hints while it shows, and theirs come back after. The player's Hints key and setting still apply: with hints switched off nothing shows unless you pass `force: true`. Where the game has no hint panel (the main menu) the kit draws an equivalent box on the right edge from the theme, and unresolved `{key:...}` shows as the plain action name.
- Hints show one at a time in the order they were added, each for `seconds` (0.5 to 120, default 6) once it is up. Asking again for a hint with the same owner, title and text extends the one already waiting instead of queueing a copy, so calling `Hud.Hint` from an event that fires often is safe. At most six wait; the oldest waiting one is dropped first. `HudHint.Hide()` removes one early.
- **Subtitle** is one line near the bottom of the screen on a dark strip, in the game's font. A new line replaces the one showing; it fades during its last half second. It has no owner because it only lives for a few seconds.
- **Bar** is a small labelled bar at the top left, track and fill in the theme's slider colours. The function returns 0 to 1 and is read five times a second (the bar glides between reads, with no allocations per frame); if it throws, the bar is logged and removed. Up to six bars; `Hud.Bar` returns null beyond that. Bars only show in a level.
- Nothing shows while a kit window or the pause menu is open, while a save loads, or while the game's own HUD is hidden (the hide-HUD key, inventory and other full menus). Hint and subtitle timers wait until it is back. The HUD pieces sit under the kit's windows.

The code is in `Hud/`: `HintQueue` (plain bookkeeping), `NativeHints` (the `HintOverlay` bridge), `HintPanel`, `SubtitleLine`, `BarStack`, with `HudRuntime` tying them to the kit's lifetime. The per-frame component is only enabled while something is showing.

## Hotkeys

`Hotkeys` is a registry of mod hotkeys the player can rebind, so mods don't each read `Input.GetKeyDown` and each invent a settings screen.

```csharp
using ObenUIKit;

Hotkeys.Register(this, "open", "Open the cheat menu", KeyCode.Insert, Open);
Hotkeys.Register(this, "heal", "Heal", KeyCode.H, Heal, KeyModifiers.Ctrl);
Hotkeys.Register(this, "close", "Close the menu", KeyCode.Insert, Close, scope: HotkeyScope.InGame | HotkeyScope.Windows);
```

- The owner is a mod or one of its features. The id is stored in `hotkeys.json` in the kit's folder as `mod id/hotkey id`, so keep it stable. Only choices that differ from the default are written. A hotkey goes away when its owner unloads; registering the same id twice is an error.
- `KeyModifiers` is Ctrl, Shift and Alt (left or right). A chord fires when its key goes down with its modifiers held; extra modifiers don't matter, except that a more specific chord on the same key wins (Ctrl+K held means plain K doesn't fire).
- `Hotkey` has `Chord`, `Default`, `Rebind(chord)`, `Reset()` and a `Changed` event.
- One check per frame covers every hotkey. It returns at once unless some key went down, and only then looks at the hotkeys' keys and the gate. It allocates nothing. `onPressed` runs inside a try/catch that logs the mod's id.
- The gate is what the Cheat Menu uses to open: no firing while a text field or key binding row has the keyboard, while the game waits for a key in its own key bindings page, in the F1 console, with a menu, shop, dialogue, or the pause menu open, or while the game's controls are locked. A kit window blocks it too, unless the hotkey's `HotkeyScope` includes `Windows` (a hotkey that closes its own window). `HotkeyScope.MainMenu` allows it at the main menu. The default is `InGame`.
- **Conflicts.** `Hotkeys.ConflictOf(hotkey)` says what a hotkey shares its key with: another hotkey that fires with it, or a key of the game's (the player's current bindings from `KeyBindings`, so rebinding in the game is noticed, plus the keys the game reads directly: F1, F11, F12, 1 to 4, Esc, Enter). Modifiers are ignored for the game's keys. A clash at registration is logged; nothing is blocked.
- The **Hotkeys** page of the mod menu lists every mod's hotkeys grouped by mod: a key binding row, a cycle for Ctrl, Shift and Alt (a modifier can't be picked by the key row because pressing it ends the capture), the chord now with its default and any clash, and Reset and Unbind buttons. Reset all asks first. The list updates as mods load and unload.

## Known limitations

- No lists with many rows, tabs or tooltips yet (planned, see the design notes).
- HUD bars and the fallback hint panel have fixed places (top left, right edge); the game's own HUD layout isn't read.
- A hotkey can only be a keyboard key with Ctrl, Shift or Alt; mouse and pad buttons can't be bound, same as the key binding row.
- The window always uses the centred column layout of the pause menu, or the small box of a dialog.
- A text field can't be typed into with a controller (there is no on-screen keyboard); the rest of a window works with one.
- Keyboard navigation moves in one column, in build order; it doesn't know about left and right placement beyond `Row` and the footer.
- String settings show on the Settings page but are edited in the config file for now; the page doesn't use text fields yet.
