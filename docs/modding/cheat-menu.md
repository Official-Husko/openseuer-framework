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
- **Hour**, **Minute** and **Set the time** jump to that time on the current day. Drag the sliders, then press the button. Setting the time raises the framework's `GameClock.HourChanged` once, with the new hour, so it is also a way to test a mod's hour handlers; for `DayChanged`, sleep past midnight.
- **Time speed** runs the clock stopped, at half speed, normal, or up to 30x. Sleeping and waiting reset it to normal.
- **Weather** switches to any weather the level has, fading over a few seconds. It is only shown where the level has weather; interiors don't.

## Crime

**Reset crime** clears your crime and wanted level (the console's `reset_crime`).

## Developer menu

A second window for testing mods and quests, ported from the reference framework's developer menu (`DevMenu*`, NomadsReach, MIT). It is a feature of the mod and **off by default**: switch on "Developer menu" under Cheat Menu on the MODS menu's Mods page, or `features CheatMenu/DevMenu on` in the F1 console. While it is on:

- **F8** opens and closes it (rebind it on the Hotkeys page). It opens in a level and at the main menu, but not over another mod window.
- **F9** shows or hides the watches over the game (see Watches below).
- The MODS menu has a **Developer** page with the same content, and the Cheats window gets an "Open the developer menu" button.

The **Show** row at the top picks the part; only the part on screen is built and read:

| Part | What it does |
| --- | --- |
| Quests | With tasks from mods started, **Made by** shows all tasks, the game's or one mod's (a mod's task ids start with its id). Pick a started task (title and state; the line under it names the objective it is on), set it Active, Finished, Failed or Abandoned; pick an objective and set its state, move its counter (-1, +1, fill, empty) or type a value and press Enter. Tasks made by a loaded mod can also be **reset**, **restarted**, **removed** (asks first), **jumped** to the chosen objective, and a **checkpoint** kept and gone back to for the session. Tasks the current level has loaded but the player hasn't started can be started. Everything goes through `QuestLog` and `QuestAuthoring`, which refuse with a reason (shown on the status line) rather than break the save. |
| Global flags | Filter the save's global flags by name (Enter applies), switch any of up to 30 shown on or off, or set a flag by name. A flag a mod forces says so. |
| Watches | Type a type name and press Enter to list its members, pick one (or type a path such as `currentTimeAndDay.hours`) and press **Watch it**. The value is read again four times a second while the window shows it. The first name is a static member, or an instance member of a game singleton (a type with a static `instance`, read through it). Watches are kept in `Watches` in the mod's config.json, up to 20. **Show them over the game** (or F9) puts every watch, the ones mods added too, in a box at the top left of the HUD, read four times a second while you play; it hides while a window or the pause menu is open, and the choice is kept (`WatchesOnScreen`). A watch that can't be resolved says "can't be read". |
| Session and story | Live lines: scene, world ready, paused; the session kind; the save gate; the clock; how many tasks are started; the story pump's ready gate and the names of the actions waiting in its queue. |
| Player and world | Set any player stat by its console name (pick it, drag the slider, **Set the stat**) or any skill (0 to the game's top level, **Set the skill**). Go to any entrypoint of the level (the tenement has `PlayerTenement_In`, `TenementBasementEntranceIn` and others), or to coordinates: X, Y and Z start at your position (**Where I am** fills them again; a comma works as the decimal mark too). Find any item by part of its name, its id, or `mod:(mod id)` for a mod's items, and give **How many** you set. **Items of removed mods** lists the stacks whose mod isn't loaded (kept as placeholders) with a button per mod that removes them from the inventory and the containers loaded (asks first). |
| NPCs | The NPCs in this scene, the ones up and about first, then by distance (hidden and dead ones greyed). Pick one for its job, state, activity and timetable entry, read live, and: **Go to it** (a step in front of it, facing it), **Bring it here**, **Look at me**, **Follow me**, **Stop following**, **Stop its activity**, **Kill it** (asks first). Moving one needs path finding, which most of the game's NPCs don't have; those say so. Below: the **mod NPCs** in the scene, whichever mod made them, with Go to it, Bring it here and **Despawn** (which also forgets a saved one); **Spawn a copy** of any game character by id, with a first name, in front of you (not saved, gone at the next level change); and the **relationships**: pick one, see its disposition, target and daily drift, and set the disposition with the slider (the target follows, so it stays). |
| Log | The loader's buffer of this session's lines (the last 4096, from mods, the loader and Unity, debug lines included): pick the level (everything, info and up, warnings and errors, errors only) and the source, filter by text (Enter applies), and the list follows new lines as they come while **Follow new lines** is on. Warnings show amber, errors red, the number of extra lines (a stack trace) on the right. Picking a line shows all of it below, with **Copy it**. The files themselves are in `ObenLoader/logs`. |
| From mods | Sections other mods added. |

In the F1 console, `devmenu` opens or closes the menu and `devmenu watches` shows or hides the watches over the game; with the feature off it says how to switch it on.

Every change made from the menu (not keeping a checkpoint) marks the save as dev-modified: `mods/CheatMenu.dev.json` is written next to the save at the next save, the menu shows a note, and the log says so once. A bug report from such a save should mention it.

## For mod authors

The mod is a small example of the kit: the hotkey is read with `Input.GetKeyDown` in `OnUpdate`, the window and the page share one build function (`Sections/CheatPanel.cs`), and each cheat is a framework call (`PlayerState`, `PlayerVitals`, `PlayerTeleport`, `Wallet`, `PlayerItems`, `GameClock`, `GameWeather`) or one of the game's own public routines (noclip, reset crime). See [framework/uikit.md](framework/uikit.md) and the other guides in [framework/](framework/).

Other mods can add to the developer menu (add a project reference to `mods/CheatMenu` and `"CheatMenu"` to the mod.json dependencies):

```csharp
using CheatMenu.Dev;

DevMenu.AddSection(this, "My mod", ui =>
{
    ui.Label("Things to poke while testing.");
    ui.Button("Spawn the test crate", SpawnCrate);
});
DevMenu.AddWatch(this, "crates spawned", () => _spawned.ToString());
```

Both go away when the mod unloads; the same owner and title replaces an earlier one. Mod watches also show in the box over the game. `DevMenu.SaveIsDevModified` tells whether the loaded save was changed from the menu. Sections show under "From mods", watches under "Watches", whether or not the developer menu feature is on (it only decides whether the window can be opened).
