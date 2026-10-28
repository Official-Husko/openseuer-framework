# Game Tweaks

`mods/GameTweaks` changes how the game plays, one tweak at a time. Every tweak is a feature of its own and **off until the player switches it on**: in the MODS menu (**Mods** page, the switches under Game Tweaks), with the F1 console (`features GameTweaks/<id> on`), or over the bridge (`features.set mod=GameTweaks id=<id> enabled=true`). The values each tweak uses are on the **Settings** page of the MODS menu (the "Game tweaks" config) and in `Mods/GameTweaks/config.json`. A changed value takes effect at once while its tweak is on.

A tweak that is off has no Harmony patches and no registrations, so it costs nothing. Tweaks that the framework already has a hook for use it instead of patching the game again (needs, crafting, dishwasher). Nothing a tweak does is written into the game's save, except what the game itself keeps (a bigger stack you filled, an order that was cheaper, food that spoiled less).

The tweaks are a port of the `Tweaks*` files and `CameraView` of the reference framework (NomadsReach, MIT); see `docs/research/framework-reference.md`, module M04.

| Id | Tweak | Value on the Settings page (default when switched on) | How |
| --- | --- | --- | --- |
| `Needs` | Needs grow slower or faster | Hunger, Thirst, Hygiene, Alcohol/Mushroom/Smoking/Gambling craving (0.5 each; 0 stops one) | `StatDrain.Set` per need |
| `StackSize` | Bigger (or smaller) stacks | Stack size (2) | postfix on `Item.Stackable` |
| `Spoilage` | Food spoils slower (or faster) | Spoilage (0.5; 0 stops it) | prefix on `ItemPerishabledata.RemoveMinutesFromIntegrity` |
| `CraftingSpeed` | Faster (or slower) crafting | Crafting speed (2) | `CraftingSpeed.Set("*")`, plus a postfix on `BaseCrafting.StartCrafting` for furniture blueprints |
| `NoMinimumSkill` | Recipes don't need a minimum skill | | postfixes on `Recipe.GetProcessTime` and `RecipeInfoUI.CheckSkillSoft`, prefix and finalizer on `Recipe.GetCraftingResult` |
| `DishwasherSpeed` | Faster (or slower) dishwasher | Dishwasher speed (2), on top of the crafting speed | `CraftingSpeed.Set("Dishwasher")` |
| `RenovationCost` | Cheaper (or dearer) renovations | Renovation cost (0.5) | postfixes on `TenementUpgrade.GetPriceStartOC` and `GetPriceFinalRM` |
| `RenovationMoneyOnly` | Renovations need only money | | prefixes on `TenementController.CheckForItem` and `RemoveRequiredItems` |
| `RenovationTime` | Faster (or slower) renovations | Renovation time (0.5), Instant renovations (off) | prefix on `TenementUpgradeProgress.AddUpgrade`, postfix on `TenementUpgrade.GetRequiredTimeHours` |
| `DayLength` | Longer (or shorter) days | Time speed (0.5) | prefix on `AzureTimeController.SetNewDayLength` |
| `ThirdPerson` | Third-person camera | Third person distance (2.5 m), Scroll to zoom (off); the key is on the Hotkeys page (F6) | unlocks `ThirdPersonCameraCollision`; patches on `PlayerCameraTrigger.FirstPersonOnly` and `ThirdPersonCameraCollision.OnSavingGame` |

## Notes per tweak

**Needs.** The framework's `StatDrain` multiplies the game's per-hour rate (already adjusted for difficulty), so the change shows on the next game minute. Multipliers of other mods combine with these. `player.drain` lists them.

**Stack size.** Every stack check reads `Item.Stackable`, so inventory, storages and shops all follow, and liquid containers hold more per slot because more of them fit. Lowering the factor (or switching the tweak off) leaves stacks that are already bigger than the new limit as they are. Items that don't stack stay single.

**Spoilage.** Both ways the game spoils an item end in `RemoveMinutesFromIntegrity`. Integrity already lost stays lost.

**Crafting speed.** Stations go through the framework's process time factor (`docs/modding/framework/crafting.md`); a craft keeps the time it started with. Furniture blueprints at the workbench don't use a recipe, so their build time is divided when the build starts.

**No minimum skill.** The recipe shows and can be crafted whatever the skill, without the "skill too low" time multiplier and fail chance penalty. Skill still lowers the fail chance and raises quality. The skill line in the recipe info stays red; that is the game's display only.

**Renovations.** Costs and the shown hours are scaled where the game reads them, so the upgrade screen, the payment and the framework's `Renovations` agree. The build time of an order is fixed when it is placed: orders under way keep their time, and their progress bar can look odd while the factor differs from when they were ordered. Upgrades the story starts on its own keep their scripted time. "Money only" also keeps the framework's extra inventory sources from being asked for materials.

**Day length.** The game hands the sky controller the length of a day in real minutes every frame (`TimeOfDayAzure.actualSpeed`, 96 minutes in the tenement at speed 1). Dividing it there moves the clock, the timers and every stat tick together, without touching `dayLenghtInMinutes`, which is in the save. Sleeping and waiting still speed time up on top. Two small things keep the unscaled length: the power meters' usage per real second, and where an NPC is put along its path when a level loads.

**Third-person camera.** The game still has its third-person camera but locks it (`disableThirdPerson`, `firstpersonOnly`, both true in the tenement). The hotkey lifts the locks and moves the camera back; going back to first person (or switching the tweak off) puts the game's values back. Camera triggers that force first person and the game's zoom blockers win: the camera goes back to first person and a toast says so. While a save is written the camera briefly looks the way the game left it, so a save made in third person loads in first person, with or without the mod. Third person is a mode the game no longer uses, so the body, its animations and clipping can look rough.

## Checking in game

Load a level (the tenement is fine: `oben call console.exec 'command=new_scene Interior Player Tenement'`), then switch each tweak on with `oben call features.set mod=GameTweaks id=<id> enabled=true` and compare with it off. `oben eval` snippets can go in a script file when the quoting gets awkward.

| Tweak | Check |
| --- | --- |
| `Needs` | `oben call player.drain`: an entry from GameTweaks per need with multiplier 0.5, `combined.Hunger.multiplier` 0.5. Switched off, the entries are gone. |
| `StackSize` | `oben call items.find query=plank`: `stackable` 8 off, 16 on. |
| `Spoilage` | `oben eval "var p = new OS.Items.ItemPerishabledata(); p.RemoveMinutesFromIntegrity(60f); return p.integrity;"`: 0.9 off, 0.95 on. |
| `CraftingSpeed` | `oben call crafting.speed`: a factor with key `*` and speed 2 owned by the GameTweaks feature. |
| `NoMinimumSkill` | With Mashing skill 1, `oben eval "var r = RecipeDatabase.Recipes.First(x => x.ID == 10016); return RecipeInfoUI.CheckSkillSoft(r) + \" \" + r.GetProcessTime(1f, 1f);"` (Carrot mash, minimum 3; its fail chance is 1 with the minimum and 0.16 without): `False 21600` off, `True 7200` on. |
| `DishwasherSpeed` | `oben call crafting.speed`: key `Dishwasher`, speed 2. |
| `RenovationCost` | `oben call renovations.list state=Available`: the Makeshift stairwell goes from 3500 OC / 175 RM to 1750 / 88. |
| `RenovationMoneyOnly` | `oben eval "TenementController.instance.CheckForItem(35010, 9999f)"` (9999 planks): False off, True on. |
| `RenovationTime` | `oben call renovations.list state=Available`: the Makeshift stairwell goes from 24 to 12 hours. |
| `DayLength` | `oben eval "HarmonyLib.Traverse.Create(TimeOfDayAzure.instance.TimeController).Field(\"m_dayLength\").GetValue<float>()"`: 96 off, 192 on (speed 0.5). `oben call clock.get` twice, 30 real seconds apart: about 30 game minutes off, 15 on. |
| `ThirdPerson` | In the tenement press F6: the camera moves behind the player and `oben eval "ThirdPersonCameraCollision.instance.firstperson"` is False; F6 again or switching the tweak off: True, and `disableThirdPerson`/`firstpersonOnly` are True again. Save in third person and look for `"disableThirdPerson":true` and `"firstperson":true` in the save. |

`oben call features.list` shows which tweaks are on.
