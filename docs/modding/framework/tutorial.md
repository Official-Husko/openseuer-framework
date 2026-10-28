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
| `Step` | the prompt shown now (`TutorialStep`: `None`, `LookAround`, `Move`, `Interact`, `Menu`, `Inventory`, `Map`, `Tasks`, `Tenement`, `Building`, `Cleaning`, `HeadLight`, `Reload`) |
| `Active` | a prompt is up (the game's `TutorialActive`) |
| `StepChanged` | `TutorialStepChange` with `Previous` and `Step` |
| `IsAvailable` | the scene has the tutorial component |

- When several flags are on, the step is the one `Tutorial.Update` checks first: the opening sequence, then the tenement prompt (until it was used), then building, cleaning, head lamp and reload (`TutorialSteps`, unit tested).
- `StepChanged` is raised from a postfix on `Tutorial.Update`, so it follows the game's own timing (the opening sequence waits two seconds between prompts, during which the step is `None`). The postfix returns at once while nobody listens and allocates nothing. The first frame after subscribing, or after a scene load, only records the current step.
- The bridge command `tutorial.state` shows `available`, `active` and `step`.
