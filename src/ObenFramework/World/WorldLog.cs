using System;
using System.Collections.Generic;
using ObenFramework.Events;

namespace ObenFramework.World;

/// <summary>
/// Logging for the world module's Harmony handlers. The game calls patched methods inside its own code,
/// so a handler must never throw; it reports a failure here instead, once per place.
/// </summary>
internal static class WorldLog
{
    private static readonly HashSet<string> Reported = new HashSet<string>();

    public static void Failed(string where, Exception e)
    {
        if (Reported.Add(where))
            GameEvents.Log?.Error($"{where} failed (further failures of it are not logged)", e);
    }

    public static void Reset() => Reported.Clear();
}
