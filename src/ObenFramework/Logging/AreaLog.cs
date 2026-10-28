using System;
using ObenLoader.Logging;

namespace ObenFramework.Logging;

/// <summary>
/// One area's part of the framework log (items, NPCs, the world...). Refusals and failures that can repeat
/// every frame are logged once, through the framework logger's log-once keys under the area's own prefix,
/// so <see cref="Reset"/> only forgets this area's. Everything is forgotten when the framework reloads.
/// Safe to call while the framework isn't loaded: nothing is logged then.
/// </summary>
internal sealed class AreaLog
{
    private readonly string _prefix;

    public AreaLog(string area)
    {
        Area = area;
        _prefix = area + ":";
    }

    public string Area { get; }

    /// <summary>The framework's logger, or null while it isn't loaded.</summary>
    public static ModLogger Framework
    {
        get
        {
            var framework = FrameworkMod.Instance;
            return framework != null ? framework.Log : null;
        }
    }

    public void Info(string message) => Framework?.Info(message);

    public void Warn(string message) => Framework?.Warn(message);

    public void Error(string message, Exception e = null) => Framework?.Error(message, e);

    /// <summary>A warning logged the first time <paramref name="key"/> comes up in this area.</summary>
    public bool WarnOnce(string key, string message)
    {
        var log = Framework;
        return log != null && log.WarnOnce(_prefix + key, message);
    }

    /// <summary>An error logged the first time <paramref name="key"/> comes up in this area; repeats still count in errors.log.</summary>
    public bool ErrorOnce(string key, string message, Exception e = null)
    {
        var log = Framework;
        return log != null && log.ErrorOnce(_prefix + key, message, e);
    }

    /// <summary>
    /// For Harmony handlers and game event handlers, which must never throw into the game's code: logs the
    /// failure the first time <paramref name="where"/> fails.
    /// </summary>
    public bool Failed(string where, Exception e) =>
        ErrorOnce("failed:" + where, $"{where} failed (further failures of it are not logged)", e);

    /// <summary>Forgets what this area already logged, so it's reported again.</summary>
    public void Reset() => Framework?.ForgetOnce(_prefix);
}
