namespace ObenFramework.NewGames;

/// <summary>How the running play session began. See <see cref="Sessions"/>.</summary>
public enum SessionKind
{
    /// <summary>No session: the main menu, or nothing has loaded yet.</summary>
    None,

    /// <summary>A save was loaded, from the main menu or from the pause menu.</summary>
    SaveLoad,

    /// <summary>A new character was made on the New Game screen and its first level loaded.</summary>
    NewGame,

    /// <summary>A level was loaded without a save or a new character, like the console's <c>new_scene</c>.</summary>
    Direct,

    /// <summary>The framework was loaded into a world that was already running, so how it began is not known.</summary>
    Unknown,
}
