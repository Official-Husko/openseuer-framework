# Cheat Menu

`mods/CheatMenu` is a window of cheats. Press **Insert** to open it, or open the **MODS** menu (main menu or pause menu) and pick the **Cheats** page. Both show the same controls. It needs the ObenFramework and ObenUIKit mods.

Most cheats need a loaded game. At the main menu the sections say "Load a game to use this." and show no controls. Whatever a cheat did, or why it couldn't, is written on the grey line at the top of the window.

## Opening and closing

- **Insert** opens the window and **Insert** or **Esc** closes it. The window locks the game's own controls while it is open, so you can't walk around behind it.
- The key only works when nothing else has the keyboard: no other mod window, no F1 console, no pause menu, inventory or dialogue, no text field with focus and no key being rebound in the options.
- To use another key, edit `config.json` in the mod's folder (`Mods/CheatMenu/config.json`) and set `Key` to a Unity key name such as `Home`, `F2` or `Delete`. The game itself doesn't use F2 to F10, Insert, Delete, Home, End, Page Up and Page Down. An unknown name is reported in the log and Insert is used.

## Player

| Control | What it does |
| --- | --- |
| God mode | The game's god mode: you can't die. Health still drops to 0. It is saved with the game. |
| Refill needs | The console's `clear_needs`: full health, no hunger, thirst, tiredness or cravings, clean. |
| Full heal | Health to 100, bleeding stopped, nothing left to regenerate. Needs are left alone. |
| Noclip | The console's `noclip`: fly through walls with the movement keys. Hold Walk to go faster. Moving up and down uses the game's Up and Down actions (not bound by default) or the controller bumpers. |
| Move speed | Scales walking, running, swimming, sneaking and strafing from 0.5x to 5x. It is remembered between sessions (`MoveSpeed` in the mod's config.json) and put back on each new level. |
| Teleport spot | Pick spot 1, 2 or 3. **Save here** stores your position and facing; **Go there** teleports you. A spot belongs to the scene it was saved in, so it won't take you from the street to a spot saved in your flat. Spots are kept in `config.json` and survive restarts. |

## Money

Shows your coins and Real Money. **+100 coins**, **+1000 coins**, **+10 Real Money** and **+100 Real Money** add to your wallet the way a reward does, so the HUD and the coin sound react. **Set both to zero** takes everything away.

## Items

Pick an item from a short list (Lock Pick, Bandage, Water, Rye Bread, Canned Meat, Coffee, Painkillers, Vodka, cigarettes, Lighter, Flashlight, Crowbar, and some crafting material) and press **Give 1** or **Give 10**. The items go into your inventory; anything that doesn't fit is dropped at your feet. For other items use the F1 console: `find_item <name>` then `add_item <id> [amount]`.

## Time and weather

- The line at the top of the section shows the day and the time.
- **Hour** and **Set the time** jump to a whole hour (00:00 to 23:00) on the current day. Drag the slider, then press the button.
- **Time speed** runs the clock stopped, at half speed, normal, or up to 30x. Sleeping and waiting reset it to normal.
- **Weather** switches to any weather the level has, fading over a few seconds. It is only shown where the level has weather; interiors don't.

## Crime

**Reset crime** clears your crime and wanted level (the console's `reset_crime`).

## For mod authors

The mod is a small example of the kit: the hotkey is read with `Input.GetKeyDown` in `OnUpdate`, the window and the page share one build function (`Sections/CheatPanel.cs`), and each cheat is a framework call (`PlayerState`, `PlayerVitals`, `PlayerTeleport`, `Wallet`, `PlayerItems`, `GameClock`, `GameWeather`) or one of the game's own public routines (noclip, reset crime). See [framework/uikit.md](framework/uikit.md) and the other guides in [framework/](framework/).
