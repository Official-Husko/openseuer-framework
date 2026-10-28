// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
using HarmonyLib;
using ObenFramework.Events;
using ObenFramework.World;
using ObenLoader;

namespace ObenFramework.NewGames;

/// <summary>A tutorial prompt changed. See <see cref="TutorialGuide.StepChanged"/>.</summary>
public readonly struct TutorialStepChange
{
    internal TutorialStepChange(TutorialStep previous, TutorialStep step)
    {
        Previous = previous;
        Step = step;
    }

    public TutorialStep Previous { get; }

    /// <summary>The prompt shown now; <see cref="TutorialStep.None"/> once the player did what it asked.</summary>
    public TutorialStep Step { get; }
}

/// <summary>
/// The game's tutorial prompts (look around, move, interact, open the menu, then inventory, map and tasks,
/// and later the tenement, building, cleaning, head lamp and reload prompts). The game keeps them in its
/// <c>Tutorial</c> component, one flag per prompt, saved with the game.
/// </summary>
public static class TutorialGuide
{
    private static readonly AccessTools.FieldRef<Tutorial, bool> ActiveFlag =
        AccessTools.FieldRefAccess<Tutorial, bool>("TutorialActive");
    private static readonly AccessTools.FieldRef<Tutorial, bool> Look =
        AccessTools.FieldRefAccess<Tutorial, bool>("LookAroundTutorial");
    private static readonly AccessTools.FieldRef<Tutorial, bool> Move =
        AccessTools.FieldRefAccess<Tutorial, bool>("MoveTutorial");
    private static readonly AccessTools.FieldRef<Tutorial, bool> Use =
        AccessTools.FieldRefAccess<Tutorial, bool>("UseTutorial");
    private static readonly AccessTools.FieldRef<Tutorial, bool> Menu =
        AccessTools.FieldRefAccess<Tutorial, bool>("MenuTutorial");
    private static readonly AccessTools.FieldRef<Tutorial, bool> Inventory =
        AccessTools.FieldRefAccess<Tutorial, bool>("InventoryTutorial");
    private static readonly AccessTools.FieldRef<Tutorial, bool> Map =
        AccessTools.FieldRefAccess<Tutorial, bool>("MapTutorial");
    private static readonly AccessTools.FieldRef<Tutorial, bool> Task =
        AccessTools.FieldRefAccess<Tutorial, bool>("TaskTutorial");
    private static readonly AccessTools.FieldRef<Tutorial, bool> Tenement =
        AccessTools.FieldRefAccess<Tutorial, bool>("TenementTutorial");
    private static readonly AccessTools.FieldRef<Tutorial, bool> TenementUsed =
        AccessTools.FieldRefAccess<Tutorial, bool>("tenementUsed");
    private static readonly AccessTools.FieldRef<Tutorial, bool> Building =
        AccessTools.FieldRefAccess<Tutorial, bool>("BuildingTutorial");
    private static readonly AccessTools.FieldRef<Tutorial, bool> Cleaning =
        AccessTools.FieldRefAccess<Tutorial, bool>("CleaningTutorial");

    private static TutorialStep _last;
    private static bool _watching;

    /// <summary>
    /// The prompt changed: raised from the game's own <c>Tutorial.Update</c>, so it follows the game's timing
    /// (a short pause between prompts of the opening sequence). Only checked while somebody listens.
    /// Each handler runs in its own try/catch.
    /// </summary>
    public static readonly WorldEvent<TutorialStepChange> StepChanged = new WorldEvent<TutorialStepChange>(nameof(StepChanged));

    /// <summary>The scene has the tutorial component (every gameplay scene does).</summary>
    public static bool IsAvailable => global::Tutorial.instance != null;

    /// <summary>A tutorial prompt is up right now.</summary>
    public static bool Active
    {
        get
        {
            var tutorial = global::Tutorial.instance;
            return tutorial != null && ActiveFlag(tutorial);
        }
    }

    /// <summary>The prompt shown right now.</summary>
    public static TutorialStep Step
    {
        get
        {
            var tutorial = global::Tutorial.instance;
            return tutorial != null ? TutorialSteps.StepOf(Read(tutorial)) : TutorialStep.None;
        }
    }

    internal static void Init(ObenMod framework)
    {
        _watching = false;
        GameEvents.SceneLoaded.Subscribe(framework, _ => _watching = false);
    }

    internal static TutorialFlags Read(Tutorial t) => new TutorialFlags
    {
        Look = Look(t),
        Move = Move(t),
        Use = Use(t),
        Menu = Menu(t),
        Inventory = Inventory(t),
        Map = Map(t),
        Task = Task(t),
        Tenement = Tenement(t),
        TenementUsed = TenementUsed(t),
        Building = Building(t),
        Cleaning = Cleaning(t),
        HeadLight = t.HeadLightTutorial,
        Reload = t.PrimaryReloadTutorial || t.AuxiliaryReloadTutorial,
    };

    // Called every frame by the Update postfix, so it must not allocate. The first look only records.
    internal static void Observe(Tutorial tutorial)
    {
        if (!StepChanged.HasSubscribers)
        {
            _watching = false;
            return;
        }
        var now = TutorialSteps.StepOf(Read(tutorial));
        if (!_watching)
        {
            _watching = true;
            _last = now;
            return;
        }
        if (now == _last)
            return;
        var change = new TutorialStepChange(_last, now);
        _last = now;
        StepChanged.Raise(change);
    }
}
