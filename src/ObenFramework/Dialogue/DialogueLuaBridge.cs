// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
using System;
using ObenFramework.Quests;
using ObenFramework.Story;
using ObenLoader;

namespace ObenFramework.Dialogue;

/// <summary>
/// The framework's own Lua functions, usable in any condition or script (the game's conversations, mod
/// conversations, <see cref="LuaScript"/>):
/// <c>Oben_QuestState(taskId)</c> and <c>Oben_ObjectiveState(taskId, objectiveId)</c> give "Active",
/// "Finished", "Failed", "Abandoned", "Hidden" or "" when not started; <c>Oben_CrimeTotal()</c> and
/// <c>Oben_CrimeAgainst(npcId)</c> read the crime record. <c>Oben_ResponsePicked</c>,
/// <c>Oben_BranchPicked</c> and <c>Oben_LineText</c> are what added responses, branches and text callbacks
/// call; they are not meant for hand-written Lua.
/// </summary>
internal static class DialogueLuaBridge
{
    public static void Register(ObenMod framework)
    {
        Add(framework, "Oben_QuestState", new Func<string, string>(QuestState));
        Add(framework, "Oben_ObjectiveState", new Func<string, string, string>(ObjectiveState));
        Add(framework, "Oben_CrimeTotal", new Func<double>(CrimeTotal));
        Add(framework, "Oben_CrimeAgainst", new Func<double, double>(CrimeAgainst));
        Add(framework, "Oben_ResponsePicked", new Action<string>(DialogueResponses.Picked));
        Add(framework, "Oben_BranchPicked", new Action<string>(DialogueBranches.Picked));
        Add(framework, "Oben_LineText", new Func<string, string>(LineTexts.Text));
    }

    private static void Add(ObenMod framework, string name, Delegate function)
    {
        try { LuaFunctions.Register(framework, name, function); }
        catch (Exception e) { StoryLog.Failed("Registering Lua function " + name, e); }
    }

    private static string QuestState(string taskId)
    {
        var status = QuestLog.StatusOf(taskId);
        return status == null ? "" : status.Value.ToString();
    }

    private static string ObjectiveState(string taskId, string objectiveId)
    {
        var status = QuestLog.StatusOf(taskId, objectiveId);
        return status == null ? "" : status.Value.ToString();
    }

    private static double CrimeTotal() => Law.Total;

    private static double CrimeAgainst(double npcId) => Law.Against((int)npcId);
}
