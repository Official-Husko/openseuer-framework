using System;

namespace ObenLoader.Logging;

public sealed partial class ModLogger
{
    private readonly OnceKeys _once = new OnceKeys();
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
    /// reloads: for refusals a mod may repeat every frame. Returns whether it was written.
    /// </summary>
    public bool WarnOnce(string key, string message)
    {
        if (!_once.First(key))
            return false;
        Write(LogLevel.Warning, message);
        return true;
    }

    /// <summary>
    /// An error written the first time <paramref name="key"/> comes up. Repeats aren't written, but they
    /// still count in errors.log. Returns whether it was written.
    /// </summary>
    public bool ErrorOnce(string key, string message, Exception e = null)
    {
        string text = e == null ? message : message + "\n" + e;
        if (!_once.First(key))
        {
            LogHub.CountError(Source, text);
            return false;
        }
        Write(LogLevel.Error, text);
        return true;
    }

    /// <summary>
    /// For code running inside the game's own methods (Harmony handlers, game events), which must never
    /// throw: logs "<paramref name="where"/> failed" with the exception the first time that place fails.
    /// </summary>
    public bool Failed(string where, Exception e) =>
        ErrorOnce("failed:" + where, $"{where} failed (further failures of it are not logged)", e);

    /// <summary>
    /// Forgets the once-keys starting with <paramref name="keyPrefix"/> (all of them for null), so they
    /// are logged again. The change-only keys are kept unless <paramref name="keyPrefix"/> is null.
    /// </summary>
    public void ForgetOnce(string keyPrefix = null)
    {
        _once.Forget(keyPrefix);
        if (keyPrefix == null)
            _changes.Forget();
    }
}
