using System;
using System.Collections.Generic;
using ObenFramework.Events;

namespace ObenFramework.World;

/// <summary>
/// A log for one area of the framework that reports each kind of refusal or failure once. Calls such as
/// "place this prop" can be made every frame by a careless mod; the first refusal explains why and the
/// rest stay quiet. The keys are forgotten when the framework unloads.
/// </summary>
internal sealed class OnceLog
{
    private readonly string _area;
    private readonly HashSet<string> _seen = new HashSet<string>();

    public OnceLog(string area)
    {
        _area = area;
    }

    public void Info(string message) => GameEvents.Log?.Info(_area + ": " + message);

    /// <summary>Logs a warning the first time <paramref name="where"/> is used.</summary>
    public void Warn(string where, string message)
    {
        if (_seen.Add(where))
            GameEvents.Log?.Warn($"{_area}: {where}: {message}");
    }

    /// <summary>Logs an exception the first time <paramref name="where"/> fails.</summary>
    public void Error(string where, Exception e)
    {
        if (_seen.Add(where))
            GameEvents.Log?.Error($"{_area}: {where} failed (further failures of it are not logged)", e);
    }

    public void Reset() => _seen.Clear();
}
