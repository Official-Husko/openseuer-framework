using System;

namespace ObenLoader.Logging;

public sealed partial class ModLogger
{
    private readonly OnceKeys _once = new OnceKeys();
    private readonly OnceKeys _failed = new OnceKeys();
    private readonly ChangeFilter _changes = new ChangeFilter();

    /// <summary>
    /// A debug line that is only written when <paramref name="message"/> differs from the last one under
    /// <paramref name="key"/>: handy for a state checked every frame. Written as "key: message".
    /// </summary>
    public void Debug(string key, string message)
    {
        if (_changes.Changed(key, message))
            Write(LogLevel.Debug, key + ": " + message);
    }

    /// <summary>An info line written the first time <paramref name="key"/> comes up. Returns whether it was written.</summary>
    public bool InfoOnce(string key, string message)
    {
        if (!_once.First(key))
            return false;
        Write(LogLevel.Info, message);
        return true;
    }

    /// <summary>
    /// A warning written the first time <paramref name="key"/> comes up and never again until the mod
    /// reloads: for refusals a mod may repeat every frame. With verbose logging on it is written every
    /// time. Returns whether it was written.
    /// </summary>
    public bool WarnOnce(string key, string message)
    {
        if (!_once.First(key) && !LogHub.Verbose)
            return false;
        Write(LogLevel.Warning, message);
        return true;
    }

    /// <summary>
    /// An error written the first time <paramref name="key"/> comes up and never again until the mod
    /// reloads (every time with verbose logging on). A repeat costs one set lookup, so this is safe in a
    /// Harmony handler that runs every frame. Returns whether it was written.
    /// </summary>
    public bool ErrorOnce(string key, string message, Exception e = null)
    {
        if (!_once.First(key) && !LogHub.Verbose)
            return false;
        Write(LogLevel.Error, e == null ? message : message + "\n" + e, "once " + key);
        return true;
    }

    /// <summary>
    /// For code running inside the game's own methods (Harmony handlers, game events), which must never
    /// throw: logs "<paramref name="where"/> failed" with the exception the first time that place fails
    /// (every time with verbose logging on).
    /// </summary>
    public bool Failed(string where, Exception e)
    {
        // Own key set, so a repeat needs no string building at all.
        if (!_failed.First(where) && !LogHub.Verbose)
            return false;
        Write(LogLevel.Error, $"{where} failed{(LogHub.Verbose ? "" : " (further failures of it are not logged)")}\n{e}", "failed " + where);
        return true;
    }

    /// <summary>
    /// Forgets the once-keys starting with <paramref name="keyPrefix"/> (all of them for null), so they
    /// are logged again. The change-only keys are kept unless <paramref name="keyPrefix"/> is null.
    /// </summary>
    public void ForgetOnce(string keyPrefix = null)
    {
        _once.Forget(keyPrefix);
        _failed.Forget(keyPrefix);
        if (keyPrefix == null)
            _changes.Forget();
    }
}
