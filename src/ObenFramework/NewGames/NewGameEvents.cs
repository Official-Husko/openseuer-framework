using ObenFramework.Events;

namespace ObenFramework.NewGames;

/// <summary>Starting a new game.</summary>
public static class NewGameEvents
{
    /// <summary>
    /// The player confirmed a new character on the New Game screen. Raised in the main menu, right after
    /// the game stored the name and difficulty and before the intro loads; there is no world yet. The game's
    /// first level follows, and <see cref="Sessions.Started"/> fires with <see cref="SessionKind.NewGame"/>
    /// when it is ready. Each handler runs in its own try/catch.
    /// </summary>
    public static readonly GameEvent<NewGameInfo> Started = new GameEvent<NewGameInfo>(nameof(Started));

    /// <summary>The choices of the last new game started in this run of the game, or null.</summary>
    public static NewGameInfo Last { get; private set; }

    internal static void Raise(NewGameInfo info)
    {
        Last = info;
        Sessions.NewGameStarted();
        Started.Raise(info, GameEvents.Log);
    }

    internal static void Reset() => Last = null;
}
