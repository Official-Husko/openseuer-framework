// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
namespace ObenFramework.Quests;

/// <summary>
/// Whether finishing a mod's task starts its follow-up. No Unity types, so it is unit tested on its own.
/// </summary>
internal static class QuestFollowUpPlan
{
    public const string None = "none";
    public const string Self = "self";
    public const string Exists = "exists";
    public const string Again = "again";
    public const string Depth = "depth";

    /// <summary>The follow-up's task id, or null when there is none.</summary>
    public static string TargetId(string modId, string nextKey) =>
        string.IsNullOrEmpty(modId) || string.IsNullOrEmpty(nextKey) ? null : modId + "." + nextKey;

    /// <summary>
    /// Null when the follow-up should start, else why not: there is none, it points at itself, the save
    /// already has it, it was already started this session, or this session started too many in a row.
    /// </summary>
    public static string Skip(string ownKey, string nextKey, bool exists, bool startedBefore, int startedThisSession)
    {
        if (string.IsNullOrEmpty(nextKey))
            return None;
        if (nextKey == ownKey)
            return Self;
        if (exists)
            return Exists;
        if (startedBefore)
            return Again;
        return startedThisSession + 1 <= QuestPlan.MaxChain ? null : Depth;
    }

    /// <summary>Reasons that are worth a line in the log (the others are the normal case).</summary>
    public static bool IsWorthLogging(string skip) => skip != null && skip != None && skip != Exists;
}
