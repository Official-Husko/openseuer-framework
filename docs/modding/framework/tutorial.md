# Tutorial prompts

`TutorialGuide` (`src/ObenFramework/NewGames/Tutorial/`, namespace `ObenFramework.NewGames`) tells a mod which tutorial prompt the game is showing and when it changes.

The game's `Tutorial` component (one per level, saved with the game) keeps one flag per prompt. A new game runs the opening sequence: look around, move, interact, open the menu (which unlocks the first door), then inventory (which unlocks the exit door), map and tasks (which drops the package). Later prompts are the tenement menu, the building menu, cleaning mode, the head lamp and reloading. A load hides the prompt that was up (`TutorialActive` goes false) and drops the head lamp and reload prompts; the tenement prompt comes back if it was never used.

```csharp
using ObenFramework.NewGames;

TutorialGuide.StepChanged.Subscribe(this, change =>
{
    if (change.Previous == TutorialStep.Menu && change.Step == TutorialStep.None)
        Log.Info("The player opened the menu for the first time.");
});

TutorialStep now = TutorialGuide.Step;
bool showing = TutorialGuide.Active;
```

| Member | Meaning |
|---|---|
| `Step` | the prompt the game is waiting on (`TutorialStep`: `None`, `LookAround`, `Move`, `Interact`, `Menu`, `Inventory`, `Map`, `Tasks`, `Tenement`, `Building`, `Cleaning`, `HeadLight`, `Reload`) |
| `Active` | a prompt is on screen (the game's `TutorialActive`) |
| `StepChanged` | `TutorialStepChange` with `Previous` and `Step` |
| `IsAvailable` | the scene has the tutorial component |

- When several flags are on, the step is the one `Tutorial.Update` checks first: the opening sequence, then the tenement prompt (until it was used), then building, cleaning, head lamp and reload (`TutorialSteps`, unit tested).
- `StepChanged` is raised from a postfix on `Tutorial.Update`, so it follows the game's own timing (the opening sequence waits two seconds between look, move, interact and menu, during which the step is `None` and the old prompt stays on screen; map leads straight on to tasks). The postfix returns at once while nobody listens and allocates nothing. The first frame after subscribing, or after a scene load, only records the current step.
- `Step` and `Active` can disagree. Finishing the building or cleaning prompt hides whatever prompt is up, so a tenement prompt still waiting stays `Step` with `Active` false, and nothing is on screen until the next load. Pressing the tenement key still finishes it.
- The tutorial's state is saved for the whole game, not per level. After a load or a level change the game shows an unused tenement prompt again once the load has gone through, and `StepChanged` reports `None` → `Tenement` for it. The head lamp and reload prompts don't survive a load.
- Where the prompts come from: the opening level (Interior Start) starts the sequence about three seconds after it fades in (`StartOpenSewer`); the inventory and map prompts come from that level's own triggers (`StartOpenSewer.TutorialInventory` and `TutorialMap`); the tenement prompt from the story; building and cleaning from entering a building area (`BuildingArea`); the head lamp and reload prompts from using those items. A reload prompt with no batteries in the inventory hides itself after 2.5 seconds.

**How it was checked:** a whole run in a new game on 0.5.13 with the input faked from a script (`InputManager.GetKeyDown` and `GetKey` and the camera's `finalInputX`): every opening prompt raised its change in order, and so did the head lamp, building, tenement (before and after a level change) and reload prompts. The game doesn't let the opening room be saved (its Save button is greyed), so the load case went through a level change.
- The bridge command `tutorial.state` shows `available`, `active` and `step`.
