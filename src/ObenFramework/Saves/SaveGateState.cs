// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
namespace ObenFramework.Saves;

/// <summary>
/// The bookkeeping behind <see cref="SaveGate"/>, with the clock, the frame counter and the
/// loading screen passed in so it can be tested without the game.
/// </summary>
internal sealed class SaveGateState
{
    // A pending load is dropped after this long even if nothing says it ended, so a load the game
    // abandoned can never leave the gate shut for the rest of the session.
    public const float StuckSeconds = 300f;

    // A save runs inside a single call, so it is "in flight" only for the frame it started in.
    // A save that threw never raises Saved, and this still lets go on the next frame.
    private int _savingFrame = -1;

    private bool _loadPending;
    private bool _loadStarted;
    private int _pendingFrame;
    private float _pendingSince;

    /// <summary>Set when a pending load was dropped because it never finished.</summary>
    public bool DroppedStuckLoad { get; private set; }

    public void SavingStarted(int frame) => _savingFrame = frame;

    public void SavingDone() => _savingFrame = -1;

    public bool IsSaving(int frame) => _savingFrame == frame;

    /// <summary>
    /// A level change or load was announced (<paramref name="started"/> is false) or the new scene
    /// is open and the game began restoring it (true).
    /// </summary>
    public void BeginLoad(int frame, float now, bool started)
    {
        if (!_loadPending)
        {
            _loadPending = true;
            _pendingFrame = frame;
            _pendingSince = now;
        }
        if (started)
            _loadStarted = true;
    }

    public void LoadDone()
    {
        _loadPending = false;
        _loadStarted = false;
    }

    public void Reset()
    {
        LoadDone();
        _savingFrame = -1;
    }

    /// <summary>
    /// Whether a load is in flight. Loads the game abandons (file missing, aborted migration, a
    /// level change that only saved) never raise LoadingDone, so a pending load that has not
    /// started restoring is dropped once the game's own flag is off and its loading screen is idle.
    /// Once restoring has started the game runs it in one go apart from a single frame, so only
    /// the stuck timeout applies.
    /// </summary>
    public bool IsLoading(int frame, float now, bool gameLoadingFlag, bool screenIdle)
    {
        if (!_loadPending)
            return false;

        if (now - _pendingSince > StuckSeconds)
        {
            Reset();
            DroppedStuckLoad = true;
            return false;
        }
        if (!_loadStarted && frame > _pendingFrame && !gameLoadingFlag && screenIdle)
        {
            LoadDone();
            return false;
        }
        return true;
    }

    public void ClearDroppedFlag() => DroppedStuckLoad = false;
}
