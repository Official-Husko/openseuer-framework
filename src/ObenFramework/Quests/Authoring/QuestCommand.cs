// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
using ObenFramework.Console;
using ObenLoader;
using ObenLoader.Mods;

namespace ObenFramework.Quests;

/// <summary>
/// The F1 console's <c>modquest</c> command for testing quests: <c>modquest reset MyMod.intro</c> sends a
/// mod's task back to its start, <c>modquest skip SomeTask [objective]</c> finishes a task or one of its
/// steps (game tasks too), <c>modquest list</c> lists the mod tasks of the save.
/// </summary>
internal static class QuestCommand
{
    private const string Usage = "modquest list | reset <modTaskId> | skip <taskId> [objectiveId]";

    public static void Register(ObenMod framework) => GameConsole.Register(framework, "modquest", Usage, Run);

    private static string Run(string[] args)
    {
        if (args.Length == 0)
            return Usage;
        switch (args[0])
        {
            case "list":
                return List();
            case "reset" when args.Length == 2:
                return Reset(args[1]);
            case "skip" when args.Length == 2:
                return Describe(QuestLog.SetTaskStatus(args[1], CurrentTaskStatus.Finished), args[1] + " finished");
            case "skip" when args.Length == 3:
                return Describe(QuestLog.SetObjectiveStatus(args[1], args[2], ObjectiveStatus.Finished), args[1] + " / " + args[2] + " finished");
            default:
                return Usage;
        }
    }

    private static string Reset(string taskId)
    {
        string modId = QuestIds.OwnerOf(taskId);
        var owner = modId == null ? null : ModRegistry.Find(modId)?.Instance;
        if (owner == null)
            return $"{taskId} is not a task of a loaded mod.";
        return Describe(QuestAuthoring.Reset(owner, taskId), taskId + " is back at its start");
    }

    private static string List()
    {
        var lines = new System.Text.StringBuilder();
        foreach (var task in QuestLog.All())
        {
            if (QuestIds.OwnerOf(task.Id) != null)
                lines.AppendLine($"{task.Id}: {task.Title} [{task.Status}]");
        }
        return lines.Length == 0 ? "No mod tasks in this save." : lines.ToString().TrimEnd();
    }

    private static string Describe(QuestResult result, string done) => result.Changed ? done : "Not changed: " + result.Reason;
}
