using System;
using ObenFramework.Logging;

namespace ObenFramework.World;

/// <summary>
/// A log for one area of the framework that reports each kind of refusal or failure once. Calls such as
/// "place this prop" can be made every frame by a careless mod; the first refusal explains why and the
/// rest stay quiet. The keys are forgotten when the framework unloads. Lines start with the area's name.
/// </summary>
internal sealed class OnceLog
{
    private readonly string _area;
    private readonly AreaLog _log;

    public OnceLog(string area)
    {
        _area = area;
        _log = new AreaLog(area);
    }

    public void Info(string message) => _log.Info(_area + ": " + message);

    /// <summary>Logs a warning the first time <paramref name="where"/> is used.</summary>
    public void Warn(string where, string message) =>
        _log.WarnOnce(where, $"{_area}: {where}: {message}");

    /// <summary>Logs an exception the first time <paramref name="where"/> fails.</summary>
    public void Error(string where, Exception e) =>
        _log.ErrorOnce(where, $"{_area}: {where} failed (further failures of it are not logged)", e);

    public void Reset() => _log.Reset();
}
