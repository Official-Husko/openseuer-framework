The game repeats some work every frame that it doesn't need to, and some of it makes garbage the game then has to clean up, which shows as small stutters. Performance Fixes cuts that work down. The rule for every fix: the game looks and behaves exactly the same, only the work goes down.

All fixes are on by default. Each has its own switch on the **Mods** page of the MODS menu (or `features PerformanceFixes/<name> off` in the F1 console).

| Fix | What it does |
| --- | --- |
| Inventory refresh | With the inventory open, the game redrew every slot on nearly every frame. Now it redraws when something changes, and at most four times a second for the clock. |
| Looking at things | Pointing at something you can use made garbage every frame. Not anymore. |
| Lighting zones | Checking which lighting zone you're in made garbage on every physics tick. Not anymore. |
| Inventory checks | Counting what's in your inventory and containers reuses its lists instead of making new ones each time. |
| Ambient colour | Area portals share one lookup of the ambient colour instead of each repeating it every frame. |
| Player stats | Your stats no longer make garbage on every tick. |
| Timers | In-game timers no longer make garbage on every tick. |

## Seeing the difference

Type `perf` in the F1 console. It shows the last ten seconds: average frame time, the slowest frames, how much memory was allocated and how many clean-ups ran. `perf 30` looks at the last 30 seconds, `perf reset` starts over.

To compare, switch a fix off, do the same thing for half a minute (open the inventory, walk around a building), run `perf`, then switch it back on and do it again.

The details, and how each fix was checked, are in [performance-fixes.md](https://github.com/Official-Husko/openseuer-framework/blob/main/docs/modding/performance-fixes.md).
