// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
using System;
using ObenLoader.Runtime;
using ObenUIKit;
using ObenUIKit.Controls;
using ObenUIKit.Windows;

namespace CheatMenu.Dev;

/// <summary>
/// The developer menu window, also the Developer page of the MODS menu. The kit has no tabs yet, so a cycle
/// at the top picks the part shown (quests, flags, watches, session, player and world, mods) and the window is built again.
/// Each part is built inside its own guard: one that throws says so and the rest of the window still works.
/// </summary>
internal static class DevWindow
{
    public const string WindowId = "cheatmenu.dev";

    private static readonly string[] Parts = { "Quests", "Global flags", "Watches", "Session and story", "Player and world", "From mods" };
    private static int _part;
    private static UiWindow _window;

    /// <summary>What the menu's windows and dialogs belong to: the Developer menu feature while it is on.</summary>
    internal static ITracker Owner { get; private set; }

    public static void Init(ITracker owner)
    {
        Owner = owner;
        _window = null;
    }

    public static void Toggle(CheatContext ctx)
    {
        if (_window != null && _window.IsOpen)
        {
            _window.Close();
            return;
        }
        if (UiKit.AnyWindowOpen || Owner == null)
            return;
        _window = UiKit.Open(Owner, WindowId, "Developer menu", ui => Build(ui, ctx));
    }

    public static void Build(UiBuilder ui, CheatContext ctx)
    {
        if (DevMarks.Modified)
            ui.Label("This save was changed from the developer menu; say so in a bug report from it.", muted: true);
        ui.Live(() => ctx.Status, muted: true);
        ui.Cycle("Show", Parts, _part, i =>
        {
            _part = i;
            ui.Window.Rebuild();
        });

        try
        {
            switch (_part)
            {
                case 0: QuestsSection.Build(ui, ctx); break;
                case 1: FlagsSection.Build(ui, ctx); break;
                case 2: WatchesSection.Build(ui, ctx); break;
                case 3: SessionSection.Build(ui, ctx); break;
                case 4: WorldSection.Build(ui, ctx); break;
                default: BuildModSections(ui, ctx); break;
            }
        }
        catch (Exception e)
        {
            ctx.Fail(Parts[_part], e);
            ui.Label($"Building {Parts[_part]} failed; the Cheat Menu log has the error.", muted: true);
        }
    }

    private static void BuildModSections(UiBuilder ui, CheatContext ctx)
    {
        if (DevMenu.Sections.Count == 0)
        {
            ui.Label("No mod has added a section. Mods call CheatMenu.Dev.DevMenu.AddSection, and AddWatch for the Watches part.", muted: true);
            return;
        }
        foreach (var section in DevMenu.Sections)
        {
            ui.Header($"{section.Title} ({section.ModId})");
            try
            {
                section.Build(ui);
            }
            catch (Exception e)
            {
                ctx.Fail($"The developer menu section '{section.Title}' of {section.ModId}", e);
                ui.Label($"This section failed: {e.GetType().Name}: {e.Message}", muted: true);
            }
        }
    }
}
