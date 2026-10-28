// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
using System.Collections.Generic;
using ObenFramework.NewGames;
using ObenLoader.Mods;
using ObenLoader.Runtime;
using ObenUIKit.Controls;
using ObenUIKit.Windows;

namespace ObenUIKit.NewGameScreen;

/// <summary>
/// The window behind the New Game screen's "Mod options" button: one group per mod with its options,
/// general ones first and the "custom rules" ones after. Picks go to <see cref="NewGameOptions.Choose"/>
/// and are stored when the player starts the game.
/// </summary>
internal static class NewGameOptionsWindow
{
    public const string WindowId = "obenuikit.newgame.options";

    public static UiWindow Open(ITracker owner) =>
        UiKit.Open(owner, WindowId, "Mod options", Build, Footer);

    private static void Build(UiBuilder ui)
    {
        var options = NewGameOptions.Options;
        if (options.Count == 0)
        {
            ui.Label("No mod has added options to the new game.", muted: true);
            return;
        }

        ui.Label("These apply to the game you are about to start and are saved with it.", muted: true);
        foreach (var modId in ModsOf(options))
        {
            ui.Space(10f);
            ui.Header(ModName(modId));
            Group(ui, options, modId, NewGamePlacement.BelowDifficulty);
            Group(ui, options, modId, NewGamePlacement.CustomPanel);
        }
    }

    private static void Footer(UiBuilder ui)
    {
        ui.Row(r =>
        {
            r.Button("Defaults", () =>
            {
                NewGameOptions.ResetScreen();
                r.Window.Rebuild();
            });
            r.Button("Back", () => r.Window.Close());
        });
    }

    private static void Group(UiBuilder ui, List<NewGameOption> options, string modId, NewGamePlacement placement)
    {
        foreach (var option in options)
        {
            if (option.ModId == modId && option.Placement == placement)
                NewGameOptionRows.Add(ui, option);
        }
    }

    private static List<string> ModsOf(List<NewGameOption> options)
    {
        var mods = new List<string>();
        foreach (var option in options)
        {
            if (!mods.Contains(option.ModId))
                mods.Add(option.ModId);
        }
        return mods;
    }

    private static string ModName(string modId)
    {
        var info = ModRegistry.Find(modId);
        return info != null ? info.Manifest.DisplayName : modId;
    }
}
