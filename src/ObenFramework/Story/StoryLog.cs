using System;
using System.Collections.Generic;
using ObenLoader.Logging;

namespace ObenFramework.Story;

/// <summary>
/// The story modules' log. A refusal (a mod asking for something the rules don't allow) is logged once
/// per caller and reason, since the same call tends to repeat every frame or every load.
/// </summary>
internal static class StoryLog
{
    private const int MaxRemembered = 512;

    private static readonly HashSet<string> Seen = new HashSet<string>(StringComparer.Ordinal);

    internal static ModLogger Log;

    public static void Init(ModLogger log)
    {
        Log = log;
        Seen.Clear();
    }

    /// <summary>Logs a warning the first time this <paramref name="what"/> and <paramref name="reason"/> come up.</summary>
    public static void Refused(string what, string reason)
    {
        if (Remember(what + "\n" + reason))
            Log?.Warn($"{what} refused: {reason} (repeats are not logged)");
    }

    /// <summary>Logs an exception the first time it happens at <paramref name="where"/>.</summary>
    public static void Failed(string where, Exception e)
    {
        if (Remember(where + "\n" + e.GetType().FullName))
            Log?.Error($"{where} failed (repeats are not logged)", e);
    }

    public static void Info(string message) => Log?.Info(message);

    /// <summary>A mod's id, or the type name of another owner (a script scope).</summary>
    public static string NameOf(ObenLoader.Runtime.ITracker owner) =>
        owner is ObenLoader.ObenMod mod ? mod.Info.Id : owner?.GetType().Name ?? "?";

    private static bool Remember(string key)
    {
        if (Seen.Count >= MaxRemembered)
            Seen.Clear();
        return Seen.Add(key);
    }
}
