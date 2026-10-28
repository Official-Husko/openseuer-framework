// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
namespace ObenFramework.NewGames;

/// <summary>The tutorial prompt the game is showing. The first seven run in order at the start of a new game.</summary>
public enum TutorialStep
{
    None,
    LookAround,
    Move,
    Interact,
    Menu,
    Inventory,
    Map,
    Tasks,

    /// <summary>The tenement menu prompt, shown once the player owns the tenement.</summary>
    Tenement,

    /// <summary>The building menu prompt.</summary>
    Building,

    /// <summary>The cleaning mode prompt.</summary>
    Cleaning,

    /// <summary>The head lamp prompt.</summary>
    HeadLight,

    /// <summary>The reload prompt of a tool or weapon.</summary>
    Reload,
}

/// <summary>The flags the game's <c>Tutorial</c> component keeps, one per prompt. Plain data.</summary>
internal struct TutorialFlags
{
    public bool Look;
    public bool Move;
    public bool Use;
    public bool Menu;
    public bool Inventory;
    public bool Map;
    public bool Task;
    public bool Tenement;
    public bool TenementUsed;
    public bool Building;
    public bool Cleaning;
    public bool HeadLight;
    public bool Reload;
}

/// <summary>
/// Which prompt the flags stand for, in the order <c>Tutorial.Update</c> checks them: the opening sequence
/// first, then the tenement prompt, then the building, cleaning, head lamp and reload prompts.
/// No Unity types, so it is unit tested on its own.
/// </summary>
internal static class TutorialSteps
{
    public static TutorialStep StepOf(in TutorialFlags f)
    {
        if (f.Look) return TutorialStep.LookAround;
        if (f.Move) return TutorialStep.Move;
        if (f.Use) return TutorialStep.Interact;
        if (f.Menu) return TutorialStep.Menu;
        if (f.Inventory) return TutorialStep.Inventory;
        if (f.Map) return TutorialStep.Map;
        if (f.Task) return TutorialStep.Tasks;
        if (f.Tenement && !f.TenementUsed) return TutorialStep.Tenement;
        if (f.Building) return TutorialStep.Building;
        if (f.Cleaning) return TutorialStep.Cleaning;
        if (f.HeadLight) return TutorialStep.HeadLight;
        if (f.Reload) return TutorialStep.Reload;
        return TutorialStep.None;
    }
}
