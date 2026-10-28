// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
namespace ObenLoader.Logging;

/// <summary>
/// Caps how many lines one logger writes per second, so a mod logging every frame can't flood the log
/// file and the game's own Player.log. Lines over the cap are dropped and counted; the count is reported
/// in one line once the second is over. Thread-safe. Times are passed in (milliseconds from any steady
/// clock) so the rules can be tested on their own.
/// </summary>
public sealed class LineLimiter
{
    /// <summary>The cap a new logger starts with.</summary>
    public const int DefaultPerSecond = 100;

    public const int MinPerSecond = 1;
    public const int MaxPerSecond = 100_000;

    private const long WindowMs = 1000;

    private readonly object _lock = new object();
    private int _perSecond;
    private bool _started;
    private long _windowStart;
    private int _lines;
    private int _dropped;

    public LineLimiter(int perSecond = DefaultPerSecond)
    {
        PerSecond = perSecond;
    }

    /// <summary>Lines allowed per second, clamped to <see cref="MinPerSecond"/>..<see cref="MaxPerSecond"/>.</summary>
    public int PerSecond
    {
        get => _perSecond;
        set => _perSecond = value < MinPerSecond ? MinPerSecond : value > MaxPerSecond ? MaxPerSecond : value;
    }

    /// <summary>Every line dropped since the limiter was made.</summary>
    public long TotalDropped { get; private set; }

    /// <summary>
    /// Asks to write one line at <paramref name="nowMs"/>. Returns false when the line has to be dropped.
    /// <paramref name="droppedBefore"/> is how many lines the window that just ended dropped and nobody
    /// reported yet; write that note before the line.
    /// </summary>
    public bool TryTake(long nowMs, out int droppedBefore)
    {
        lock (_lock)
        {
            droppedBefore = 0;
            if (!_started || nowMs - _windowStart >= WindowMs || nowMs < _windowStart)
            {
                droppedBefore = _dropped;
                _dropped = 0;
                _lines = 0;
                _windowStart = nowMs;
                _started = true;
            }
            if (_lines >= _perSecond)
            {
                _dropped++;
                TotalDropped++;
                return false;
            }
            _lines++;
            return true;
        }
    }

    /// <summary>
    /// Takes the drop count of a window that is over (nothing while it still runs), for a periodic
    /// report when the logger has gone quiet. Returns 0 when there is nothing to report.
    /// </summary>
    public int TakeFinishedDrops(long nowMs)
    {
        lock (_lock)
        {
            if (_dropped == 0 || (nowMs - _windowStart < WindowMs && nowMs >= _windowStart))
                return 0;
            int dropped = _dropped;
            _dropped = 0;
            return dropped;
        }
    }

    /// <summary>Lines dropped in the current window that haven't been reported yet.</summary>
    public int PendingDrops
    {
        get
        {
            lock (_lock)
                return _dropped;
        }
    }
}
