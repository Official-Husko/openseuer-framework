// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
using System;
using System.Collections.Generic;

namespace ObenLoader.Logging;

/// <summary>
/// Keeps one logger's errors readable when something fails over and over: the same error is written at
/// most once every <see cref="RepeatMs"/>, and at most <see cref="MaxPerWindow"/> errors are written in
/// each <see cref="WindowMs"/>. What is held back is counted for one summary line once the window is
/// over. Thread-safe; times are passed in (milliseconds from any steady clock) so it can be tested alone.
/// </summary>
public sealed class ErrorThrottle
{
    public const long RepeatMs = 10_000;
    public const long WindowMs = 10_000;
    public const int MaxPerWindow = 20;
    private const int MaxKeys = 256;

    private readonly object _lock = new object();
    private readonly Dictionary<string, long> _lastWritten = new Dictionary<string, long>(StringComparer.Ordinal);
    private bool _started;
    private long _windowStart;
    private int _written;
    private int _heldBack;

    /// <summary>Every error held back since the throttle was made.</summary>
    public long TotalHeldBack { get; private set; }

    /// <summary>
    /// Asks to write the error <paramref name="key"/> stands for at <paramref name="nowMs"/>. Returns false
    /// when it has to be held back. <paramref name="heldBackBefore"/> is how many errors the window that
    /// just ended held back and nobody reported yet; write that note before the error.
    /// </summary>
    public bool TryWrite(string key, long nowMs, out int heldBackBefore)
    {
        key ??= "";
        lock (_lock)
        {
            heldBackBefore = 0;
            if (!_started || nowMs - _windowStart >= WindowMs || nowMs < _windowStart)
            {
                heldBackBefore = _heldBack;
                _heldBack = 0;
                _written = 0;
                _windowStart = nowMs;
                _started = true;
            }
            if (_lastWritten.TryGetValue(key, out long last) && nowMs - last < RepeatMs && nowMs >= last)
                return HoldBack();
            if (_written >= MaxPerWindow)
                return HoldBack();
            if (_lastWritten.Count >= MaxKeys && !_lastWritten.ContainsKey(key))
                _lastWritten.Clear();
            _lastWritten[key] = nowMs;
            _written++;
            return true;
        }
    }

    /// <summary>
    /// Takes the count of a window that is over (nothing while it still runs), for a report when the
    /// logger has gone quiet. Returns 0 when there is nothing to report.
    /// </summary>
    public int TakeFinishedHeldBack(long nowMs)
    {
        lock (_lock)
        {
            if (_heldBack == 0 || (nowMs - _windowStart < WindowMs && nowMs >= _windowStart))
                return 0;
            int count = _heldBack;
            _heldBack = 0;
            return count;
        }
    }

    /// <summary>Errors held back in the current window that haven't been reported yet.</summary>
    public int PendingHeldBack
    {
        get
        {
            lock (_lock)
                return _heldBack;
        }
    }

    private bool HoldBack()
    {
        _heldBack++;
        TotalHeldBack++;
        return false;
    }
}
