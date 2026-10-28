using System;
using ObenFramework.Logging;

namespace ObenFramework.Items;

/// <summary>Logging for the item and recipe code, safe to call while the framework is not loaded.</summary>
internal static class ItemsLog
{
    private static readonly AreaLog Area = new AreaLog("items");

    public static void Info(string message) => Area.Info(message);

    public static void Warn(string message) => Area.Warn(message);

    /// <summary>Warns once per key, for refusals a mod may repeat every frame.</summary>
    public static void WarnOnce(string key, string message) => Area.WarnOnce(key, message);

    public static void Error(string message, Exception e = null) => Area.Error(message, e);

    /// <summary>
    /// For Harmony handlers, which must never throw into the game's code: reports a failure once per place.
    /// </summary>
    public static void Failed(string where, Exception e) => Area.Failed(where, e);

    public static void Reset() => Area.Reset();
}
