namespace ObenFramework.NewGames;

/// <summary>
/// Works out how a play session began from the order of the game's own signals. A level change
/// (doors, fast travel) is a save followed by a load of that save, so it announces a level change and
/// then a load; a load from the menu announces only the load; a new game announces neither and goes
/// through the intro scene first. No Unity types, so it is unit tested on its own.
/// </summary>
internal sealed class SessionRules
{
    private SessionKind _pending;
    private bool _levelChange;
    private SessionKind _beforeLoad;

    /// <summary>How the running session began; <see cref="SessionKind.None"/> in the main menu.</summary>
    public SessionKind Current { get; private set; }

    /// <summary>A world is loading or loaded that continues the running session (a level change).</summary>
    public bool LevelChangePending => _levelChange;

    public void MainMenu()
    {
        _pending = SessionKind.None;
        _levelChange = false;
        Current = SessionKind.None;
    }

    /// <summary>The player confirmed a new character on the New Game screen.</summary>
    public void NewGameStarted()
    {
        _pending = SessionKind.NewGame;
        _levelChange = false;
    }

    /// <summary>The game announced a level change; a save and a load of that save follow.</summary>
    public void LevelChanging()
    {
        if (_pending == SessionKind.None)
            _levelChange = true;
    }

    /// <summary>The game announced a save load. After a level change announcement it is part of the level change.</summary>
    public void WillLoad()
    {
        if (_levelChange)
            return;
        _beforeLoad = _pending;
        _pending = SessionKind.SaveLoad;
    }

    /// <summary>
    /// An announced level change or save load didn't happen (a missing save, or a door to another spot of
    /// the same level). The running session goes on as if nothing was announced.
    /// </summary>
    public void LoadAborted()
    {
        if (_pending == SessionKind.SaveLoad)
            _pending = _beforeLoad;
        _beforeLoad = SessionKind.None;
        _levelChange = false;
    }

    /// <summary>
    /// A level is ready to play. Returns the kind of the session that starts here, or
    /// <see cref="SessionKind.None"/> when the level continues the running session (a level change) or is the
    /// new game's intro, which comes before its first real level.
    /// </summary>
    public SessionKind WorldReady(bool introScene)
    {
        if (introScene)
            return SessionKind.None;

        if (_levelChange && _pending == SessionKind.None && Current != SessionKind.None)
        {
            _levelChange = false;
            return SessionKind.None;
        }

        var kind = _pending != SessionKind.None ? _pending : SessionKind.Direct;
        _pending = SessionKind.None;
        _levelChange = false;
        Current = kind;
        return kind;
    }

    /// <summary>Puts back the state of an earlier copy (hot reload), or marks the session unknown.</summary>
    public void Resume(SessionKind current, bool worldRunning)
    {
        _pending = SessionKind.None;
        _levelChange = false;
        Current = current != SessionKind.None ? current : worldRunning ? SessionKind.Unknown : SessionKind.None;
    }
}
