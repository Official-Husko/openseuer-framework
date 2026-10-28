using System;
using ObenFramework.Logging;

namespace ObenFramework.World;

/// <summary>
/// Logging for the world module's Harmony handlers. The game calls patched methods inside its own code,
/// so a handler must never throw; it reports a failure here instead, once per place (until the next level).
/// </summary>
internal static class WorldLog
{
    private static readonly AreaLog Area = new AreaLog("world");

    public static void Failed(string where, Exception e) => Area.Failed(where, e);

    public static void Reset() => Area.Reset();
}
