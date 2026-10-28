using System;
using System.Collections.Generic;

namespace ObenFramework.Items;

/// <summary>Logging for the item and recipe code, safe to call while the framework is not loaded.</summary>
internal static class ItemsLog
{
    private static readonly HashSet<string> Reported = new HashSet<string>();

    public static void Warn(string message) => FrameworkMod.Instance?.Log.Warn(message);

    /// <summary>Warns once per key, for refusals a mod may repeat every frame.</summary>
    public static void WarnOnce(string key, string message)
    {
        if (Reported.Add(key))
            Warn(message);
    }

    public static void Error(string message, Exception e = null)
    {
        var log = FrameworkMod.Instance?.Log;
        if (log == null)
            return;
        if (e == null)
            log.Error(message);
        else
            log.Error(message, e);
    }

    /// <summary>
    /// For Harmony handlers, which must never throw into the game's code: reports a failure once per place.
    /// </summary>
    public static void Failed(string where, Exception e)
    {
        if (Reported.Add(where))
            Error($"{where} failed (further failures of it are not logged)", e);
    }

    public static void Reset() => Reported.Clear();
}
