using System;
using ObenLoader.Logging;

namespace ObenFramework.Logging;

/// <summary>
/// One area's part of the framework log (items, NPCs, the world...). Refusals and failures that can repeat
/// every frame are logged once per key with the loader's <see cref="OnceKeys"/>, the same helper behind
/// <see cref="ModLogger.WarnOnce"/>, like the reference framework's ApiLog.Once; verbose logging writes
/// them every time, as its debug mode does. The keys are the area's own, so <see cref="Reset"/> only
/// forgets this area's, and a repeat costs one set lookup and no allocation. Everything is forgotten when
/// the framework reloads. Safe to call while the framework isn't loaded: nothing is logged then.
/// </summary>
internal sealed class AreaLog
{
    private readonly OnceKeys _once = new OnceKeys();

    public AreaLog(string area)
    {
        Area = area;
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

    /// <summary>A warning logged the first time <paramref name="key"/> comes up in this area (every time with verbose logging on).</summary>
    public bool WarnOnce(string key, string message)
    {
        var log = Framework;
        if (log == null || (!_once.First(key) && !LogHub.Verbose))
            return false;
        log.Warn(message);
        return true;
    }

    /// <summary>An error logged the first time <paramref name="key"/> comes up in this area (every time with verbose logging on).</summary>
    public bool ErrorOnce(string key, string message, Exception e = null)
    {
        var log = Framework;
        if (log == null || (!_once.First(key) && !LogHub.Verbose))
            return false;
        log.Error(message, e);
        return true;
    }

    /// <summary>
    /// For Harmony handlers and game event handlers, which must never throw into the game's code: logs the
    /// failure the first time <paramref name="where"/> fails (every time with verbose logging on, where the
    /// logger's error throttle is off too). The message is only built when it is written.
    /// </summary>
    public bool Failed(string where, Exception e)
    {
        var log = Framework;
        if (log == null || (!_once.First(where) && !LogHub.Verbose))
            return false;
        log.Error($"{where} failed{(LogHub.Verbose ? "" : " (further failures of it are not logged)")}", e);
        return true;
    }

    /// <summary>Forgets what this area already logged, so it's reported again.</summary>
    public void Reset() => _once.Forget();
}
