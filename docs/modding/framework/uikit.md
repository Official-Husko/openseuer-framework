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

For uGUI of your own (in an `Area`, or on a handle's `Rect`), the theme has a few more things:

- `UiKit.Theme.MutedSize` (the size of muted text), `Accent`, `AccentHover` and `AccentPressed` (the game's reds for what is picked, hovered and pressed).
- `UiKit.Theme.Glyph('→', "->")`: the character when the menu font draws it, else the fallback. Characters the font has but draws as something else ("<", ">", "»", "›") count as missing.
- `UiText.Plain(text)` and `UiText.Paint(color, text)` (namespace `ObenUIKit.Controls`): text from outside (a log line, a name, a value) for a label, list row or title, shown exactly as it is. Labels read rich text tags, so a "<b>" in a log line would turn the rest bold, and the menu font draws angle brackets as stray marks; these escape the tags and draw the brackets in TextMeshPro's default font. `Paint` also colours it.
- `UiTheme.Blocks(normal, highlighted, pressed)`: a uGUI `ColorBlock` for a `Selectable` of your own, with the disabled colour half transparent and a short fade.
- `UiSprites.RoundedRect(radius)`: a white rounded rectangle (corners of 0 to 64 pixels) that slices to any size; put it on an `Image` with `type = Sliced` and tint it with the Image's colour. The kit's own windows keep the game's square look.
- `UiSprites.Gradient(bottom, top)`: a 1 by 64 texture fading between two colours, for a `RawImage` backdrop.

The sprites and textures are made once, shared, and destroyed when the kit unloads.

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
- Esc (or the pad's B, or whatever key or pad button the game's "Pause Menu" action is bound to, as the game's own menus do) closes the top window. It closes a frame later, so the same key press doesn't also reach the game. While a text field or a key binding row has the keyboard, Esc goes to it instead and the window stays.
- The window locks game input (movement, mouse look, hotkeys) and shows the cursor while open. In a level that goes through the game's own `GameController` lock, and a game menu that is open (inventory, map) closes first, since both can't have the keys. If the game's unlock throws on the way out, the keys and cursor are freed directly so the player isn't left stuck. At the main menu the menu's keyboard navigation is paused.
- Over the pause menu, the pause menu's keyboard handler and its Esc toggle are held off until the window closes.
- Windows close on their own when the player changes level or a save starts loading. If something destroys a window's objects without closing it, the window closes itself so its input lock and scope are still released.

A `height` argument (default 0, the full-height column) makes a centred box instead, which is what `UiKit.Confirm` uses.

`UiWindow` has `Title`, `IsOpen`, `Close()`, `Rebuild()` (build the body again with current values), `Defer(action)`, `CloseOnEscape`, `CloseReason`, `Dock`, `LocksInput` and a `Closed` event. `Defer` runs something at the end of the frame, after the click or key being handled: the place for a `Rebuild()` or `Close()` from inside a control's callback. `UiKit.Find(id)` returns an open window by id; `UiKit.AnyWindowOpen` is true while a window that takes input is open, `UiKit.AnyWindowShown` while any window is on screen, display-only ones included.

### Docked, floating and display-only windows

Pass a `WindowOptions` for anything beyond the centred column:

```csharp
UiKit.Open(this, "mymod.tracker", "Tracker", ui => ui.Live(() => Status()), new WindowOptions
{
    Dock = WindowDock.Right,   // Center (default), Left, Right, Floating, Full
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

UiKit.Open(this, "mymod.wardrobe", "Wardrobe", ui => ShowOutfit(_picked), new WindowOptions
{
    Dock = WindowDock.Full,    // the whole screen less a margin
    Side = side => { foreach (var outfit in Outfits) side.Button(outfit.Name, () => Pick(outfit)); },
    SideWidth = 440f,
});
```

| Option | Default | Does |
| --- | --- | --- |
| `Dock` | `Center` | `Center` is the pause menu's column; `Left` and `Right` are columns along the screen edge; `Floating` is a box the player drags by its title (clicking it brings it to the front); `Full` covers the screen less a margin |
| `Width`, `Height` | 760, 0 | in 1920x1080 units; height 0 is the full screen height (640 for a floating window). A full window ignores both |
| `Position` | centre | floating windows: where the centre goes |
| `Top` | 0 | full-height `Left` and `Right` columns: room kept free above the column (150 clears the game's two money counters in the top-right corner) |
| `Side`, `SideWidth` | none, 440 | full windows: builds a panel left of the body, with its own scrolling and scrollbar. It is built again on `Rebuild()`, and the keyboard goes through it before the body |
| `Footer` | Back button | builds the bottom bar |
| `LockInput` | true | off makes a display-only panel: game keys, mouse look and Esc stay with the game, there's no keyboard navigation, and the HUD and hotkeys don't step aside for it. In a level the cursor stays hidden, so it can't be clicked; at the main menu it can |
| `Dim` | centre and full | darken the scene behind; a dimmed window also stops clicks reaching the game |
| `CloseOnEscape` | true | |
| `CloseOnError` | false | close the window as soon as one of its callbacks (a control, a `Live` text, the build itself) throws. The error is logged either way |
| `KeepOnLevelChange` | false | stay open through a level change or a save loading |

### A window is a lifetime scope

`UiWindow` is an `ITracker`: anything passed to `window.Track(disposable)`, and anything subscribed or registered with the window as its owner, is disposed when the window closes, whatever the reason (Back, Esc, the owner unloading, a level change, an error).

```csharp
var window = UiKit.Open(this, "mymod.watch", "Watch", ui => ...);
GameClock.MinutePassed.Subscribe(window, OnMinute);    // unsubscribed when the window closes
window.Track(InputCapture.Key(this, KeyCode.G));       // the game gets G back when it closes
window.Closed += () => Log.Info("closed: " + window.CloseReason);
```

`CloseReason` is one of `Requested`, `Escape`, `OwnerUnloaded`, `LevelChange`, `SaveLoad`, `Error`, `Destroyed` or `KitUnloaded`. `window.Run(what, action)` runs a callback of yours with the same guard the controls use.

## Controls

| Method | Looks like | Returns |
| --- | --- | --- |
| `Header(text)` | the grey bar above each setting in the game's options | `LabelHandle` |
| `Title(text)` | a large line on the left for a section, without a bar | `LabelHandle` |
| `Label(text, muted, align)` | plain wrapped text; muted is greyer and smaller | `LabelHandle` (`Text`, `Color`, `Size`) |
| `Live(() => text, muted)` | a label read again four times a second while the window is open | `LabelHandle` |
| `Poll(() => ...)` | runs four times a second while the window is open, until it is built again: for a control that follows something itself, like a list that grows (a throw is logged and stops it) | nothing |
| `Button(text, onClick)` | underlined text button, like Apply and Back | `ButtonHandle` (`Enabled`, `Text`) |
| `Button(text, style, onClick)` | `ButtonStyle.Plain` as above, `Quiet` (smaller and greyer, for lesser actions) or `Box` (a bar in the option-row look that lights up red, for the main action) | `ButtonHandle` |
| `Cycle(label, options, index, onChange)` | a header and a `< value >` row; arrows step, the row lights up dark red under the cursor | `CycleHandle` (`Index`, `Set`, `SetOptions`) |
| `Toggle(label, value, onChange)` | a cycle between Off and On; the game has no checkboxes | `ToggleHandle` (`Value`, `Set`) |
| `Slider(label, min, max, value, onChange, format, wholeNumbers)` | a header and a slider with its value on the left (written the same in every language); `onChange` runs at most once a frame, with the latest value | `SliderHandle` (`Value`, `Set`) |
| `TextField(label, value, onChange, onEnd)` | a header and a box styled like the developer console's input field | `FieldHandle` (`Text`, `Set`, `Enabled`, `Focus()`, `Masked`: stars instead of the text, for join codes and keys on a stream) |
| `KeyBind(label, key, onChange)` | a header and a row showing the key; click or Enter, then press the new key | `KeyBindHandle` (`Key`, `Chord`, `Set`) |
| `KeyBind(label, chord, onChange)` | the same for a `KeyChord`: hold Ctrl, Shift or Alt and press the key | `KeyBindHandle` |
| `Segmented(label, options, index, onChange)` | a header and a row with every option side by side; the picked one is lit and underlined | `ChoiceHandle` (`Index`, `Set`) |
| `Tabs(names, (i, tab) => ..., index, onChange)` | a row of tab names over one page per tab | `TabsHandle` (`Selected`, `Select`, `Rename`, `Page(i)` to add to a page later) |
| `Swatches(label, colors, index, onChange)` | a header (none for a null label) and colour squares in rows of up to eight; the picked one is framed | `SwatchHandle` (`Index`, `Color`, `Set`) |
| `Stepper(label, value, min, max, step, onChange, format)` | a header and a `< 5 >` row that counts in steps and stops at the ends | `StepperHandle` (`Value`, `Set`, `SetRange`) |
| `Card(title, c => ...)` | a framed grey box with its own controls and a small title | `CardHandle` (`Title`, `Visible`) |
| `List(count, (i, row) => ..., onPick, rows)` | a long list that only builds the rows on screen | `ListHandle` (`Count`, `SetCount`, `Refresh`, `ScrollTo`, `Selected`) |
| `ModelPreview(model or object, height)` | a box showing a 3D model, turned by dragging, or the player live | `ModelPreviewHandle` (`Show`, `ShowItem`, `ShowPlayer`, `ShowLive`, `Refresh`, `Clear`, `Framing`, `ShowFramingSwitch`, `Spin`, `Rotation`, `Zoom`, `Renderers`, `Texture`) |
| `Tooltip(text)` | gives the control built just before it a tooltip | the builder, so calls chain |
| `HeaderButton(text, onClick, tooltip)` | a small underlined button at the right end of the title bar of the control built just before it | `ButtonHandle` |
| `Resettable(changed, reset, tooltip)` | gives the control built just before it a Reset like the Settings page's: shown on its title bar while `changed()` is true, and Backspace, Delete or the pad's X on its row | |
| `Row(r => ...)`, `Row(r => ..., align, spacing)` | controls side by side, centred 40 apart, or placed and spaced as given | |
| `Scroll(height, s => ...)` | a scrolling area of its own with its own scrollbar, for a long run of controls beside others that stay put | its `RectTransform` |
| `Area(height)` | an empty area for uGUI of your own; put your objects under the rect it returns | `RectTransform` |
| `Space(height)` | empty space | |

`Set` on a handle changes what's shown without calling your callback. Every handle has a `Rect` (the control's row or box) for uGUI of your own or layout changes. Callbacks are guarded: an exception is logged with the window's id and the window stays open (unless it was opened with `CloseOnError`).

```csharp
SliderHandle volume = null;
volume = ui.Slider("Volume", 0f, 1f, _volume, v => _volume = v, "0.00");
ui.Resettable(() => _volume != 0.5f, () => { _volume = 0.5f; volume.Set(0.5f); }, "Default: 0.50");
ui.HeaderButton("Max", () => { _volume = 1f; volume.Set(1f); });
```

`HeaderButton` and `Resettable` go right after a control with a title bar (a slider, cycle, toggle, text field, key binding...); anywhere else they throw, which the window logs.

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

#### Live and posed characters

```csharp
var preview = ui.ModelPreview(520f);
preview.ShowPlayer();                       // false outside a level
preview.Framing = PreviewFraming.Face;      // or Body (the default)
preview.ShowFramingSwitch = true;           // "Body" and "Face" in the corner for the player
// after changing the outfit:
preview.Refresh();
```

`ShowPlayer()` shows the player as they are, not a copy: the game keeps posing and animating them, and whatever they wear shows. `ShowLive(gameObject)` does the same for another object in the scene, an NPC say. The object's renderers move to the preview layer while it is shown, so it leaves the game's own view (also the player's body in first person, which is switched on for the preview) and comes back exactly as it was when the preview shows something else, is cleared or its window closes. The camera and lamps go to the object, start in front of it (its forward) and circle it as you drag; the framing is measured again every frame. The scene's other lights are kept off the preview layer for as long as it is live. One live preview at a time: a second one takes the object over from the first.

`Framing` fits the whole model (`Body`) or its top (`Face`: a character's head and shoulders) for copies and live objects alike. `Refresh()` copies the shown object again after it changed, or picks up a live object's new parts after an outfit change, keeping the turn and zoom. `Renderers` counts what is on show, and `Texture` is the picture the preview draws into.

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
| Backspace, Delete, pad X | puts the highlighted setting back to its default (Settings page rows with a Reset) |
| Page Up / Page Down, the game's tab keys (Q by default), pad LB / RB | turns the page in a window that has pages (the mod menu), wrapping around |
| Esc, pad B, the game's "Pause Menu" key or button | closes the top window |

The highlight is the control's mouse hover look. Moving the mouse over a control moves the highlight there, so only one control is ever lit. The body, and any scroll area or side panel the control sits in, scrolls to keep the highlighted control in view. When the next control is out of view (a long description, the release notes, an error's stack trace), Up and Down first scroll the text a step at a time and move the highlight once that control shows, so pages that are mostly text can be read from the keyboard or pad. Disabled buttons are skipped.

The mouse wheel scrolls the body anywhere over it, over text and the gaps between controls too. A thin bar in the right margin shows where you are on a long page and can be dragged; it hides when everything fits. When a window rebuilds (a mod menu page change, `Rebuild()`), the highlight stays at the same place in the list.

Navigation reads the keyboard directly, and only runs while a window is open and no text field or key binding has the keyboard. W/S/A/D and the arrow keys are fixed; they don't follow the game's key bindings. The page keys do: the keys bound to the game's "Switch Tab Left" and "Switch Tab Right" turn pages too, unless one of them is a key that already moves around the window.

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

Pages are sorted by `Order`, then title, and chosen with a `< page >` row at the top or with the page keys (Page Up and Page Down, the game's tab keys, the pad's bumpers), which wrap around. The footer shows those keys next to Back. A page goes away when its mod unloads. Two pages with the same title get a warning in the kit's log, since only the first can be opened by name. A page whose build throws shows "This page couldn't be shown" under the page row, with the error in the kit's log, so the player can still turn to another page. `ModMenu.Open(pageTitle)` opens the menu, on a page if given; `ModMenu.Close()` and `ModMenu.IsOpen` do what they say.

The built-in pages:

- **Mods** lists every installed mod with its version, description and state, whether it loads when the game starts, and an Off/On switch for each of its features (`ModFeatures`).
- **Settings** shows every mod's config files, with a search over all of them (below).
- **Hotkeys** lists every mod's registered hotkeys with controls to rebind them (see Hotkeys below).
- **What's New** shows the release notes from `changelog.json`, newest release first: each release with its date and how many lines of each kind it has, then a card per mod (alphabetical, lines without a mod last under "General"). Every line starts with its kind in that kind's color: green `feat` (added), amber `change`, blue `fix`, red `remove`. See `docs/modding/changelog.md` for where the file comes from.
- **Errors** lists this session's problems from `ErrorReport`: who reported each one, its title, how often and when it last happened, the scene, and the first eight lines of its details ("Show all N lines" opens the rest). Errors nobody caught are red, problems a mod caught and reported amber. Stack traces are colored: the exception type in the problem's color, an inner exception on a line of its own ("Caused by"), and in each frame the method, file and line picked out, with frames in System, Unity, Harmony and Newtonsoft code dimmed so the mod's own stand out. The names the compiler gives lambdas, iterators, async methods and local functions read the way they are written (`S.Loader.Items (lambda)`), Harmony-patched methods say `(patched)`, and paths are cut to the file name. Clear empties the report, Copy list puts it on the clipboard (the full text, uncolored), and "Make a bug report" goes to the Report page.
- **Report** makes a bug report zip (`BugReports`, see [docs/modding/reports.md](../reports.md)): the versions, the system, the logs and the mods' settings always, and the processor, graphics card and screen, memory and the save being played when ticked. "Hide personal info" is on by default. Make report packs it on a worker thread and says where it went and how big it is (and when it's over GitHub's 25 MB limit); Open folder shows `ObenLoader/reports`, Copy summary puts `report.md` on the clipboard, Report on GitHub opens a new bug report with the versions and ticked parts filled in.

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
- Only as many rows as fit on the screen are used: the menu is a sign in the scene, and the kit measures through the menu camera where each row would end (two rows, six buttons, at 16:9; the camera's height of view is the same at any aspect). The measurement is taken while the sign is in view and remembered for the other pages.
- When there are more buttons than places, the last place becomes "More…", which opens a small window listing the rest as buttons that do what their menu buttons would (greyed out ones stay greyed). The window follows changes to the list while it's open and closes when everything fits again. `IsShown` is false for a button in that list.
- `Interactable = false` greys a button out the way the pause menu greys its own (the game's disabled text colour) and ignores its clicks.
- The keyboard and pad reach them: down from the bottom row goes into the mod rows, up from the first mod row goes back to the button above, left and right move along a row, and "More…" is part of that. The game's own grid moves are untouched. This is done with postfixes on `MainMenuNavigationHelper.GetButtonAbove/Below/OnTheLeft/OnTheRight` that only answer for moves out of the bottom row and between mod buttons. After a mod's window closes, the button that opened it is still the menu's highlighted one.
- A button goes away when its mod unloads (or with `Remove()`); the rest close up. Ids are per mod; adding the same id again replaces the old button. `MainMenuButtons.All` and `Find(modId, id)` list them.
- MODS itself stays in the grid's unused "???" slot and isn't one of these.

## Pause menu buttons

The pause menu takes mod buttons too:

```csharp
using ObenUIKit.Menu;

PauseMenuButtons.Add(this, "coop", "Co-op", () => OpenCoopWindow(), order: 10);
```

- Each is a copy of the pause menu's own Resume button (like MODS): its font, colours, hover and click sounds, without the original's click action and relays. They sit above MODS, which stays just above "Quit to Menu", sorted by `order` and then by when they were added. The pause menu's own keyboard and pad handling reaches them.
- They are made in every level (the pause menu is part of the level) and go away when their mod unloads or with `Remove()`. `Label` and `Interactable` change them; `IsShown` is true while one is in the menu. Ids are per mod; adding the same id again replaces the old button. `PauseMenuButtons.All` and `Find(modId, id)` list them.

The pause menu also shows "ObenseuerExtended" and "v0.2.1 (57a4977)" on two lines just above the game's own version at its bottom, in the same small text, kept inside the menu's black column: the version and the commit the kit was built from ("-dirty" after uncommitted changes), read from the kit's own assembly. Every assembly of a build carries the same commit and a hot reload brings the new kit's, so after a change the line tells whether it loaded. Pre-releases are titled with the same version and commit.

## Settings pages

Every config a loaded mod opens with `LoadConfig<T>()` shows on the Settings page by itself, titled with the mod's name (and the file name when a mod has several, like "DevBridge (trace)"). Nothing has to be registered, and mods that don't reference the kit get a page too. `[System.ComponentModel.Browsable(false)]` on the settings class keeps a file off the page (the kit's hotkeys.json is one), and `[DisplayName("...")]` on the class changes its title. `ModSettings.Register` gives a file your own title, or shows one loaded some other way:

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
| `string` with `Choices` in `[Setting]` | cycle through the choices; a value in the file that isn't one of them is kept and offered first |
| `List<string>`, `string[]` | a text field with the items separated by commas, written when the player leaves the field |
| `KeyCode` | a key binding row |
| `Color` | colour swatches: a palette of eleven colours, with the field's own colour first when it isn't one of them |
| `[Flags]` enum | a card with an Off/On switch per single flag (combinations like `All` are left out) |
| `Vector2`, `Vector3`, `Vector4` | a text field with the numbers separated by commas; text that isn't the right count of numbers puts the old value back |
| anything else | a note to change it in the config file |

`[Setting]` is optional. It carries `label` (default: the field name with spaces), `Description` (a muted line under the control), `Tooltip` (longer help in a tooltip on the control), `Min` and `Max` (both are needed for a slider; `[Setting(min, max)]` is the short form), `Step`, `Group`, `Choices` and `Hidden`. A mod that doesn't reference the kit can use the System.ComponentModel attributes: `[DisplayName]` for the label, `[Description]`, `[Category]` for the group and `[Browsable(false)]` to hide a field. Fields that are `readonly` or `const` are skipped.

- **Groups.** Fields with the same `Group = "Needs"` are shown together in a card with that title, groups in the order they first appear, after the fields without a group.
- **Ranges.** A number outside its `Min`..`Max` in the config file (a hand edit) is put back in range when the page first lists the config (or it is registered), with a warning in the mod's log; one that isn't a number ("NaN") gets its default. The file is written back with the fixed values.
- **Reset per setting.** A setting that differs from its default has a small Reset at the right end of its title bar; hovering it shows the default. Backspace, Delete or the pad's X does the same on the highlighted row. Flags have no Reset of their own.
- **Reset to defaults.** Every config gets a button at the bottom that asks first, then puts every shown field back to the value a new `T()` has. Hidden fields keep theirs (they are the mod's bookkeeping). From code, `config.ResetToDefaults()` resets everything, and `config.Defaults` is a fresh object with the defaults.
- **Search.** The search box at the top of the page finds settings and actions of every mod: type a few words and press Enter (or leave the field). Every word has to appear in a setting's label, description, tooltip or group, ignoring case; words that match a mod's title find all of that mod's settings. The results are grouped in a card per mod and can be changed right there. "Clear the search" (or an empty search) goes back to one mod at a time, and the search starts empty each time the menu opens.
- **A setting that fails to build** (a broken value, a bug in a control) is logged and shown as a line saying so; the rest of the page is built as usual.
- **Outside the Settings page.** `ModSettings.Draw(ui, config, title)` draws the same controls (groups, ranges, Reset, action buttons) into any kit window, for a mod that wants its settings in its own window too. Pass `showReset: false` to leave out the "Reset to defaults" button.

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
    description: "Every hint shows again.", confirm: true, group: "Hints");
```

- The method takes no arguments and returns nothing or a string. After it runs the config is saved and the page is built again (the method may have changed fields shown above).
- A registered action's function returns the text to show as its result, or null for "Done.". With `confirm` the player is asked first in a small window.
- The result of the last run stays under the button. An exception is logged and shown as "Failed: ...".
- Actions with the same `group` (or `Group` in `[SettingAction]`) share a card with that title, after the actions without one.
- Registering a title the mod already used replaces that action in its place. Disposing the handle `Register` returns removes the action.
- A mod with actions but no config still gets its own entry on the Settings page. Registered actions go away when the mod unloads; `ModActions.All`, `ModActions.Of(modId)` and `ModActions.Find(modId, title)` look them up. The page's search finds actions by title and description.

Edits are written with `config.Save()` right away, which also raises the config's `Changed` event, so a mod can apply values as they change. For one value, `config.Watch(this, c => c.Speed, speed => Apply(speed))` calls back only when that value changed. Every save logs what changed in the mod's log (`config.json: Speed 1.0 -> 2.5`). Slider drags are written once they settle (about a quarter of a second), when the player switches page or mod, when the menu closes, or when the game quits. The page reads the config's current `Value` each time it is built, so `config.Reload()` is picked up the next time the page is shown. With several configs a `Mod` cycle under the search box picks the one to edit. A config leaves the page when the mod it belongs to (or the mod that registered it) unloads; one that reloads shows its new copy. The kit registers its own config the same way (What's New after updates, the "Show mod hints" and "Show mod key prompts" switches below, and "Show mod difficulty presets" for the New Game screen's selector, see difficulty.md). The New Game screen's mod option rows are described in new-game.md.

The list of fields is worked out by reflection once per settings class and cached.

## HUD hints, subtitles, bars and readouts

`Hud` puts short things on the game's HUD. Each belongs to the mod (or feature) that adds it and goes when that unloads.

```csharp
using ObenUIKit;

HudHint hint = Hud.Hint(this, "Press {key:Interact} to open the crate.", seconds: 8f);
hint.Text = "Press {key:Interact} to open the other crate.";   // changes it in place, on screen too
Hud.Hint(this, "Rain is coming.", title: "Weather");
Hud.HideHints(this);                                            // every hint of this mod
Hud.Subtitle("Somebody is following you.", seconds: 4f);
Hud.GameSubtitle("...and it isn't the police.");               // the game's own line, where remarks show

HudBar bar = Hud.Bar(this, "Stamina", () => Stamina.Fraction);
bar.Color = Color.cyan;   // optional; null puts the theme's slider colour back
bar.Label = "";           // an empty label leaves the line out
bar.Remove();

HudReadout readout = Hud.Readout(this, "Crates", () => $"Spawned {_spawned}\nOpened {_opened}");
readout.Title = "Crates (hard mode)";
readout.Remove();
```

- **Hints** go through the game's own `HintOverlay` side panel in levels, so they look and animate like its scene hints, and `{key:Action Name}` becomes the player's current key (`HintContent.ResolveKeys`). Our entry goes above the scene's own hints while it shows, and theirs come back after. The player's Hints key and setting still apply: with hints switched off nothing shows unless you pass `force: true`. So does the kit's "Show mod hints" setting (MODS, Settings, UI kit): while it is off, `Hud.Hint` drops the hint and returns it with `IsActive` false, again unless `force: true`, and switching it off drops the hints already waiting or showing. `Hud.ModHintsShown` reads it. A hint that isn't forced still runs its time while the player has the game's hints off, so it runs out unseen. Where the game has no hint panel (the main menu) the kit draws an equivalent box on the right edge from the theme, and unresolved `{key:...}` shows as the plain action name.
- Hints show one at a time in the order they were added, each for `seconds` (0.5 to 120, default 6) once it is up. Asking again for a hint with the same owner, title and text extends the one already waiting instead of queueing a copy (and keeps `force` if either asked for it), so calling `Hud.Hint` from an event that fires often is safe. At most six wait, three of them from one owner: an owner over its share loses its own oldest waiting hint, so one busy mod can't push the others out. `HudHint.Hide()` removes one early, `Hud.HideHints(owner)` all of an owner's, and `Hud.ShownHint` is the one on screen. Hints carry over a level change.
- **Subtitle** is one line near the bottom of the screen on a dark strip, in the game's font (rich text). A new line replaces the one showing; it fades during its last half second. It has no owner because it only lives for a few seconds, and it works at the main menu too.
- **GameSubtitle** adds a line to the game's own subtitles, where characters' remarks show: it types out (20 characters a second by default), then fades over its seconds. Lines stack and can't be taken back. It answers false outside levels.
- **Bar** is a small labelled bar at the top left, track and fill in the theme's slider colours. The function returns 0 to 1 and is read five times a second (the bar glides between reads, with no allocations per frame); if it throws, the bar is logged and removed (one that throws on its first read comes back with `IsActive` false). Up to six bars; `Hud.Bar` returns null beyond that. Bars only show in a level and stay over level changes, since they follow their function.
- **Readout** is a box of text at the top left, under the bars, in the hint panel's look: a grey title (left out when empty) over the text, which may have several lines. The function is read four times a second and the label is only written when the text changed; if it throws, the readout is logged and removed (one that throws on its first read comes back with `IsActive` false). The text shows as it is, so values with angle brackets are safe; it is cut at 2000 characters. Up to four readouts; `Hud.Readout` returns null beyond that. Readouts only show in a level. The Cheat Menu's developer menu shows its watches in one.
- Nothing shows while a modal kit window or the pause menu is open (docked, floating and display-only windows leave the HUD alone), while a save loads, or while the game's own HUD is hidden (the hide-HUD key, inventory and other full menus). Hint and subtitle timers wait until it is back. The HUD pieces sit under the kit's windows.

The code is in `Hud/`: `HintQueue` (plain bookkeeping), `NativeHints` (the `HintOverlay` bridge), `HintPanel`, `SubtitleLine`, `BarStack`, `ReadoutStack`, with `HudRuntime` tying them to the kit's lifetime. The per-frame component is only enabled while something is showing.

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
- One prompt shows at a time: the newest one whose conditions hold. The others wait and come up when it goes. Asking again from the same owner for the same text, key and `Near` object returns the prompt already there (with its first options), so `Hud.Prompt` can be called every frame. One owner can have 16 prompts; past that its oldest goes. A prompt for an action that has no key on the keyboard or the pad is logged as a likely misspelt action name.
- Prompts show only in a level, and step aside for modal kit windows, the pause menu, the game's menus and HUD hiding, dialogue, the console, loading and a paused game. Their timers wait meanwhile, and coming back doesn't chime again.
- The player can switch mod prompts off (MODS, Settings, UI kit, "Show mod key prompts"; `Hud.ModPromptsShown` reads it). They stay registered but out of sight, so their `OnPress` keys and the guide steps waiting on them do nothing until the switch is on again.
- The game's tutorial prompt is the game's while one of its own tutorial steps is up (`TutorialGuide.Active`, a new game's opening and the building, cleaning, head lamp and reload hints). Prompts then show in the kit's own copy a little lower on the screen and move back when the game is done.
- `HudPrompt` has `Text` (settable), `IsShowing`, `IsActive` and `Hide()`. `Hud.ShownPrompt` is the one on screen, and `Hud.HidePrompts(owner)` takes all of an owner's away. Prompts go when their owner unloads; one tied to an object that was destroyed goes at once.

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
- `Hotkey` has `Chord`, `Default`, `Rebind(chord)`, `Reset()`, `Active` (false once its owner unloaded) and a `Changed` event.
- **Polling.** Besides the action, a mod can ask in its own update: `hotkey.Down()` (the chord went down this frame, where its scope allows), `hotkey.Held()` (it went down as this hotkey and its main key is still held, even if a modifier was let go) and `hotkey.Up()` (the main key came up after that). A plain K never reports a press that was really Ctrl+K.
- **Pad buttons.** A key binding row takes a pad button too (stored as the any-pad `JoystickButton3`, shown as "Pad Y"); the pad's B cancels the capture, so it can't be bound. Clashes with the game's pad bindings are found the same way.
- One check per frame covers every hotkey. It returns at once unless some key went down, and only then looks at the hotkeys' keys and the gate. It allocates nothing. `onPressed` runs inside a try/catch that logs the mod's id.
- The gate is what the Cheat Menu uses to open: no firing while a text field or key binding row has the keyboard, while the game waits for a key in its own key bindings page, in the F1 console, with a menu, shop, dialogue, or the pause menu open, or while the game's controls are locked. A kit window blocks it too, unless the hotkey's `HotkeyScope` includes `Windows` (a hotkey that closes its own window). `HotkeyScope.MainMenu` allows it at the main menu. The default is `InGame`.
- **Conflicts.** `Hotkeys.ConflictOf(hotkey)` says what a hotkey shares its key with: another hotkey that fires with it, or a key of the game's (the player's current bindings from `KeyBindings`, so rebinding in the game is noticed, plus the keys the game reads directly: F1, F11, F12, 1 to 4, Esc, Enter; a numbered pad's button counts as the same button). Modifiers are ignored for the game's keys. A clash at registration is logged; nothing is blocked.
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
- The menu font draws "<" and ">" as stray marks (and "»", "›" as quotes). The kit's own pages draw text that isn't markup (error messages, release notes) with the angle brackets borrowed from TextMeshPro's default font; a mod's own labels don't do that yet, so write "→" (which the font has) or reword.
- A text field can't be typed into with a controller (there is no on-screen keyboard); the rest of a window works with one.
- Keyboard navigation moves in one column, in build order; it doesn't know about left and right placement beyond `Row` and the footer.
- Only one key prompt shows at a time. A prompt's `Capture` goes through `InputCapture`, so keys the game reads straight from Unity (F1, the toolbelt numbers) still reach the game.
- Only one tab row per `Tabs` call. A tab's page is part of the window's body and scrolls with it; a `Scroll` area inside it scrolls on its own, but the wheel doesn't pass on to the window once the inner area reaches its end.
