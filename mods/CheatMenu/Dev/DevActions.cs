using System;
using ObenFramework.Quests;
using ObenUIKit.Controls;

namespace CheatMenu.Dev;

/// <summary>
/// Runs a developer menu action: the result goes on the status line, a change marks the save as
/// dev-modified, and the window is built again so it shows the new state.
/// </summary>
internal static class DevActions
{
    /// <summary>
    /// A quest change, which says itself whether it went through and why not. <paramref name="marks"/> is
    /// false for calls that don't touch the save (keeping a checkpoint).
    /// </summary>
    public static void Quest(CheatContext ctx, UiBuilder ui, string what, Func<QuestResult> change, bool marks = true)
    {
        ctx.Run(what, () =>
        {
            QuestResult result;
            try
            {
                result = change();
            }
            catch
            {
                // It may have failed halfway, with part of the change already in the save.
                if (marks)
                    DevMarks.Mark();
                throw;
            }
            if (result.Changed)
            {
                if (marks)
                    DevMarks.Mark();
                ctx.Say(what + ": done.");
            }
            else
            {
                ctx.Say(what + ": " + result.Reason);
            }
        });
        ui.Window.Rebuild();
    }

    /// <summary>
    /// A change through a framework call that says itself whether it went through: only one that did marks the
    /// save; one that didn't puts <paramref name="refused"/> on the status line.
    /// </summary>
    public static void Act(CheatContext ctx, UiBuilder ui, string what, Func<bool> change, string refused)
    {
        ctx.Run(what, () =>
        {
            bool done;
            try
            {
                done = change();
            }
            catch
            {
                DevMarks.Mark();
                throw;
            }
            if (done)
                DevMarks.Mark();
            ctx.Say(what + ": " + (done ? "done." : refused));
        });
        ui.Window.Defer(ui.Window.Rebuild);
    }

    /// <summary>Any other change; one that throws leaves its message on the status line instead.</summary>
    public static void Change(CheatContext ctx, UiBuilder ui, string what, Action change)
    {
        ctx.Run(what, () =>
        {
            try
            {
                change();
            }
            finally
            {
                // Also when it throws halfway: part of the change may already be in the save.
                DevMarks.Mark();
            }
            ctx.Say(what + ": done.");
        });
        ui.Window.Rebuild();
    }
}
