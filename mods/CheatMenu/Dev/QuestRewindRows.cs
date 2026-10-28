// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
using ObenFramework.Quests;
using ObenLoader;
using ObenLoader.Mods;
using ObenUIKit;
using ObenUIKit.Controls;

namespace CheatMenu.Dev;

/// <summary>
/// Rewinding a task made by a mod: reset, restart, remove, jump to an objective, and one checkpoint kept for
/// the session. <see cref="QuestAuthoring"/> only lets a mod rewind its own tasks, so the calls are made on
/// behalf of the mod whose id the task id starts with. The game's own tasks have no rewind; their state can
/// still be set above.
/// </summary>
internal static class QuestRewindRows
{
    private const string Checkpoint = "devmenu";

    public static void Build(UiBuilder ui, CheatContext ctx, QuestTask task, string objectiveId)
    {
        string modId = DevRules.TaskOwner(task.Id);
        ObenMod owner = modId == null ? null : ModRegistry.Find(modId)?.Instance;
        if (owner == null)
        {
            ui.Label(modId == null
                ? "This is one of the game's tasks: it can change state, but only tasks made by mods can be rewound."
                : $"{modId} made this task but isn't loaded, so it can't be rewound now.", muted: true);
            return;
        }

        var kept = QuestAuthoring.Checkpoints(task.Id);
        ui.Label($"Made by {modId}. Checkpoint {(kept.Contains(Checkpoint) ? "kept" : "not kept yet")} for this session.", muted: true);
        string id = task.Id;
        ui.Row(r =>
        {
            r.Button("Reset", () => DevActions.Quest(ctx, ui, "Reset " + id, () => QuestAuthoring.Reset(owner, id)));
            r.Button("Restart", () => DevActions.Quest(ctx, ui, "Restart " + id, () => QuestAuthoring.Restart(owner, id)));
            r.Button("Remove", () => UiKit.Confirm(DevWindow.Owner, "Remove the task?",
                $"{task.Title} goes from the task list and, at the next save, from the save.",
                () => DevActions.Quest(ctx, ui, "Remove " + id, () => QuestAuthoring.Remove(owner, id)), yes: "Remove"));
        });
        ui.Row(r =>
        {
            if (objectiveId != null)
                r.Button("Jump to objective", () => DevActions.Quest(ctx, ui, "Jump to " + objectiveId, () => QuestAuthoring.JumpTo(owner, id, objectiveId)));
            r.Button("Keep checkpoint", () => DevActions.Quest(ctx, ui, "Checkpoint kept", () => QuestAuthoring.SaveCheckpoint(owner, id, Checkpoint), marks: false));
            r.Button("Go back to it", () => DevActions.Quest(ctx, ui, "Checkpoint restored", () => QuestAuthoring.RestoreCheckpoint(owner, id, Checkpoint)));
        });
    }
}
