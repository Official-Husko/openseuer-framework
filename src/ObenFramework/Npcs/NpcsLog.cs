using System;
using ObenFramework.Logging;

namespace ObenFramework.Npcs;

/// <summary>
/// Logging for the NPC code. The Harmony handlers run inside the game's own methods, so they report a
/// failure here once per place instead of throwing.
/// </summary>
internal static class NpcsLog
{
    private static readonly AreaLog Area = new AreaLog("npcs");

    public static void Info(string message) => Area.Info(message);

    public static void Warn(string message) => Area.Warn(message);

    /// <summary>A warning logged the first time <paramref name="key"/> comes up, then not again until the framework reloads.</summary>
    public static void WarnOnce(string key, string message) => Area.WarnOnce(key, message);

    public static void Failed(string where, Exception e) => Area.Failed(where, e);

    public static void Reset() => Area.Reset();
}
