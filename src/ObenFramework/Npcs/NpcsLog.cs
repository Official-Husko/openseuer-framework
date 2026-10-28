using System;
using System.Collections.Generic;

namespace ObenFramework.Npcs;

/// <summary>
/// Logging for the NPC code. The Harmony handlers run inside the game's own methods, so they report a
/// failure here once per place instead of throwing.
/// </summary>
internal static class NpcsLog
{
    private static readonly HashSet<string> Reported = new HashSet<string>();

    public static void Info(string message) => FrameworkMod.Instance?.Log.Info(message);

    public static void Warn(string message) => FrameworkMod.Instance?.Log.Warn(message);

    public static void Failed(string where, Exception e)
    {
        if (Reported.Add(where))
            FrameworkMod.Instance?.Log.Error($"{where} failed (further failures of it are not logged)", e);
    }

    public static void Reset() => Reported.Clear();
}
