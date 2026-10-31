// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
using System;
using System.Collections.Generic;
using ObenFramework.Events;
using ObenLoader;
using ObenLoader.Runtime;

namespace ObenFramework.NewGames;

/// <summary>
/// Options a mod adds to the New Game screen: a toggle, a choice, a whole number or a decimal. The player
/// picks them before starting; the values are stored with the save (ObenFramework.newgame.json in its mod
/// folder) and read with the <c>Get</c> methods for the rest of that game. A registration ends when its
/// mod unloads; the stored values stay. Register them in <c>OnLoad</c>: an option registered after a game
/// was started has no stored value in that game and reads its default (a warning is logged).
/// See docs/modding/framework/new-game.md.
/// </summary>
public static partial class NewGameOptions
{
    private static readonly NewGameOptionStore Store = new NewGameOptionStore();

    /// <summary>An option was registered or removed. The New Game screen rebuilds its list on this.</summary>
    public static readonly GameEvent ListChanged = new GameEvent(nameof(ListChanged));

    /// <summary>The registered options in registration order. A copy.</summary>
    public static List<NewGameOption> Options => Store.All();

    /// <summary>Adds an Enabled/Disabled option. Returns null when the mod already has an option with this key.</summary>
    public static NewGameOption RegisterToggle(ObenMod owner, string key, string title, string description, bool defaultValue,
        NewGamePlacement placement = NewGamePlacement.BelowDifficulty) =>
        Add(owner, NewGameOptionRules.Toggle(Id(owner), key, title, description, defaultValue, placement));

    /// <summary>
    /// Adds a choice between named entries. <paramref name="ids"/> are stored (keep them stable),
    /// <paramref name="titles"/> are shown. Returns null when the key is taken.
    /// </summary>
    public static NewGameOption RegisterChoice(ObenMod owner, string key, string title, string description, string[] ids, string[] titles,
        int defaultIndex, NewGamePlacement placement = NewGamePlacement.BelowDifficulty) =>
        Add(owner, NewGameOptionRules.Choice(Id(owner), key, title, description, ids, titles, defaultIndex, placement));

    /// <summary>Adds a whole number from <paramref name="min"/> to <paramref name="max"/>. Returns null when the key is taken.</summary>
    public static NewGameOption RegisterInteger(ObenMod owner, string key, string title, string description, int min, int max,
        int defaultValue, NewGamePlacement placement = NewGamePlacement.BelowDifficulty) =>
        Add(owner, NewGameOptionRules.Integer(Id(owner), key, title, description, min, max, defaultValue, placement));

    /// <summary>
    /// Adds a decimal from <paramref name="min"/> to <paramref name="max"/> in steps of <paramref name="step"/>
    /// (at most 1000 steps; the default must be on a step). Returns null when the key is taken.
    /// </summary>
    public static NewGameOption RegisterFloat(ObenMod owner, string key, string title, string description, float min, float max,
        float step, float defaultValue, NewGamePlacement placement = NewGamePlacement.BelowDifficulty) =>
        Add(owner, NewGameOptionRules.Float(Id(owner), key, title, description, min, max, step, defaultValue, placement));

    /// <summary>The registered option, or null.</summary>
    public static NewGameOption Find(string modId, string key) => Store.Find(modId, key);

    /// <summary>
    /// What the New Game screen shows for an option right now: the player's pick, else the default.
    /// Null for an option that isn't registered.
    /// </summary>
    public static string ScreenValue(string modId, string key) => Store.DraftOrDefault(modId, key);

    /// <summary>Sets an option's value on the New Game screen; it is stored when the player starts the game.</summary>
    public static void Choose(string modId, string key, string value) => Store.Choose(modId, key, value);

    /// <summary>The value one step left or right of <paramref name="value"/>, wrapping at the ends.</summary>
    public static string Step(NewGameOption option, string value, bool right) => NewGameOptionRules.Step(option, value, right);

    /// <summary>
    /// What one press of the New Game screen's arrows does: one step, or for a number option with more than
    /// 41 values a twentieth of its range, stopping at the ends before wrapping.
    /// </summary>
    public static string ScreenStep(NewGameOption option, string value, bool right) => NewGameOptionRules.ScreenStep(option, value, right);

    /// <summary>Forgets what was picked on the screen, so every option shows its default again.</summary>
    public static void ResetScreen() => Store.ClearDraft();

    // Set when the player confirmed a new character, until the main menu shows again.
    private static bool _captured;

    internal static void Init(ObenMod framework)
    {
        GameEvents.MainMenu.Subscribe(framework, () => _captured = false);
        // Picks on a New Game screen that was left without starting a game don't carry over past a load.
        GameEvents.Loading.Subscribe(framework, _ => Store.ClearDraft());
    }

    internal static void NewGameCaptured() => _captured = true;

    // The player confirmed the New Game screen: every registered option is stored, picked or default.
    internal static void CaptureNewGame()
    {
        var data = NewGameSave.Data;
        if (data != null)
            data.CopyOptionsFrom(Store.Capture());
        Store.ClearDraft();
    }

    internal static void Reset()
    {
        Store.ClearDraft();
        _captured = false;
    }

    private static NewGameOption Add(ObenMod owner, NewGameOption option)
    {
        if (!Store.Add(option))
        {
            GameEvents.Log.Warn($"{option.ModId} registered the new game option '{option.Key}' twice; the first one is kept.");
            return null;
        }
        WarnIfLate(option);
        owner.Track(Disposable.Create(() =>
        {
            if (Store.Remove(option))
                ListChanged.Raise(GameEvents.Log);
        }));
        ListChanged.Raise(GameEvents.Log);
        return option;
    }

    // An option the running game didn't store when it started reads its default for the whole game; say so
    // when the registration comes too late for that (a mod reloading an option the game stored is fine).
    private static void WarnIfLate(NewGameOption option)
    {
        if (!_captured && Sessions.Current == SessionKind.None)
            return;
        if (NewGameSave.Data?.Get(option.ModId, option.Key) != null)
            return;
        GameEvents.Log.Warn($"{option.ModId} registered the new game option '{option.Key}' after this game was started, so the game " +
            "reads its default. Register new game options in OnLoad.");
    }

    private static string Id(ObenMod owner)
    {
        if (owner == null)
            throw new ArgumentNullException(nameof(owner));
        return owner.Info.Id;
    }
}
