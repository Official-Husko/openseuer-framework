Fixes for bugs in Obenseuer itself. Each fix has its own switch on the **Mods** page of the MODS menu (or `features ObenPatches/<name> on|off` in the F1 console), so if one ever gets in your way you can turn just that one off.

## The fixes

On unless it says otherwise.

| Fix | What was wrong |
| --- | --- |
| Bank interest | Banks say they pay interest on savings and charge it on debts, but never did. Now they do, once a week, and it shows in the bank's history. |
| Renovation materials | With more than one resource storage, a renovation took its materials from every storage that had them, and again from your inventory. Now it takes them once. |
| Farms replant on their days | Farms that replant on certain weekdays did it a day late: the Potato Mine's Monday, Wednesday and Friday replant ran on Tuesday, Thursday and Saturday. Now they run on their days. |
| Item givers refill on time | Cup dispensers and other things that refill over time refilled twice as fast after you entered their level, and leaving the level logged an error. |
| Money in conversations | Money given or taken in conversations dropped the decimals of Real Money amounts. |
| Pocket bank | The pocket bank conversation didn't know when you already had an account. |
| Email dates | Emails from August, September and October showed the wrong month. |
| Robbers and police | NPCs checked their own head when looking for other NPCs, so robbers either saw every police officer or none. |
| Random picks | Random choices in a few places could never pick the last option, and an NPC on a two-point patrol could hang the game. |
| Ambient sounds | Random ambient sounds always played at the low end of their pitch range and never used their last clip. |
| Lamps without a light | Three lamps (in Open Sewer Tenement, on the Pontikka Roof and in the Mallment) had the light script but no light, which logged an error every time you entered. |
| Error before the first save | The main menu logged an error until you had saved once. |
| Screenshots | F11 and F12 never saved a screenshot. Now they go into the Screenshots folder next to the game. |
| HUD after a screenshot | A screenshot without the HUD (F12), taken while the HUD was hidden, made the hide-HUD key need two presses to bring it back. |
| Head lamp prompt | Taking off a hat with a head lamp before ever switching it on left the "Head Lamp" key prompt on screen. |
| Auto-eat while crafting | Eating automatically while crafting ate the last item of a stack twice and then failed, which could also break the Craft button. |
| Lantern candles | Blowing out a candle lantern and lighting it again made the new candle go out when the old one would have ended. |
| Hotel stays | A hotel or pod stay only counted down when you left the hotel's level and came back, so a stay never ran out while you stayed there. |
| NPC sight chance (off by default) | NPC sight ignores its detection chance, so you're always spotted when in view. The fix makes the chance count; it changes how sneaking feels, so it's yours to turn on. |
| Reflection probes (off by default) | A typo stops some reflections from updating while you stand in their area. Off until it has been checked in more places. |

## Found another one?

Tell us with the "Bug in the game itself" form on the [issues page](https://github.com/Official-Husko/openseuer-framework/issues/new/choose), ideally with a [report](Reporting-Bugs) and your save. Please report it to Loiste Interactive on the [Steam forums](https://steamcommunity.com/app/951240/discussions/) too.

For the technical details of every fix, see [known-bugs.md](https://github.com/Official-Husko/openseuer-framework/blob/main/docs/game/known-bugs.md).
