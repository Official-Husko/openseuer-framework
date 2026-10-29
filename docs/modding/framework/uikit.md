# ObenUIKit

Windows and controls that look like the game's own options pages, and the mod menu. Source: `src/ObenUIKit/`. Design notes: `docs/research/uikit-design.md`.

Add `"ObenUIKit": "0.1.0"` to your mod.json dependencies and a project reference to `src/ObenUIKit/ObenUIKit.csproj`.

To see every control at once, open the demo window: `oben call ui.demo` (or `ObenUIKit.Dev.KitDemo.Open()` from code). Its tabs show the choices, long lists, a model preview, the window kinds and the HUD pieces; its source in `src/ObenUIKit/Dev/` doubles as example code.

What the player's screen is busy with (pause menu, menus, typing, mod windows) and keeping keys away from the game are in the framework: see [ui-state.md](ui-state.md).

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

`UiWindow` has `Title`, `IsOpen`, `Close()`, `Rebuild()` (build the body again with current values), `CloseOnEscape`, `CloseReason`, `Dock`, `LocksInput` and a `Closed` event. `UiKit.Find(id)` returns an open window by id.

### Docked, floating and display-only windows

Pass a `WindowOptions` for anything beyond the centred column:

```csharp
UiKit.Open(this, "mymod.tracker", "Tracker", ui => ui.Live(() => Status()), new WindowOptions
{
    Dock = WindowDock.Right,   // Center (default), Left, Right, Floating
    Width = 520f,
    LockInput = false,         // display-only: the player keeps playing, no cursor, no Esc
});

UiKit.Open(this, "mymod.notes", "Notes", Build, new WindowOptions
{
    Dock = WindowDock.Floating,
    Width = 600f, Height = 500f,
    Position = new Vector2(-500f, 100f),   // the window's centre, from the screen's centre, in 1920x1080 units
    CloseOnError = true,
});
```

| Option | Default | Does |
| --- | --- | --- |
| `Dock` | `Center` | `Center` is the pause menu's column; `Left` and `Right` are columns along the screen edge; `Floating` is a box the player drags by its title (clicking it brings it to the front) |
| `Width`, `Height` | 760, 0 | in 1920x1080 units; height 0 is the full screen height (640 for a floating window) |
| `Position` | centre | floating windows: where the centre goes |
| `Footer` | Back button | builds the bottom bar |
| `LockInput` | true | off makes a display-only panel: game keys, mouse look and Esc stay with the game, there's no keyboard navigation, and the HUD and hotkeys don't step aside for it. In a level the cursor stays hidden, so it can't be clicked; at the main menu it can |
| `Dim` | centre only | darken the scene behind; a dimmed window also stops clicks reaching the game |
| `CloseOnEscape` | true | |
| `CloseOnError` | false | close the window as soon as one of its callbacks (a control, a `Live` text, the build itself) throws. The error is logged either way |
| `KeepOnLevelChange` | false | stay open through a level change |

### A window is a lifetime scope

`UiWindow` is an `ITracker`: anything passed to `window.Track(disposable)`, and anything subscribed or registered with the window as its owner, is disposed when the window closes, whatever the reason (Back, Esc, the owner unloading, a level change, an error).

```csharp
var window = UiKit.Open(this, "mymod.watch", "Watch", ui => ...);
GameClock.MinutePassed.Subscribe(window, OnMinute);    // unsubscribed when the window closes
window.Track(InputCapture.Key(this, KeyCode.G));       // the game gets G back when it closes
window.Closed += () => Log.Info("closed: " + window.CloseReason);
```

`CloseReason` is one of `Requested`, `Escape`, `OwnerUnloaded`, `LevelChange`, `Error` or `KitUnloaded`. `window.Run(what, action)` runs a callback of yours with the same guard the controls use.

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
| `Segmented(label, options, index, onChange)` | a header and a row with every option side by side; the picked one is lit and underlined | `ChoiceHandle` (`Index`, `Set`) |
| `Tabs(names, (i, tab) => ..., index, onChange)` | a row of tab names over one page per tab | `TabsHandle` (`Selected`, `Select`, `Rename`) |
| `Swatches(label, colors, index, onChange)` | a header and a row of colour squares; the picked one is framed | `SwatchHandle` (`Index`, `Color`, `Set`) |
| `Stepper(label, value, min, max, step, onChange, format)` | a header and a `< 5 >` row that counts in steps and stops at the ends | `StepperHandle` (`Value`, `Set`, `SetRange`) |
| `Card(title, c => ...)` | a framed grey box with its own controls and a small title | `CardHandle` (`Title`, `Visible`) |
| `List(count, (i, row) => ..., onPick, rows)` | a long list that only builds the rows on screen | `ListHandle` (`Count`, `SetCount`, `Refresh`, `ScrollTo`, `Selected`) |
| `ModelPreview(model or object, height)` | a box showing a 3D model, turned by dragging | `ModelPreviewHandle` (`Show`, `ShowItem`, `Clear`, `Spin`, `Rotation`, `Zoom`) |
| `Tooltip(text)` | gives the control built just before it a tooltip | the builder, so calls chain |
| `Row(r => ...)` | controls side by side, centred | |
| `Space(height)` | empty space | |

`Set` on a handle changes what's shown without calling your callback. Callbacks are guarded: an exception is logged with the window's id and the window stays open (unless it was opened with `CloseOnError`).

### Choices

```csharp
ui.Segmented("Speed", new[] { "Slow", "Normal", "Fast" }, 1, i => SetSpeed(i));
ui.Swatches("Colour", new[] { Color.red, Color.green, new Color(0.2f, 0.4f, 1f) }, 0, i => SetColour(i));
ui.Stepper("Copies", 3, 1, 10, 1, v => _copies = (int)v);
ui.Stepper("Scale", 1f, 0.25f, 4f, 0.25f, v => _scale = v, "0.00x");
```

- A segmented row is a `Cycle` that shows all its options at once; use it for two to five short ones. Left and right step through them, clicks pick.
- Swatch colours are given the way an image editor shows them; the kit converts them for the game's linear colour space, so a square looks like the colour you passed.
- A stepper doesn't wrap: its arrows grey out at `min` and `max`. Values snap to `min + n * step`.

### Tabs

```csharp
ui.Tabs(new[] { "General", "Items", "World" }, (i, tab) =>
{
    if (i == 0) tab.Toggle("God mode", god, SetGod);
    if (i == 1) tab.List(items.Count, (n, row) => row.Text = items[n].Title, n => Give(items[n]));
    if (i == 2) tab.Button("Noon", () => GameClock.SetTime(12, 0));
});
```

Every page is built once, when the window builds; switching tabs only shows another page, so it's instant. The tab picked is kept when the window rebuilds (as long as the build creates the same tab rows in the same order). Left and right on the tab row switch tabs; up and down go into the page. Controls on hidden pages are skipped by keyboard navigation.

### Cards

```csharp
foreach (var save in saves)
    ui.Card(save.Name, c =>
    {
        c.Label(save.Date, muted: true);
        c.Row(r => { r.Button("Load", () => Load(save)); r.Button("Delete", () => Delete(save)); });
    });
```

### Long lists

```csharp
var all = ItemLookup.All;
ListHandle list = ui.List(all.Count, (i, row) =>
{
    row.Text = all[i].Title;
    row.Detail = "#" + all[i].ID;
    row.Muted = !CanGive(all[i]);
}, i => Give(all[i]), rows: 10);
```

Only the rows on screen exist (ten plus two here), and they are filled again as the list scrolls, so 1,240 items cost the same as twelve. `bind` runs for a row when it scrolls into view; set every field every time, because the row object is reused for other entries, and don't keep it. Nothing is allocated by the list while it scrolls, so what `bind` costs is up to you (format strings once up front if you can).

- Mouse: the wheel and dragging scroll it, a click picks. A thin bar on the right shows where you are.
- Keyboard and pad: up and down move a highlight inside the list and leave it at the first and last entry; left and right jump a page; Enter picks.
- `SetCount(n)` after the data changed in size, `Refresh()` after the entries changed in place, `ScrollTo(i)` to show and highlight one. The scroll position is kept when the window rebuilds.

### Tooltips

```csharp
ui.Toggle("Hardcore", hardcore, SetHardcore);
ui.Tooltip("Saves only when you sleep, and death deletes the save.");
ui.Button("Reset", Reset);
ui.Tooltip("Puts every setting on this page back to its default.");
```

`Tooltip` applies to the control built just before it (for options with a header, the row under it). The box shows after the mouse rests on the control for about half a second, next to the pointer, and when the keyboard or pad highlights the control, beside it. It's the kit's own box in the theme's colours; the game's own tooltip is tied to the inventory.

### Model previews

```csharp
var model = ModelRegistry.LoadNow(this, "lamp", "models/lamp.obj");
var preview = ui.ModelPreview(model, height: 400f);
preview.Spin = 30f;                 // degrees per second; 0 keeps it still

ui.ModelPreview().ShowItem(0);      // a game item's world model (0 is the Open Sewer coin)
ui.ModelPreview(somePrefab);        // any object: only its meshes and materials are copied
```

The model is copied onto a small stage of its own far below the world, on a layer the game's cameras don't draw, with its own camera and three lamps, and drawn into a texture the window shows. Drag to turn it, scroll to zoom, Enter (or the pad's A) puts it back, left and right turn it from the keyboard. It draws a frame only when something changed or while it spins, and only while it is visible (a hidden tab draws nothing). The copy is look-only: the original's scripts, colliders, animators and sounds are removed before it is switched on, so a game prefab can't register itself with saves or the HUD. The stage, texture and copy are destroyed when the window closes.

The scene's sun lights every layer, so while a preview is open its light is kept off the preview layer (the game doesn't use that layer, so nothing in the world changes) and handed back when the last preview closes.

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

## Main menu buttons

Any mod can put its own button on the main menu:

```csharp
using ObenUIKit.Menu;

MainMenuButtons.Add(this, "host", "Host co-op", () => OpenHostWindow());
var join = MainMenuButtons.Add(this, "join", "Join", () => OpenJoinWindow(), order: 20);
join.Interactable = false;     // greyed out the game's way until a server is found
join.Label = "Join (2)";
```

- Each button is a copy of the menu's own Credits button: the sign's font, colours, hover and click sounds. The copy loses the original's click action, relays and translation component.
- They go in rows of three under the game's 2x3 grid (New Game, Load Game, Options / MODS, Credits, Quit), in the grid's own columns and row spacing, sorted by `order` and then by when they were added.
- The keyboard and pad reach them: down from the bottom row goes into the mod rows, up from the first mod row goes back to the button above, left and right move along a row. The game's own grid moves are untouched. This is done with postfixes on `MainMenuNavigationHelper.GetButtonAbove/Below/OnTheLeft/OnTheRight` that only answer for moves out of the bottom row and between mod buttons.
- A button goes away when its mod unloads (or with `Remove()`); the rest close up. Ids are per mod; adding the same id again replaces the old button. `MainMenuButtons.All` and `Find(modId, id)` list them.
- MODS itself stays in the grid's unused "???" slot and isn't one of these.

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
| `string` | a text field; typing is written once it settles, leaving the field writes it at once |
| `KeyCode` | a key binding row |
| `Color` | colour swatches: a palette of eleven colours, with the field's own colour first when it isn't one of them |
| anything else | a note to edit it in the config file |

`[Setting]` is optional. It carries `label` (default: the field name with spaces), `Description` (a muted line under the control; `System.ComponentModel.DescriptionAttribute` works too), `Tooltip` (longer help in a tooltip on the control), `Min` and `Max` (both are needed for a slider; `[Setting(min, max)]` is the short form), `Step`, `Group` and `Hidden`. Fields that are `readonly` or `const` are skipped.

- **Groups.** Fields with the same `Group = "Needs"` are shown together in a card with that title, groups in the order they first appear, after the fields without a group.
- **Ranges.** A number outside its `Min`..`Max` in the config file (a hand edit) is put back in range when the config is registered, with a warning in the mod's log; one that isn't a number ("NaN") gets its default. The file is written back with the fixed values.
- **Reset to defaults.** Every config gets a button at the bottom that asks first, then puts every shown field back to the value a new `T()` has. Hidden fields keep theirs (they are the mod's bookkeeping). From code, `config.ResetToDefaults()` resets everything, and `config.Defaults` is a fresh object with the defaults.

### Actions

Things a player does once rather than sets (clear a cache, reset a quest, rescan a folder) are buttons under the mod's settings. Either mark a public method of the settings class:

```csharp
public sealed class MyConfig
{
    public bool ShowOverlay = true;

    [SettingAction("Reset the overlay", Description = "Puts the overlay back in the top left corner.", Confirm = true)]
    public string ResetOverlay()
    {
        OverlayX = 20; OverlayY = 20;
        return "The overlay is back in the corner.";
    }
}
```

or register one from the mod:

```csharp
ModActions.Register(this, "Forget seen hints", () => { _seen.Clear(); return $"Forgot {count} hints."; },
    description: "Every hint shows again.", confirm: true);
```

- The method takes no arguments and returns nothing or a string. After it runs the config is saved and the page is built again (the method may have changed fields shown above).
- A registered action's function returns the text to show as its result, or null for "Done.". With `confirm` the player is asked first in a small window.
- The result of the last run stays under the button. An exception is logged and shown as "Failed: ...".
- A mod with actions but no registered config still gets its own entry on the Settings page. Registered actions go away when the mod unloads; `ModActions.All` and `ModActions.Of(modId)` list them.

Edits are written with `config.Save()` right away, which also raises the config's `Changed` event, so a mod can apply values as they change. For one value, `config.Watch(this, c => c.Speed, speed => Apply(speed))` calls back only when that value changed. Every save logs what changed in the mod's log (`config.json: Speed 1.0 -> 2.5`). Slider drags are written once they settle (about a quarter of a second), when the player switches page or mod, or when the menu closes. The page reads the config's current `Value` each time it is built, so `config.Reload()` is picked up the next time the page is shown. With several registered configs a `Mod` cycle at the top picks the one to edit. A registration goes away when the mod that made it unloads. The kit registers its own config the same way (the option to show What's New after updates).

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

## Key prompts

A prompt is the "press a key to do something" line under the crosshair: the text with the key below it. The kit draws it in the game's own tutorial prompt (the one that teaches moving and interacting in a new game), so it has the game's font, key boxes, mouse pictures and pad buttons, and its chime.

```csharp
using ObenUIKit;

// Near a crate: "Open" over the player's Interact key; pressing it opens the crate instead of the game's interaction.
Hud.Prompt(this, "Open", "Interact", new PromptOptions { Near = crate.transform, Distance = 2.5f, OnPress = OpenCrate });

// A mod key, only while a condition holds.
Hud.Prompt(this, "Call a taxi", KeyCode.G, new PromptOptions { When = () => Wallet.Coins >= 20, OnPress = CallTaxi });

// A mod hotkey; the text follows the player's rebinding. The hotkey's own action runs on its key.
Hud.Prompt(this, "Open the cheat menu", _openHotkey, new PromptOptions { Seconds = 6f });
```

- `Prompt(owner, text, action)` shows one of the game's actions by its key binding name (`"Interact"`, `"Jump"`, `"Inventory"`, see `docs/modding/framework/ui-state.md`), as the player has it bound, keyboard or pad. `Prompt(owner, text, KeyCode)` shows a fixed key, `Prompt(owner, text, hotkey)` a mod hotkey with its modifiers. `{key:Action}` in the text works as in hints.
- `PromptOptions`: `When` (shown while it returns true, asked five times a second), `Near` and `Distance` (shown while the player is that close to the object; gone for good once the object is destroyed), `Seconds` (hidden for good after that long on screen; 0 keeps it), `OnPress` (runs when the key goes down while the prompt shows), `Capture` (default on: with `OnPress`, the key is kept from the game while the prompt shows, through `InputCapture`), `Once` (hide after the first press) and `Sound` (the chime, on by default).
- One prompt shows at a time: the newest one whose conditions hold. The others wait and come up when it goes.
- Prompts show only in a level, and step aside for kit windows, the pause menu, the game's menus and HUD hiding, dialogue, the console and loading. Their timers wait meanwhile, and coming back doesn't chime again.
- The game's tutorial prompt is the game's while one of its own tutorial steps is up (`TutorialGuide.Active`, a new game's opening and the building, cleaning, head lamp and reload hints). Prompts then show in the kit's own copy a little lower on the screen and move back when the game is done.
- `HudPrompt` has `Text` (settable), `IsShowing`, `IsActive` and `Hide()`. `Hud.ShownPrompt` is the one on screen. Prompts go when their owner unloads.

## Guides

A guide is a short walk-through in the same tutorial prompt: one step after another, each waiting for its key, a condition or a few seconds, with a pause between steps like the game's own tutorial.

```csharp
var guide = Hud.Guide(this, "mymod.intro")
    .Step("Open your inventory", "Inventory")                         // done when the player presses it
    .Step("Find the lamp in your bag", () => PlayerItems.Count(LampId) > 0)
    .Step("Wear it with {key:Head Use}", "Head Use", done: () => LampOn())
    .Step("That's all. Have fun!", null, seconds: 4f);
guide.Finished += () => _saved.IntroDone = true;
guide.Start();
```

- `Step(text, action, done)` and `Step(text, KeyCode, done)` show a key and are done when the player presses it, or, with `done`, when `done` turns true (the key is then only shown). `Step(text, done, seconds)` shows text only. A guide's keys aren't kept from the game: it teaches the game's own keys.
- `Pause` (default 1.5 s) is the gap between steps. `Start()` begins at the first step (again, if it ran before); starting a guide stops a running one with the same id. `Stop()`, `Skip()` (counts the current step as done), `Current`, `Count`, `IsRunning`, `IsFinished`, and the `StepStarted` and `Finished` events round it off.
- A guide doesn't remember that it ran; keep that in the mod's save (`ModSaves`) if it should show once per character.

## Hiding the game's HUD pieces

```csharp
IDisposable noCrosshair = Hud.HideCrosshair(this);   // until disposed or the mod unloads
IDisposable noHud = Hud.HideGameHud(this);          // the whole HUD, like the hide-HUD key
```

Several mods can ask at once; the piece comes back when the last one lets go. Both are applied again after a level change. The HUD goes through the game's own per-object hide list, so it stacks with the game's own reasons (the screenshot key, cutscenes). The kit's hints, bars and prompts hide along with the game's HUD.

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

## Computer apps

Apps for the in-game computers. A registered app gets a tile on the desktop of every computer the player sits down at (a column at the right edge of the desktop picture, in the screen's own font); clicking the tile opens the app in a kit window over the screen. While an app window is open the computer ignores Esc and Interact, so closing the window doesn't make the player get up.

```csharp
using ObenUIKit.Computer;

ComputerApps.Register(this, "MyMod.Stocks", "Stocks", ui =>
{
    ui.Header("Open Sewer Exchange");
    ui.Live(() => "Rat futures: " + price.ToString("0.00"));
    ui.Button("Buy", Buy);
}, icon: ModAssets.Sprite(this, "icons/stocks.png"));

ComputerApps.Open("MyMod.Stocks");          // the same app as a standalone window, anywhere: a hotkey, an item, the main menu
ComputerApps.Opened.Subscribe(this, id => ...);
ComputerApps.Closed.Subscribe(this, id => ...);
```

`Register(owner, id, title, build, icon = null, width, height)` lasts until the owner unloads or the result is disposed; up to 30 apps fit on a desktop. The window is an ordinary kit window owned by the app's mod (`width` and `height` as in `UiKit.Open`). `ComputerApps.IsOpen(id)`, `Close(id)` and `All` round it off.

The computers themselves (which one the player uses, sitting down and getting up, e-mails, recycle bin files, the desktop picture) are in the framework: `ObenFramework.World.Computers` (`Computers.Used`, `Current`, `Emails`, `Files`, `SetWallpaper(owner, computer, sprite)`, `DesktopOf`). Bridge: `computers.list` (`id` for one computer's e-mails and files) and `computer.apps` (`open` to open one).

## Known limitations

- HUD bars and the fallback hint panel have fixed places (top left, right edge); the game's own HUD layout isn't read.
- A hotkey can only be a keyboard key with Ctrl, Shift or Alt; mouse and pad buttons can't be bound, same as the key binding row.
- Windows can't be resized by the player; a floating window can only be moved.
- A display-only window (`LockInput = false`) can't be clicked in a level, because the game keeps the cursor hidden.
- Only one list scrolls at a time under the mouse wheel: over a list the wheel scrolls the list, not the window.
- A text field can't be typed into with a controller (there is no on-screen keyboard); the rest of a window works with one.
- Keyboard navigation moves in one column, in build order; it doesn't know about left and right placement beyond `Row` and the footer.
- Only one key prompt shows at a time. A prompt's `Capture` goes through `InputCapture`, so keys the game reads straight from Unity (F1, the toolbelt numbers) still reach the game.
- Main menu buttons go under the sign's grid; with many rows they may run past the bottom of the sign (`oben call menu.buttons` reports whether each sits inside the menu's rectangle).
- Only one tab row per `Tabs` call and no nested scrolling inside a tab: a tab's page is part of the window's body and scrolls with it.
