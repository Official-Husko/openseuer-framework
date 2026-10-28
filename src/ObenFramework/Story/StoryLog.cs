using System;
using ObenLoader.Logging;

namespace ObenFramework.Story;

/// <summary>
/// The story modules' log. A refusal (a mod asking for something the rules don't allow) is logged once
/// per caller and reason, since the same call tends to repeat every frame or every load. Uses the
/// framework logger's log-once keys, which start over when the framework reloads.
/// </summary>
internal static class StoryLog
{
    private const string Prefix = "story:";

    internal static ModLogger Log;

    public static void Init(ModLogger log) => Log = log;

    /// <summary>Logs a warning the first time this <paramref name="what"/> and <paramref name="reason"/> come up.</summary>
    public static void Refused(string what, string reason) =>
        Log?.WarnOnce(Prefix + what + "\n" + reason, $"{what} refused: {reason} (repeats are not logged)");

    /// <summary>Logs an exception the first time it happens at <paramref name="where"/>.</summary>
    public static void Failed(string where, Exception e) =>
        Log?.ErrorOnce(Prefix + where + "\n" + e?.GetType().FullName, $"{where} failed (repeats are not logged)", e);

    public static void Info(string message) => Log?.Info(message);

    /// <summary>A mod's id, or the type name of another owner (a script scope).</summary>
    public static string NameOf(ObenLoader.Runtime.ITracker owner) =>
        owner is ObenLoader.ObenMod mod ? mod.Info.Id : owner?.GetType().Name ?? "?";
}
