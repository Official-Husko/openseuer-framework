# ObenUIKit: reference study and design

Notes for building ObenUIKit (`src/ObenUIKit/`). Input, menu and console basics are in `docs/game/ui-input-console.md`; this file does not repeat them, it only cites them as "UIC section N".

Path shorthand (all read-only):

- `NR` = `example-framework/src/` (NomadsReach, ObenseuerCore, MIT, BepInEx, game 0.5.08). UI kit in `NR/Ui/`, mod menu in `NR/ModMenu*.cs`, main menu button in `NR/Api/MainMenuButtons.cs`.
- `NK` = `../neet-mods/src/NeetModKit/` (our own toolkit for NEET, new Input System, legacy `UnityEngine.UI.Text`).
- `G` = `decompiled/Assembly-CSharp/` (game 0.5.13).

Things I could not confirm from code (scene hierarchies are not in the decompile) are marked "unverified". Section F ends with a short list of runtime checks to do first.

## Summary of the two references

| | NomadsReach (NR) | NEET (NK) |
|---|---|---|
| Look | Draws its own dark rounded-rect UI, borrows only a TMP font | Clones live game uGUI elements, sanitizes them, instantiates them from a hidden holder |
| Theme | Hard-coded colours/sizes, font found by scanning | Set of game elements (paths) per theme, plus metrics and scrollbar colours |
| API | Static `ModUi.*(parent, ...)` returning handle objects | Instance `UiBuilder` (column/row/panel/scroll) returning the cloned uGUI component |
| Windows | `ModWindow` per call, own overlay canvas, 4 docks | `UiWindow` / `UiTabbedWindow`, one full-screen overlay |
| Menu | MODS button on main menu + a "Mods" tab cloned into the in-game menu strip | F1 hotkey + MODS column in the pause menu; tabs + per-mod pages |
| Cleanup | Per-window teardown, but static page list and sprite cache leak | `ITracker` owners destroy what they created; some statics leak |

Neither does what we want on its own. NR's controls and input handling are the more complete; NK's "reuse the game's resources" idea and owner-scoped cleanup are the right shape. The proposal in F combines them and changes one thing both skipped: capture the game's *values* (font, material, sprites, colours, sounds) and stamp them onto our own objects, instead of drawing from scratch (NR) or cloning scene objects into a long-lived holder (NK).

---

## A. How each toolkit makes UI look native

### A.1 NomadsReach: draws its own, takes a font

- Everything is built from `new GameObject(..., typeof(RectTransform))` plus `Image`/`TextMeshProUGUI` (`NR/Ui/ModUi.cs:331-336`, `:443-459`). No game prefab, sprite, material or sound is reused.
- The only game resource is the font: `UiTheme.Font` (`NR/Ui/UiTheme.cs:64-71`) calls `FindFont` (`:95-103`), which returns the font of the first `TMP_Text` that `Resources.FindObjectsOfTypeAll<TMP_Text>()` happens to list, falling back to `TMP_Settings.defaultFontAsset`. That list includes prefab assets and non-menu text (phone, terminal), so which font you get is not defined, and the font's material variants (outline, shadow) are ignored. It is cached in a static, re-found only when Unity-null.
- Colours are literals (`UiTheme.cs:49-58`: near-black `0x121418`, blue accent `0x4C8DFF`). Sizes are consts (`:9-47`). Rounded rects come from a generated 9-sliced texture cache (`NR/Ui/UiSprites.cs:13-62`).
- The look is "a tidy dark modern UI", not Obenseuer's. Buttons make no hover/click sound; text colour does not follow the game's button behaviour.
- The one place it clones live objects is `ModMenuWidgets.CloneCycle/CloneSlider` (`NR/ModMenuWidgets.cs:59-83`): reads `OptionsMenu.languageButton` and `fovSlider` by reflection from `PauseMenu.optionsMenu` (`:107-121`), `Instantiate`s them, strips listeners (`:95-105`, also sets persistent calls to `Off`). Marked `[Obsolete]`, not used by `ModUi`.
- Where it does clone: the main menu button (A.3) and the in-game menu tab (D.1).

### A.2 NEET: clones game elements into a holder

- Theme = `UiTheme` (`NK/Ui/Toolkit/UiTheme.cs`): a source root (`Func<Transform>`), a `UiTemplate -> path` map, an optional `Fallback` theme, `UiMetrics` and a scrollbar colour style.
- `Ensure()` (`:49`) finds the source, creates an inactive `DontDestroyOnLoad` holder (`:62`), `Instantiate`s one copy per template path (warns and skips missing paths, `:70`), then `Sanitize` (`:102`): removes named decorations, `EventTrigger`s, replaces `onClick`/`onValueChanged`/input events with fresh ones (that is how persistent scene calls are dropped), clears input text. `Ready` is just "holder exists and has templates" (`:46`), never revalidated.
- Templates (`NK/Ui/Toolkit/UiThemes.cs:15-30`) are the pause menu's own elements by path under `Canvas_Settings/`: backdrop `Image`, header `Controls/Display_Text (1)`, sub-header, body text `DataDisclaimer`, violet `ChangeControlsButton`, orange `Quit Button`, `Bloom_Toggle`, `Brightness_Slider`, `CommentInput`, a dropdown's scrollbar. A second theme (`:34`) copies the in-game computer's site elements and falls back to the pause theme.
- Per control, `UiBuilder.Copy` instantiates the template (`NK/Ui/Toolkit/UiBuilder.cs:133`) and the builder sets text/listeners (`UiBuilder.Controls.cs:28-110`). Fonts, materials, sprites, colours and click sounds all come along for free because the component is the game's.
- Scrollbar: the dropdown's is white, so `UiScrollbarStyle` recolours it and copies the handle sprite to the track (`NK/Ui/Toolkit/UiScrollbarStyle.cs`).
- Canvas: `UiCanvas.Create` copies the pause menu's `CanvasScaler` settings (`NK/Ui/Toolkit/UiCanvas.cs:29-47`), with a hard-coded fallback.
- Live clones (not templates) for the pause menu column: `UiCloning.Clone` of the CONTROLS column, header repositioned to the FEEDBACK header, feedback form hidden (`NK/Ui/PauseMenuMods.cs:19-45`).

### A.3 What each captures, and when

| Item | NR | NK |
|---|---|---|
| Fonts | one `TMP_FontAsset`, lazily, first access | whatever the cloned `Text` has (legacy `Text` + font) |
| Font materials | no | via clone |
| Sprites | generated rounded rects | via clone (button, slider, backdrop sprites) |
| Colours | literals | via clone, plus literals in `MenuPalette` and `UiScrollbarStyle` |
| Sizes | consts | `UiMetrics` (menu 26/22/140...; computer 12/10/40...) |
| Click/hover sounds | none | via clone (the components' own audio) |
| Canvas scaler | fixed 1920x1080, match 0.5 (`ModWindow.cs:153-157`) | copied from pause menu |

Timing:

- NR: font on first use, never refreshed. Main-menu button: attached in a 1-second retry loop (`NR/Api/MainMenuButtons.cs:87-109`) once `MainMenu.Instance` exists; `Forget()` (`:482`) resets when the menu object changes.
- NK: lazily at first window/notice (`UiWindow.cs:29` throws "game's menu isn't loaded yet" if `Feedback_Sender._` is missing; `NoticeView.cs:32` degrades to the built-in font and no sprite instead). The pause menu exists in NEET's one house scene, so "in world" and "in menu" are the same source. In Obenseuer the main menu and the pause menu are different scene objects (F.3), so a one-shot lazy capture is not enough.
- When sources are missing: NR falls back to `TMP_Settings.defaultFontAsset`; NK throws (windows) or degrades (notices); `Fallback` themes cover missing templates only, not a missing source.

---

## B. Builder APIs

### B.1 NomadsReach `ModUi` (static, handles)

Public surface (`NR/Ui/ModUi.cs`, docs `example-framework/docs/api/ui/controls.md`):

| Area | Members |
|---|---|
| Text | `Title`, `Text(parent, text, muted)` |
| Containers | `Card(parent, title)`, `Stack`, `Row`, `Scroll` (content rect), `Place(rect, anchors, offsets)` |
| Controls | `Button(style: Primary/Secondary/Ghost)`, `Slider` -> `ModSlider`, `Toggle` -> `ModToggle`, `Stepper` (cycle) -> `ModStepper`, `TextField` -> `ModTextField`, `KeyBinding` -> `ModKeyBinding`, `Swatches`, `Tabs` -> `ModTabs`, `Segmented`, `ModelPreview` |
| Windows | `ModWindow.Open(modId, title, WindowDock{Left,Right,Center,Full}, build)`, `Find`, `Close`, `Body/Side/Footer`, `Closed` event, `CloseReason` |
| Mod menu | `ModMenu.AddPage(modId, title, Action<RectTransform>)` |
| Not present | dropdown, list/virtual list, tooltip API (internal `ModMenuTooltip`), confirm dialog (internal `ModMenuActionRow` arm/expire), notifications |

Handles have `Rect` and `Set(value)` that never calls back (`NR/Ui/ModControls.cs:41-62`, `:122-170`). Callbacks run through `UiScope.Run` (`ModWindow.cs:444-515`), which finds the owning `ModWindow` by `GetComponentInParent` and routes exceptions to `Fault` (window closes, logged once).

Layout: uGUI LayoutGroups throughout.
- `Field` row (`ModUi.cs:377-441`): a card `VerticalLayoutGroup` with a fixed-height line (`FieldHeight 52`, +20 with help), inside `HorizontalLayoutGroup` with label share 45 and control share 55 via `flexibleWidth`. Fixed heights, so no `ContentSizeFitter` per row.
- `ScrollPage` (`:517-553`): `ScrollRect`, `RectMask2D` viewport, content with `VerticalLayoutGroup` + one `ContentSizeFitter(vertical)`; a nested `Canvas`+`GraphicRaycaster` on the scroll root; custom scrollbar (`:580-599`).
- Windows are anchored rects (`ModWindow.Panel/Box`, `:166-263`): footer 56 px along the bottom, body above it, optional left "Side" column (440 px or 23.5%).
- Manual RectTransform math only in `Place` and slider/knob internals.
- `ContentSizeFitter` on cards only when the parent has no layout group (`:42`).

### B.2 NEET `UiBuilder` (instance, returns game components)

Public surface (`NK/Ui/Toolkit/UiBuilder*.cs`, docs `neet-mods/docs/modding/ui-and-menu.md`):

| Area | Members |
|---|---|
| Construction | `new UiBuilder(root, theme)`, `Vertical(root, spacing, padding)` |
| Containers | `Row(spacing, height)`, `Column(width, flexW, flexH)`, `Panel(tint, padding)`, `Grid(cellW, cellH)`, `Scroll(out ScrollRect)` -> child builder, `Space`, `FlexibleSpace`, `Clear`, `Size(component, ...)` |
| Controls | `Header`, `SubHeader`, `Text(text, color, size)`, `Button(label, onClick, width, accent)`, `Toggle`, `Slider(label, value, min, max, onChange, whole)`, `Input`, `LabeledInput` |
| Windows | `UiWindow(owner, title, onClose)` with `Toolbar`/`Content` builders; `UiTabbedWindow(owner, title, pages, onClose)`; `UiPage { Title, Order, Build(ui), Refresh(), OnHide(), Rebuild() }` |
| Other | `UiCanvas.Create(owner, ...)`, `UiInputLock.Acquire(pause)`, `Notices.Show(owner, title, detail, progress, seconds)` with `Notice` handle, `VersionTag.AddNote`, `UiCloning.Clone/Remove/SetClick/SetText` |
| Not present | dropdown, cycle/stepper, key-binding capture, tooltip, confirm dialog, TMP (uses legacy `Text`/`InputField`) |

Layout is the same LayoutGroup scheme: a builder is "a rect with a layout group", `Row` and `Column` nest, sizes through `LayoutElement` (`UiBuilder.cs:110`). `Scroll` adds one `ContentSizeFitter` (`UiBuilder.Scroll.cs:32`) and a cloned scrollbar. Text shrinks with `resizeTextForBestFit` (`UiBuilder.Controls.cs:137`), which is expensive and not available the same way in TMP (use `enableAutoSizing` sparingly or fixed widths).

Pages refresh at 4 Hz (`UiTabbedWindow.cs:14`, `Tick :73`) and are rebuilt wholesale on `Rebuild()`; page lists are `OwnedList<T>` that drop a mod's entries when the mod unloads (`NK/Menu/ModMenu.cs:28-29`, `:43-54`).

### B.3 Layout lessons

- LayoutGroups with fixed row heights and `LayoutElement` flexible shares (NR) are enough. One `ContentSizeFitter` per scroll content, none per row.
- Wrapping text needs a `LayoutElement` or a preferred height; both references got burned by fitters fighting layouts (NK removes template fitters, `NoticeView.cs` AddText).
- Rebuild by destroying children and re-running the page builder (both do). Only rebuild on structural change; refresh values through handles.

---

## C. Input and focus

### C.1 NomadsReach

- World lock: `ModWindow.Lock` (`NR/Ui/ModWindow.cs:265-275`) calls `GameController.ControlsDisabled(window, ShowCursor: true)` per window (keyed by the window object) and force-hides the game menu. Unlock goes through `CoreInput.EnableControls` (`NR/CoreInput.cs:84-103`), which saves and restores `ThirdPersonCameraController.moveDisabled/firstpersonMoveDisabled` around `ControlsEnabled`, because `ControlsEnabled -> ControlsEnabledMove -> EnableMovement/EnableFirstPersonMovement` (`G/GameController.cs:409-414`) clears camera locks held by other systems. Fallback if that throws: count holders by reflection (`ModWindow.cs:296-301`, uncached) and reset flags by hand.
- Text fields: `ModFieldFocus` (`NR/Ui/ModInput.cs:83-123`) takes a shared lock `UiInput.Engage()` on select (`:25-29`, `:36-55`) that calls `ControlsDisabled(holder, ShowCursor: true, disableKeys: true, writeLock: true)` (`:49`). Esc while typing de-selects and marks "escape consumed".
- Esc: `ModWindow.LateUpdate` (`:366-379`) closes the top window on `InputManager.GetKeyDown("Pause Menu")` (not while a field in it is focused; `Typing()` only evaluated after Esc, so no per-frame allocation). Because `LateUpdate` runs after `GameController.Update`, the game sees Esc first, so `ModMenuInput` Harmony-prefixes `GameController.Update` to skip while a window is engaged or Esc was consumed this/last frame (`NR/ModMenuInput.cs:16-37`, `EscapeConsumed` `ModWindow.cs:130-138`). Also prefixes `InventoryNavigationHandler.SwitchTab` and `Update` while its custom tab is open (`:19-20`, `:38`).
- Main menu: `MainMenuButtons.SetControlsEnabled(owner, bool)` (`NR/Api/MainMenuButtons.cs:446-468`) keeps a per-owner hold count and sets `MainMenuNavigationHandler.ControlsEnabled` false while held, restoring the previous value and reselecting the MODS button on release. `Tick` re-applies the hold every frame (`:97`) because the game re-enables it itself.
- Keyboard/controller navigation inside the kit window: own `ModMenuNav` (`NR/ModMenuNav.cs`, 659 lines): units grouped in three columns (side/body/footer), d-pad axes `DPadHorizontal/Vertical` + left stick, `Return`/pad A activates (sliders step), pad B / Esc back, bumpers via `"Switch Tab Left/Right"`, auto-scroll to the focused row (`ModUi.ScrollTo :566-578`). Selection rides on `EventSystem.current.currentSelectedGameObject`, so every `ModUi` control is a `Selectable`.
- EventSystem: `UiEvents.Acquire/Release` (`ModWindow.cs:517-540`) creates a `StandaloneInputModule` EventSystem only if none exists, refcounted.
- Key rebinding widget: `KeyCapture` (`ModInput.cs:125-181`) reads the next key from a cached `Enum.GetValues(KeyCode)`; ignores modifiers/mouse/Esc.
- Pause menu's own `PauseNavigationHandler.Update` ungated (UIC 4.3): not handled anywhere in NR as far as I found. Their windows open from the main menu or the Tab menu, not from the pause screen, so the Enter/X click-through never came up.

### C.2 NEET

- `UiInputLock.Acquire(pause)` (`NK/Ui/Toolkit/UiInputLock.cs`): ref-counted `IDisposable` handles; disables the game's `InputActionAsset` (Input System, n/a here), frees the cursor, optionally sets `Time.timeScale = 0` (`:94`) unless the world is shared (co-op). `ApplyCursor` re-applies every frame because the game's camera script re-locks the cursor (`:42-48`). Whole thing is a static with `WorldRole.Changed += Apply` in a static constructor (`:30`) that is never unsubscribed.
- Esc/F1: `ModMenu.Update` polls `Hotkey` and `Hotkey.EscapePressed` (`NK/Menu/ModMenu.cs:126-132`), toggling the whole menu (no per-window stack).
- No keyboard/controller navigation of its own: legacy uGUI `Selectable`s clicked with the mouse; text input uses the game's `InputField` clone.
- Pause: it can pause time itself. On a shared house it does not.

### C.3 What carries over

| Problem | Obenseuer answer (already known) | Kit rule |
|---|---|---|
| Game input while a window is open | `GameController.ControlsDisabled(owner, ShowCursor: true)` (UIC 2.6) | One ref-counted `InputLock` per kit, keyed by one DDOL GameObject of ours, plus restore the two camera flags (NR `CoreInput.cs:84-103`) |
| Not in a gameplay scene | `GameController.instance` null; it also derefs 4 other singletons | Lock only when `WorldState.IsReady`; wrap in try/catch; otherwise skip the lock (menu) |
| Esc reaching the pause menu | `PauseMenu.Pause()` is called from `GameController.Update` (`G/GameController.cs:102`) and `PauseMenu.Update` (`PauseMenu.cs:~96`); when `GameIsPaused`, `keysDisabled` is ignored (`:71`) | Harmony prefix on `PauseMenu.Pause` returning false while a kit window is open. Smaller than patching all of `GameController.Update`, also covers pad B (`:75`) |
| Pause handler clicking behind the window | `PauseNavigationHandler.Update` (`G/PauseNavigationHandler.cs:90`) has no gate | Prefix `Update` -> false while a kit window is open |
| Main menu handler | `MainMenuNavigationHandler.ControlsEnabled` public setter (`G/MainMenuNavigationHandler.cs:77`) | Hold count per owner like NR `SetHold`, re-asserted in a cheap per-frame check only while held |
| Typing in a field | `keysDisabled` already stops hotkeys; do not use `writeLock` | Do not copy NR's `writeLock: true` (`ModInput.cs:49`): `writeLock` makes `GameController.Update` call `GameUIController.WriteLockClose()` on Esc/Menu (`G/GameController.cs:62-67`), a notes-panel feature |
| Controller active hides cursor + `MouseBlocker` eats clicks | UIC 2.6, 3.5 | On first mouse movement while a kit window is open and `inputType == Controller`: `InputManager.instance.ChangeInputType(Keyboard)` once |
| Navigation handlers' closed set | `Button`, `OptionsButton`, `TMP_InputField`, `LoadMenuButton` (UIC 5.1) | Kit windows are their own overlay with their own navigation, so no limit. Only the MODS *entry buttons* (cloned `Button`s) live in the game's pages |

---

## D. Mod menu

### D.1 NomadsReach

- Main menu: `MainMenuButtons.Add("Mods", "MODS", OpenPanel)` (`NR/ModMenuMainMenu.cs:10-13`, public API `NR/Api/MainMenuButtons.cs:36-45`). Implementation: wait for `MainMenu.Instance`, find a template button (first `Button` whose persistent `onClick` target is the `MainMenu` and method `LoadGame`, `:264-278`), `Instantiate` it next to the original (`:280-297`), delete `UnityEngine.Localization*` components (`:312-322`), replace `onClick`, set all `TMP_Text` children to the label. Placement: measure the 2x3 grid from `MainMenuNavigationHelper`'s private buttons (`GridFields` by reflection, `:346-350`), place new buttons in a row under the bottom row (`Layout :352-383`). Navigation: four Harmony postfixes on `MainMenuNavigationHelper.GetButtonAbove/Below/OnTheLeft/OnTheRight` (`:76-85`, `:117-173`). Disabled with a single flag if any patch fails.
- In-game: not a pause menu button. `ModMenuTab.Build` (`NR/ModMenuTab.cs:22-83`) clones the Character tab's slot and panel of the Tab menu (`UIPageButtons`), appends a `MenuPage` to the private `MenuPages` array by reflection with a fresh `MenuPageNames` value above `Items` (`:215-220`), destroys the cloned `CharacterPanel`, rewires the click, and moves it by hand if it overlaps a neighbour (`VerifySpacing :143-177`). Opening runs through `Follow` (`:124-141`): when the game selects that page it opens the Mods window instead and re-selects the previous page when it closes. `ModMenuInput` then patches `InventoryNavigationHandler.Update/SwitchTab` to stay out of the way.
- Window: `ModMenuWindow` (`NR/ModMenuWindow.cs`): Full dock, left sidebar of page buttons, scrolling body, footer with hints/Back/Close; bumpers switch pages (`Bumper :195`); a failed page shows "This page failed to load" (`:179`, the text still names BepInEx logs).
- Page registration: `ModMenu.AddPage(modId, title, Action<RectTransform> build)` (`NR/ModMenu.cs:15-30`), kept in a static `List<ModMenuPage>`; sorted settings-pages-first then by title; a duplicate title only logs a warning. **Never removed.**
- Pages: every mod's `ConfigFile` entries as a searchable, grouped list (`ModMenuConfigList.Build`, `NR/ModMenuConfigList.cs`, one page when `Build == null`), plus per-mod action rows with arm-to-confirm (`ModMenuActions*.cs`), plus an optional HTML page (WebUi module, not relevant). No mods list with on/off, no what's-new, no error page.

### D.2 NEET

- Pause menu: `PauseMenuMods.Apply` (`NK/Ui/PauseMenuMods.cs:17-45`) clones the pause menu's CONTROLS column, moves its header onto the FEEDBACK header, hides `ReportType/CommentInput/DataDisclaimer`, adds two stacked clones of the first button ("MOD MENU", "WHAT'S NEW", `:47-67`; width doubled for longer labels, `WidthFactor :53`). Placement uses scene positions of named children, i.e. hand layout tied to NEET's menu.
- Also F1 (`ModMenu.Update`).
- Registration: `ModMenu.AddPage(owner, MenuPage)` lists a page under the owner's name on the "Mod Settings" tab; `AddTab(owner, page)` adds a top tab (about six fit). Both are `OwnedList`s, entries vanish on owner unload (`NK/Menu/ModMenu.cs:28-54`). `UiPage` lifecycle: `Build` on show, `Refresh` at 4 Hz, `OnHide`, `Rebuild()`.
- Built-in tabs (`NK/Menu/Pages/`): **Mods** (`ModsPage.cs`, 236 lines): installed list with state colour, details, RELOAD/UNLOAD/LOAD NOW, "load at startup" toggle, features list, tail of the mod's log, problem count; **Mod Settings** (`ModSettingsPage.cs`): per mod, its pages plus an auto-built settings editor from the settings class's public fields (`NK/Settings/ConfigEditor.cs`: `bool` -> toggle, enum -> choice buttons, ranged numbers -> slider, text -> input, cached per type), saved 0.5 s after last edit (`SettingsView.cs`); **What's New** (`WhatsNewPage.cs`, changelog JSON per mod, newest first); **Errors** (`ErrorsPage.cs`, 130 lines): loader error report, ERROR vs CAUGHT tint, expandable stack text, copy/clear.
- Reload friendliness: remembers the open page across a framework reload (`ReloadState.Put/Take`, `ModMenu.cs:106-124`).

---

## E. Problems and smells

### E.1 NomadsReach

1. Files over the limit: `ModMenuNav.cs` 659, `ModUi.cs` 601, `ModWindow.cs` 541, `ModelPreview.cs` 491, `ModMenuConfigList.cs` 354, `ModMenuTab.cs` 312. `ModWindow.cs` also holds `UiScope` and `UiEvents`; `ModMenuNav.cs` holds the order logic and self-check data (`:217-221` hard-coded test keys).
2. BepInEx types in the UI API itself (`KeyboardShortcut`, `ConfigEntry`, `ModUi.cs:2-3`, `ModMenuWindow.cs:BindStyle`). Needs replacing with our own value types.
3. Leaks on unload/hot reload: `ModMenu.pages` static list never pruned (`ModMenu.cs:13`); `UiSprites` caches `Texture2D` forever and never destroys them (`UiSprites.cs:13-20`, `:55`); `UiInput.holder` DDOL object never destroyed (`ModInput.cs:43-46`); `MainMenuButtons` statics (`entries`, `holds`, `row`).
4. Windows close on `PlayerWillLoadGame` and `sceneUnloaded` of the home scene (`ModWindow.cs:160-164`, `:355-364`) but nothing closes them when the *mod* unloads; they are not owner-scoped, only string-id scoped (`ModId`).
5. Per-frame work: `ModMenu.Update` ticks five subsystems every frame (`ModMenu.cs:39-49`). `MainMenuButtons.Tick` loops `row` children and recomputes `ForeignSum()` every frame (`MainMenuButtons.cs:98`), and `GridButton` does `GetField` by name per call (`:346-350`), `Row()` allocates a `List` per navigation call (`:175-186`). `ModMenuNav.Update` reads `Selected()` and several axes every frame; fine, but nothing is cached/pooled.
6. Reflection without caching: `ModMenuReflect.Value` does `GetField` per call (`ModMenuWidgets.cs:129-135`), used by `ModMenuTab.Pages` (`:184-189`) and `ModWindow.Holders` (`:296-301`).
7. Brittle game coupling: the Mods tab replaces a private serialized array on `UIPageButtons`, invents a `MenuPageNames` value past the enum (any `switch ... default: throw` in the game would blow up; they patch two `InventoryNavigationHandler` methods to stay clear), and hand-fixes overlaps. The FAQ/status say it is pinned to 0.5.08 (`docs/status.md:13`); the game is at 0.5.13 now.
8. Font choice is arbitrary (A.1). No sounds, no game sprites: it does not look like the game.
9. `ModWindow.Lock` is not in a try/catch and relies on four other singletons being non-null (UIC 8.1); only `Unlock` has a fallback.
10. Text-field `writeLock: true` (see C.3).
11. Unity-null: mostly correct (`!= null` on `GameController.instance`); `?.` is not used on Unity objects. Good.

### E.2 NEET

1. Cloned templates live in a DDOL holder (`UiTheme.cs:62`), but the copies keep serialized references to *scene* objects. In Obenseuer that is exactly the problem: `TextButtonColorEffectTMP` has `Relay onSelected/onUnselected` (`G/TextButtonColorEffectTMP.cs:43,46`, fired from `ChangePressedState :229-233`), `HoverSoundEffect.effectSound` is an `AudioSource` (`G/HoverSoundEffect.cs:9,26-27`) that is destroyed with its scene, and `DefaultUIButton` drives an `Animator` and sprites. A holder copy hovered after a scene change throws `MissingReferenceException`. NEET got away with it because legacy `Text`/`Button` templates carry fewer references.
2. `Sanitize` removes by name and type lists (`Extras` array, `UiTheme.cs:~20`), so a game update that renames a decoration silently leaves behaviour in the copy.
3. `Ready` never revalidated: if the holder children get destroyed, `Get` returns Unity-null.
4. Hard dependency on layout of the pause menu by path (`Canvas_Settings/...`); a rename breaks a template (logged, skipped, then the fallback throws `InvalidOperationException` `UiTheme.cs:84`).
5. Unity-null misuse: `Feedback_Sender._?.PlayMenuSFX(0)` (`PauseMenuMods.cs:64`), `KitMod.Instance?.Log`, `input?.Enable()` on a `ScriptableObject` (`UiInputLock.cs:87`). Our AGENTS.md rule: singletons go stale as Unity-null, no `?.` on them.
6. `UiInputLock` static constructor subscribes to `WorldRole.Changed` and never unsubscribes (`:30`); with hot reload the old handler stays alive.
7. Static `UiThemes.Menu/Computer` hold delegates and dictionaries for the process lifetime; `ReleaseAll` is called only from `ModMenu.Shutdown`.
8. `UiTabbedWindow` rebuilds the tab bar by destroying and re-creating buttons whenever `_tabsDirty` (`:76`, `BuildTabs :112`); fine per interaction, but pages `Refresh` allocate strings (`ModsPage.Refresh`: `"STATUS: " + ...` four times a second).
9. `NoticeBoard` builds a new `VerticalLayoutGroup` + `ContentSizeFitter` stack and a full `Text` clone set per notice (`NoticeView` ctor); OK for rare notices.
10. Files: all under 300 (largest is `ModsPage.cs`, 236). Good discipline, worth copying (partial `UiBuilder` split into 3 files).
11. Legacy `Text`; our game uses TMP everywhere (121 files, UIC 1). Everything text-related must be rewritten against `TMP_Text`.

---

## F. Proposed design: ObenUIKit

### F.1 Principles

1. **Stamp, don't clone, for content.** Capture the game's values once (font + material, sprites, colours, metrics, `SoundInfo`s, canvas scaler) into a `UiTheme` and build our own small uGUI objects with them. Reason: cloned game widgets carry references to scene objects (E.2.1), animators, `Relay`s and `SavableScript` GUIDs (`Relay : SavableScript`, `G/Relay.cs`), and the Obenseuer toolbox has no toggle, dropdown or text input in its settings pages worth cloning (the options pages are `OptionsButton` cycles and `Slider`s, UIC 3.2; no `TMP_Dropdown` or `UnityEngine.UI.Toggle` is used anywhere in `Assembly-CSharp`).
2. **Clone only where the game's own navigation must see the object**: the MODS entry buttons on the main menu and the pause menu. Those clones stay in the scene they were cloned from (same parent, destroyed with the scene), never in a DDOL holder.
3. **Native idioms where the game has them**: toggles are cycle rows ("Enabled / Disabled" with arrows, as `OptionsButton`), enumerations are cycle rows, numbers are `Slider`s with the game's number readout, notifications go through `Notifications.instance.CreateNotification` when it exists, tooltips through `ToolTip.instance.Activate(title, details)` (`G/ToolTip.cs:116`).
4. **Owner-scoped everything.** Every window, page, entry button, hold and subscription is created with an `ITracker` owner (the mod) and dies when it unloads.
5. **No per-frame allocation, no per-frame polling beyond one `Tick` for the open windows.** Event-driven where the game offers events.

### F.2 Folders and files

Assembly `ObenUIKit` (mod id `ObenUIKit`, depends on `ObenFramework`, `hotReload: true`, essential). Root namespace `ObenUIKit`. Line budgets are targets, all under 300.

```
src/ObenUIKit/
  mod.json, ObenUIKit.csproj
  UiKitMod.cs                  ObenMod entry: builds Runtime, subscribes events, Harmony category   ~110
  UiKit.cs                     static facade: Open, Confirm, Toast, Menu, Theme                      ~120
  Runtime/
    KitRuntime.cs              owns root object, window stack, tick; Dispose tears all down         ~140
    KitRoot.cs                 DDOL root + overlay canvas factory + EventSystem ensure              ~120
    KitTick.cs                 MonoBehaviour: one Update/LateUpdate -> windows, refreshers, nav     ~90
  Theme/
    UiTheme.cs                 immutable-ish data: fonts, materials, colours, sprites, sounds, sizes ~140
    ThemeCapture.cs            runs probes in order, merges, raises Changed; ThemeLevel              ~160
    Probes/MainMenuProbe.cs    MainMenuNavigationHelper buttons, OptionsMenu rows, console field     ~170
    Probes/WorldProbe.cs       PauseMenu, ToolTip, Notifications prefab, scaler of the HUD canvas   ~170
    Probes/AssetProbe.cs       Resources.FindObjectsOfTypeAll by name (fonts, materials) fallback    ~90
    ThemeFallback.cs           built-in defaults when nothing could be captured                      ~70
    UiSounds.cs                copies SoundInfo, plays with explicit parent, ignores listener pause  ~80
    UiSprites.cs               fallback rounded sprites (9-slice) with Dispose                       ~90
    Native.cs                  cached FieldRefAccess/delegates for private game fields               ~120
  Windows/
    UiWindow.cs                handle: Title, Body, Footer, Close, Closed, Refresh                   ~170
    UiWindow.Build.cs          panel/dock layout, scroll, footer                                     ~150
    WindowStack.cs             order, top-most, back handling, close reasons                         ~100
    Dialogs.cs                 Confirm / Message / Prompt (modal, stamped)                           ~160
  Controls/
    UiBuilder.cs               builder core: Row, Column, Section, Space, Size, Clear                ~160
    UiBuilder.Text.cs          Header, Label, Muted, Separator, Link                                 ~100
    UiBuilder.Buttons.cs       Button, ButtonRow, Cycle, Toggle                                      ~160
    UiBuilder.Inputs.cs        Slider, TextField, KeyBind, Number                                    ~170
    UiBuilder.Lists.cs         Scroll, List<T> (pooled rows), Tabs, ChoiceList (popup for >8 items)  ~200
    Kit*.cs                    one MonoBehaviour per control (KitButton, KitCycle, KitSlider,
                               KitField, KitScroll, KitTip) with hover/press/sound/colours           ~80-150 each
    Handles.cs                 ToggleHandle, SliderHandle, CycleHandle, FieldHandle (Set w/o notify)  ~140
  Input/
    InputLock.cs               ref-counted lock, camera-flag restore, cursor                         ~150
    InputGates.cs              Harmony: PauseMenu.Pause, PauseNavigationHandler.Update               ~90
    MainMenuHold.cs            MainMenuNavigationHandler.ControlsEnabled hold per owner              ~90
    UiNav.cs                   focus ring: arrows/stick/d-pad, A/Enter, B/Esc, bumpers, scroll-to     ~260
    KeyCapture.cs              rebind widget input                                                    ~100
  Menu/
    ModMenu.cs                 page registry (OwnedList), Open/Close, remember page across reload     ~130
    ModMenuWindow.cs           sidebar of pages + body + footer hints                                 ~170
    MainMenuEntry.cs           clone button + layout row                                              ~200
    MainMenuNav.cs             four postfixes on MainMenuNavigationHelper (cached FieldRefs)          ~120
    PauseMenuEntry.cs          clone resumeMenuButton as direct child of the pause main page         ~150
    Pages/ModsPage.cs, SettingsPage.cs, WhatsNewPage.cs, ErrorsPage.cs                               ~150-250 each
    Pages/ConfigEditor.cs      public fields of a settings class -> controls (cached per type)        ~170
  Notifications/Toasts.cs      native route + stamped fallback                                        ~130
```

Rules inside: no static state outside `KitRuntime` (one instance, created in `OnLoad`, disposed in `OnUnload`); `UiKit` methods forward to it.

### F.3 Theme capture strategy for Obenseuer

**Capture values from live objects found by component type and serialized field, not by hierarchy path.** Scene hierarchies are not in the decompile (UIC 3.1), types and private field names are, and a renamed child object breaks a path while a field name changing is much rarer. Fall back to name lookups of assets only if a probe fails.

What the theme holds (`UiTheme`):

| Slot | Content |
|---|---|
| `Font` + materials | `TMP_FontAsset` (expect "Erika Ormig SDF", UIC 3.2) and the `fontSharedMaterial` of title text, body text, outlined text. Never `fontMaterial` (that creates an instance that dies with the object). |
| Text styles | per role (Title, Body, Muted, Button, Value): font size, colour, alignment, character spacing, `enableAutoSizing`, from real labels |
| Button colours | `TextButtonColorEffectTMP`: default, hover, press, disabled, disabledHover, disabledPress (private serialized `Color`s, `G/TextButtonColorEffectTMP.cs:9-27`) |
| Row look | `OptionsButton`: row `Image` sprite, `defaultColor`, `hoverColor`, `disabledColor`, text colours (`G/OptionsButton.cs:9-30`) |
| Slider look | `Slider` background/fill/handle `Image` sprites and colours, readout text style |
| Panels | background sprite/colour of the options page root and of `TooltipUI.tooltip` |
| Input field | sprite + colours + selection/caret colours of `DeveloperConsole.InputField` |
| Sounds | copies of `pressSound`/`hoverSound` `SoundInfo` (clip, mixer group, volume, pitch) from a button, `OptionsButton`, and the notification box |
| Metrics | button/row heights measured from `RectTransform.rect`, spacing from sibling distance, arrow glyphs from `OptionsButton.buttonLeft/Right` text |
| Scaler | `CanvasScaler` mode, reference resolution, match, taken from the canvas that owns the source |

Sources:

| Where | Source (type, field) | Gives |
|---|---|---|
| Main menu | `MainMenuNavigationHelper` private `newGame/loadGame/options/credits/quit` (`G/MainMenuNavigationHelper.cs:7-24`) -> `TextButtonColorEffectTMP` on each | Main button text style, colours, sounds; also the template for the MODS main-menu entry |
| Main menu | `OptionsMenu` with `mainMenu == true` (`G/OptionsMenu.cs:15`): `languageButton` (`:84`, `OptionsButton`), `fovSlider` (`:68`, `Slider`), `refreshRateButton` (`:37`) | Cycle row and slider look, value text style, arrows |
| Both | `DeveloperConsole.Instance` (`G/DeveloperConsole.cs:295-310`: DDOL, exists in menu and world): `inputField`, `consoleText` (`:23,26`) | Text input look, mono-ish text style. Background is inactive but still readable |
| World | `PauseMenu.instance`: `resumeMenuButton`, `saveMenuButton` (public `GameObject`, `G/PauseMenu.cs:66,68`), private `optionsMenu` (`:16`) | Pause button style (may differ from the main menu), second `OptionsMenu` instance for rows |
| World | `ToolTip.instance` -> `TooltipUI` `tooltip/title/details` (`G/TooltipUI.cs:9-20`) | Tooltip panel look |
| World | `Notifications.instance`: private `notificationBoxPrefab` (`G/Notifications.cs:29`) -> `NotificationBox.typeText/nameText` (`G/NotificationBox.cs`) | Toast style |
| World | `GameUIInstance.instance` canvas (`G/GameUIInstance.cs`) | HUD scaler |
| Optional | `ShopBaseUI.notification` (`ConfirmationNotification`, `G/ShopBaseUI.cs:126`) | Confirm dialog look, only if the shop UI is loaded |

Two theme instances, same data type: `UiTheme.MainMenu` and `UiTheme.Pause`. `UiKit.Theme` returns the one matching `WorldState.InWorld`, falling back to the other, then to `ThemeFallback`. A window keeps the theme it was created with and rebuilds on `Changed`.

When:

1. `OnLoad`: if `WorldState.InMainMenu` run the main-menu probes now (hot reload mid-session), if `WorldState.IsReady` run the world probes now (`GameEvents.WhenWorldReady` already does the "right away if ready" part).
2. `GameEvents.MainMenu`: run `MainMenuProbe` (the game raises it when the scene is up; defer one frame so `OptionsMenu.StartInit` finished, `G/PauseMenu.cs:Start`/`optionsMenu.StartInit`).
3. `GameEvents.WorldReady`: run `WorldProbe` (PauseMenu, ToolTip, Notifications exist in the world scene).
4. Revalidate cheaply on `SceneLoaded`: if any captured reference is Unity-null (`font == null`, sprite, material), mark the level `Stale` and recapture on the next `MainMenu`/`WorldReady`. Captured values are assets (fonts, materials, sprites, clips, mixer groups) and `Color`/`float` structs, so they survive a scene unload as long as we hold references; but `Resources.UnloadUnusedAssets` on a scene change can still drop assets only the scene referenced, so the check is needed. If it turns out to bite, set `HideFlags.DontUnloadUnusedAsset` on the few captured assets and restore on dispose (unverified).

When sources do not exist (menu has no pause source, game was updated, probe threw):

- Each probe returns what it filled; failures log once with the probe name and field, never throw.
- Slot fallback chain: world theme -> main-menu theme -> `AssetProbe` (`Resources.FindObjectsOfTypeAll<TMP_FontAsset>()` / `Material` by name, "Erika Ormig SDF" and its material variants, UIC 3.2) -> `ThemeFallback` (TMP default font, grey/orange palette, generated rounded sprites).
- `ThemeLevel { Fallback, MenuOnly, Full }` is exposed so a window opened before capture finished shows with the fallback look and restyles itself on `Changed` (restyle = re-run colour/font application over the control components, not a rebuild).

How a stamped control behaves natively:

- `KitButton` replicates `TextButtonColorEffectTMP`: text colour default/hover/press/disabled from the theme, `PointerEnter/Exit/Down/Up`, plus the theme's hover and press `SoundInfo.PlayMisc(ourRootObject)`. Pass an explicit parent: `MiscAudioPlayer.PlaySound*` otherwise uses `PlayerLocator.instance.gameObject` (`G/MiscAudioPlayer.cs:43,51`), which may not exist in the main menu (unverified). Set `IgnoreListenerPause = true` on the copied `SoundInfo`: the pause menu sets `AudioListener.pause` (`G/PauseMenu.cs:~PauseMenuOpen`).
- `KitCycle` replicates `OptionsButton`: row hover colour, left/right arrow buttons, click anywhere on the row = next (`OptionsButton.OnPointerClick`), `Joystick1Button4/5` step left/right like `PauseNavigationHandler` (`G/PauseNavigationHandler.cs:~119-123`). Toggle = cycle with two options.
- `KitSlider` is a stock `Slider` with the theme's sprites and a number readout.
- Selected/focus visual for the controller = the hover visual (the game fakes pointer enter, UIC 5.1).

What to clone instead (entry buttons only): the template button's `TextButtonColorEffectTMP` clone must have `onSelected/onUnselected` (private `Relay`) nulled via a cached `FieldRef`, `onClick` replaced with a fresh event (clears persistent scene calls), localisation components destroyed (NR `StripLocalization`), `HoverSoundEffect.effectSound` left as is only because the clone is a sibling in the same scene.

### F.4 Public API

Facade (`UiKit`), all taking an owner (`ObenMod` or `ModFeature`, both `ITracker`):

```csharp
UiWindow UiKit.Open(ITracker owner, string id, string title, Action<UiBuilder> build, WindowOptions? o = null);
void     UiKit.Confirm(ITracker owner, string title, string text, Action onYes, string yes = "OK", string no = "Cancel");
void     UiKit.Toast(string title, string text, bool silent = false);       // game's toast in world, stamped one in menu
IDisposable UiKit.Tooltip(Component target, string title, string details);  // hover tooltip on any control
UiTheme  UiKit.Theme { get; }   event Action UiKit.ThemeChanged;
ModMenuApi UiKit.Menu { get; }
```

Window options: `Dock` (Center, Left, Right, Full), width/height share, `CloseOnLevelChange` (default true, bound to `GameEvents.LevelChanging`/`Loading`), `PauseGame` (off by default; co-op will want it off), `Modal`.

Builder (`UiBuilder`, instance, nested builders for rows/sections, NK style; handles, NR style):

```csharp
ui.Header("Player");  ui.Label("text");  ui.Muted("hint");  ui.Separator();
ui.Section("Needs", s => { ... });            // titled group
ui.Row(r => { r.Button("Reset", Reset); r.Button("Close", win.Close, primary: true); });
ToggleHandle  ui.Toggle(string label, bool value, Action<bool> onChange, string tip = null);
CycleHandle   ui.Cycle(string label, string[] options, int index, Action<int> onChange);   // the game's dropdown idiom
SliderHandle  ui.Slider(string label, float min, float max, float value, Action<float> onChange, string format = "0.##");
FieldHandle   ui.TextField(string label, string value, Action<string> onChange, bool onEnd = false);
KeyBindHandle ui.KeyBind(string label, KeyCode value, Action<KeyCode> onChange);
ui.Button(string label, Action onClick, bool primary = false);
UiBuilder ui.Scroll();                         // child builder for the scrolling content
ListHandle<T> ui.List<T>(IReadOnlyList<T> items, Action<UiBuilder, T> row, Action<T> onPick = null);  // pooled rows
TabsHandle ui.Tabs(string[] names, Action<int, UiBuilder> build);
```

Every handle has `Set(value)` (no callback), `Enabled`, `Visible`, and `Bind(Func<T>)` to poll at the kit's 4 Hz refresh instead of per-mod timers. Callbacks are wrapped (`try/catch` -> log with the owner's logger and keep the window open; only a failing *build* closes it, NR `Fault` model).

Example: a settings window.

```csharp
public sealed class CheatsMod : ObenMod
{
    private ConfigFile<Settings> _cfg;
    public override void OnLoad()
    {
        _cfg = LoadConfig<Settings>();
        Track(Hotkey.Register(KeyCode.F6, Toggle));          // framework helper, unrelated to the kit
    }
    private UiWindow _win;
    private void Toggle()
    {
        if (_win != null) { _win.Close(); return; }
        _win = UiKit.Open(this, "cheats.settings", "Cheat settings", ui =>
        {
            var s = _cfg.Value;
            ui.Header("Player");
            ui.Toggle("God mode", s.God, v => { s.God = v; _cfg.Save(); }, tip: "Health never drops.");
            ui.Slider("Move speed", 0.5f, 4f, s.Speed, v => { s.Speed = v; _cfg.Save(); }, "0.0x");
            ui.Cycle("Weather", WeatherNames, s.Weather, i => Weather.Set(i));
            ui.KeyBind("Open key", s.Key, k => { s.Key = k; _cfg.Save(); });
            ui.Row(r => { r.Button("Reset", ResetAll); r.Button("Close", _win.Close, primary: true); });
        });
        if (_win != null) _win.Closed += () => _win = null;
    }
}
```

Example: a mod-menu page (shown under MODS, from the main menu and from the pause menu).

```csharp
UiKit.Menu.AddPage(this, new ModPage("Co-op")
{
    Order = 30,
    Build = ui =>
    {
        ui.TextField("Server address", _addr, v => _addr = v);
        ui.Button("Host", Host, primary: true);
        _status = ui.Label("");
    },
    Refresh = () => _status.Text = Coop.StateText,   // 4 Hz while visible; no allocation if text unchanged
});
UiKit.Menu.AddSettings(this, _cfg);                  // auto-built editor, shows under "Settings" for this mod
```

`ModMenuApi`: `AddPage(owner, ModPage)`, `AddSettings(owner, ConfigFile<T>)`, `Open(string page = null)`, `Close()`, `IsOpen`. Page lists are owner-scoped lists (NK `OwnedList` shape); on `owner` unload the page goes, and if it was selected the window falls back to the first page.

`ModPage { Title, Order, Build(UiBuilder), Refresh(), OnHide(), RebuildRequested }`; same lifecycle as NK `UiPage`, but callbacks instead of subclassing so a mod does not need a class per page.

### F.5 Input and focus implementation

- `InputLock` (one instance): `Acquire(ITracker owner, bool cursor) : IDisposable`. Count holds; on first hold in a world scene call `GameController.instance.ControlsDisabled(rootGo, ShowCursor: true)` inside try/catch; on last release `ControlsEnabled(rootGo)` through the NR camera-flag save/restore (`NR/CoreInput.cs:84-103`) using cached `AccessTools.FieldRefAccess` on `ThirdPersonCameraController.moveDisabled/firstpersonMoveDisabled`. Unlock is also called from owner dispose, `LevelChanging`, and the kit's own `Dispose`. If `GameController.instance` is Unity-null at release time the hold is just dropped (the lists died with the scene).
- In the main menu: no `ControlsDisabled`. `MainMenuHold` sets `MainMenuNavigationHandler.Instance.ControlsEnabled = false` per owner (port NR `SetHold`, `NR/Api/MainMenuButtons.cs:446-468`), restores the previous value on the last release and re-selects the MODS button. Re-assert in `KitTick` only while held, one bool check per frame.
- Gates (`InputGates`, Harmony category "ObenUIKit.gates", unpatched with the mod's Harmony): prefix `PauseMenu.Pause()` -> `false` while `WindowStack.Any`; prefix `PauseNavigationHandler.Update()` (private, no args) -> `false` while `WindowStack.Any`. If either patch fails to apply, log once and mark `UiKit.PauseGateOk = false`; the MODS pause entry is then not added (a window opened from the pause screen would click Resume on Enter).
- Esc/back: `KitTick` (LateUpdate is fine now, the gate above already stopped the pause menu) reads `InputManager.instance.GetKeyDown("Pause Menu")` and `"Menu"` (pad B) through a cached `Func<string,bool>`; closes the top window unless a `KitField` is focused (a counter set by the field's `onSelect/onDeselect`, no `GetComponentsInChildren`). Close on the *next* frame to match the console's `Closelate` trick (UIC 6.1) so the same press cannot reach a game handler running later in the frame.
- Navigation (`UiNav`): focus ring over the visible `Selectable`s of the top window, ordered by a column/row index each control records when built (no per-frame sorting; rebuilt on structural change only). Up/down moves with repeat 0.3 s then 0.15 s (same as the game's handlers, UIC 5.1); left/right adjusts cycles and sliders; `Return`/`X`/pad A activates; bumpers (`Switch Tab Left/Right` and `Joystick1Button4/5`) switch tabs; auto-scroll to focus (port `ModUi.ScrollTo`, `NR/Ui/ModUi.cs:566-578`). Focus uses `EventSystem.SetSelectedGameObject` so `Selectable` highlight works for mouse and pad alike; the kit never fakes pointer events on game objects.
- Mouse while the game thinks a controller is active: see C.3.
- Cursor re-lock: the world camera script may re-lock the cursor; `ControlsDisabled`'s `ShowCursor` list keeps it unlocked (`G/GameController.cs:285-311`), no per-frame `ApplyCursor` like NK needed unless testing shows otherwise (unverified).
- EventSystem: `KitRoot` ensures one exists (the game's scenes have one, UIC 3.5); if it creates one it destroys it on dispose (NR `UiEvents` refcount).

### F.6 Mod menu and entry points

- **Main menu MODS**: port `MainMenuButtons` (`NR/Api/MainMenuButtons.cs`) as `MainMenuEntry` + `MainMenuNav`, with these changes: cached `FieldRef`s for the grid buttons and for `MainMenuNavigationHelper.CurrentObject`; no per-frame `ForeignSum` (rebuild only on `GameEvents.MainMenu` and when `row.childCount` changes, checked in the same tick as the hold); one owned list instead of statics; the clone is parented in the menu scene and removed with the `UiCloning.Remove` pattern (detach, hide, destroy, `NK/Ui/UiCloning.cs:31-38`) in `Dispose`. The 4 postfixes are in a Harmony category so they are unpatched on unload. Open handler: `ModMenu.Open()` + `MainMenuHold`.
- **Pause menu MODS**: new (neither reference has exactly this). In `GameEvents.WhenWorldReady` (PauseMenu exists in gameplay scenes): template = `PauseMenu.instance.resumeMenuButton` (public, `G/PauseMenu.cs:66`; clone that rather than `saveMenuButton`, which `DisableSave` hides, `:472-476`). Parent = the template's parent, which UIC 4.3 says must be a *direct child of the pause main page* (`PauseNavigationHandler.main`, `G/PauseNavigationHandler.cs:26`) so `InitializeSlotArrays` (`:490`, runs in `OnEnable` on every pause open) picks it up as a navigable `Button` for free. Verify at runtime that `template.parent == main.transform`. Insert above "Quit"-style buttons by sibling index. Positioning is unverified: if the page has a `VerticalLayoutGroup` nothing to do; otherwise offset from the last button by the pitch of the two lowest ones (NK did a hand offset, `PauseMenuMods.cs:47-67`). Click: `ModMenu.Open()`; the `InputGates` keep the pause screen quiet. Closing the MODS window returns to the pause screen as it was (the pause state never changed).
- Not doing: NR's tab inside the Tab menu (`ModMenuTab`): it mutates a private array, invents enum values and needs two navigation patches (E.1.7).
- **Window**: `ModMenuWindow` = Full or large Center window: sidebar of pages (owner-registered, sorted by `Order` then title), body scroll, footer with key hints from `InputManager.instance.GetKeyCodeDisplayText` (UIC 2.5). Remembers the selected page across kit reload (`ReloadState.Put/Take`, pattern at `NK/Menu/ModMenu.cs:106-124`).
- **Built-in pages**:
  - Mods: from `ModRegistry.Mods` (`ObenLoader/Mods/ModRegistry.cs`): name, version, `ModInfo.State`, `Error`, `CanHotReload`, enable/disable switch (`ModRegistry.Switches.cs`), load/unload/reload buttons, features list from `ModFeatures.Of(mod)` with switches (`ModFeature.AppliesWhen` as a note). Port the structure of `NK/Menu/Pages/ModsPage.cs`.
  - Settings: per mod, its `AddSettings` editors and its pages, port `NK/Settings/ConfigEditor.cs` logic (cached per type, attributes for range/label/hidden) but build kit controls.
  - What's New: from the changelog generated from git history (AGENTS.md rule 8). Format and source still to be defined with that tooling.
  - Errors: from `ObenLoader/Logging/LogBuffer.cs`/`LogHub`: errors/warnings with count, expandable stack, copy to clipboard. Port `NK/Menu/Pages/ErrorsPage.cs` and `StackText.cs`.

### F.7 Clean up on hot reload and unload

- One `KitRuntime` per mod instance; `UiKitMod.OnUnload` calls `Dispose()` which, in this order: closes all windows (fires `Closed` so dependent mods can drop handles), releases all `InputLock`/`MainMenuHold` holds, removes the MODS entries (detach+destroy), unpatches (the mod's Harmony does it), destroys the DDOL root (all canvases, the `KitTick` behaviour), destroys generated sprites/textures (`UiSprites.Dispose`, fixing NR E.1.3) and `UiSounds` copies, nulls the theme.
- Everything is created through `Track` (loader `ObenMod.CreateObject`/`Track`), so a thrown exception half-way through `OnLoad` still cleans the parts that exist.
- No `static` event subscriptions in static constructors (NK E.2.6): subscribe in `OnLoad` through `GameEvents.X.Subscribe(this, ...)` which unsubscribes on unload.
- Dependents: windows and pages registered by mod X are owned by X. If the *kit* reloads, the loader's cascade reloads dependents (MEMORY, hot reload), their `OnLoad` re-registers pages, and `ReloadState` reopens the menu where it was. If only X reloads, its windows/pages vanish (owner dispose) and come back from X's `OnLoad`; a window X wants to keep across reload uses `ReloadState` itself.
- Cloned entry buttons from an old kit copy are removed in `Dispose`. As NK notes (`UiCloning.cs:27-37`), `Destroy` is deferred to the end of the frame, so the new copy running in the same frame could still see the old button; detach and hide first, and name the clones with a per-instance token so the new copy can ignore stale ones.
- Do not leave `Time.timeScale` or `AudioListener` changes (the kit does not pause by default).
- Tests during development: reload the kit with a window open, a text field focused and the pause screen showing; the controls must come back, cursor locked, no duplicate MODS button, no duplicate Harmony patches.

### F.8 What to port from where

NR code is MIT (NomadsReach): ported files keep a short header "Adapted from ObenseuerCore (MIT), (c) NomadsReach" and the notice goes in `THIRD-PARTY-NOTICES.md` (MEMORY decision). NK code is ours.

| Piece | From | Notes |
|---|---|---|
| `InputLock` camera-flag restore | NR `CoreInput.cs:84-103` | Credit NomadsReach. Replace uncached reflection with `FieldRefAccess`; add try/catch around `ControlsDisabled` |
| `KeyCapture` | NR `Ui/ModInput.cs:125-181` | Credit. Drop `UiInput.Engage` coupling; use the kit lock |
| Text-field focus tracking | NR `ModFieldFocus` `:83-123` | Credit. No `writeLock` |
| Main-menu button | NR `Api/MainMenuButtons.cs` | Credit. Cache reflection, owned list, no per-frame loop work, separate nav file |
| Field rows, cycle, slider, scroll page, scrollbar | NR `ModUi.cs:104-233, 377-441, 517-599` | Credit. Rewrite fills with theme sprites/colours/sounds; keep layout scheme (fixed heights, 45/55 shares) |
| Handles (`Set` without notify, `Coalesce<T>`) | NR `Ui/ModControls.cs` | Credit. `Coalesce` drops repeated slider callbacks |
| Rounded-rect sprite generator | NR `UiSprites.cs` | Credit. Fallback only; add `Dispose` |
| Focus navigation order/scroll-to | NR `ModMenuNav.cs`, `ModUi.ScrollTo` | Credit. Rewrite smaller (<=260 lines): no hard-coded test keys, use column index per control |
| Window ordering/escape/close reasons | NR `ModWindow.cs` | Credit. Replace string `ModId` with `ITracker` owner; keep `WindowCloseReason` values |
| Builder shape (Row/Column/Panel/Scroll, `Size`, partial files) | NK `UiBuilder*.cs` | Ours. Port to TMP; return handles instead of raw uGUI components |
| Owner-scoped removal (`Clone/Remove`) | NK `UiCloning.cs` | Ours |
| `OwnedList<T>` page lists | NK `ModMenu.cs:28-54` | Ours. Check whether it already lives in ObenFramework/loader before copying |
| Tab/page lifecycle (`Build/Refresh/OnHide/Rebuild`, 4 Hz) | NK `UiPage`, `UiTabbedWindow` | Ours |
| Mods, Settings, Errors pages, `ConfigEditor`, `StackText` | NK `Menu/Pages/*`, `Settings/ConfigEditor.cs` | Ours. Text -> TMP, data sources -> ObenLoader/ObenFramework |
| Scroll-bar recolouring idea | NK `UiScrollbarStyle` | Replace with theme sprites; Obenseuer's scrollbars (if any) come from the console `ScrollRect`/inventory lists, unverified |
| Toast stack | NK `Notifications/*` | Only as the main-menu fallback; in world use the game's `Notifications` |
| Confirm arm-then-expire idea | NR `ModMenuActionRow.cs` | Only for destructive actions (unload mod); use `Dialogs.Confirm` otherwise |
| Not porting | NR `ModelPreview` (491 lines, 8 save/restore dictionaries), `ModMenuTab`, WebUi hooks, BepInEx config list, NK pause CONTROLS column cloning | Out of scope or too brittle |

### F.9 First things to do (checks I could not do read-only)

1. Dump the main menu and pause menu hierarchies through the dev bridge (`oben eval` or a ScriptRunner script): for the five main buttons, `languageButton`, `fovSlider`, `PauseMenu.resumeMenuButton` and `DeveloperConsole.InputField`, print component types, `TMP_Text.font.name`, `fontSharedMaterial.name`, sprite names, `Image.type`, and the private colours. This confirms the font/material names (UIC 3.2 lists them from raw asset strings only) and which sprites exist.
2. Check that `resumeMenuButton.transform.parent == PauseNavigationHandler main` and whether that page uses a LayoutGroup.
3. Check `EventSystem.current.currentInputModule` type (UIC 1) before relying on `Selectable` keyboard navigation; the kit's own `UiNav` does not need it, but `Button` highlight on submit does.
4. Check `PlayerLocator.instance` in the main menu (`MiscAudioPlayer.PlaySound` parent) and whether `AudioListener.pause` mutes a `SoundInfo` with `IgnoreListenerPause = true`.
5. Check whether captured sprites/materials survive a scene change without `DontUnloadUnusedAsset`.
6. Is the `unknown` main menu button visible? NR uses it as the anchor for the new row (`MainMenuButtons.cs:136,372`); UIC 5.2 says unverified.
7. Verify `MainMenuNavigationHandler.ControlsEnabled` stays false for the whole time a kit window is open (NR re-asserts it every frame; maybe the game flips it).

### F.10 Biggest pitfalls (short list)

- Cloned game widgets drag in scene references (`Relay`, `AudioSource`, animators, persistent `UnityEvent`s, `SavableScript` GUIDs); keep clones in their own scene, stamp everything else.
- `PauseNavigationHandler.Update` is ungated and `PauseMenu.Pause()` is reachable from Esc/pad B even with `keysDisabled`; both need Harmony gates before a kit window may open over the pause screen.
- `ControlsDisabled` throws outside gameplay scenes and `ControlsEnabled` clears other systems' camera locks; guard and restore.
- `writeLock` is a notes-panel feature; do not use it for text fields.
- The font alone is not "native": material variants, colours, sprites and sounds matter; capture them from live components.
- Main menu and pause menu are different scenes; one lazy capture is not enough.
- Stale singletons are Unity-null; no `?.` and no `??` on them.
