using System.Linq;
using ObenUIKit.Controls;
using ObenUIKit.Menu.Settings;

namespace ObenUIKit.Menu.Pages;

/// <summary>
/// The config files mods registered with <see cref="ModSettings"/>, one at a time (a cycle row picks
/// the mod when there are several). Edits are saved as they're made.
/// </summary>
internal static class SettingsPage
{
    public const string Title = "Settings";

    private static string _current;

    public static ModPage Create() => new ModPage(Title, Build) { Order = 10 };

    private static void Build(UiBuilder ui)
    {
        var sections = ModSettings.Sections.OrderBy(s => s.Title).ToList();
        if (sections.Count == 0)
        {
            ui.Label("No mod has settings to show yet.", muted: true);
            return;
        }

        int index = sections.FindIndex(s => s.Title == _current);
        if (index < 0)
            index = 0;
        _current = sections[index].Title;

        if (sections.Count > 1)
        {
            var titles = sections.Select(s => s.Title).ToArray();
            ui.Cycle("Mod", titles, index, i =>
            {
                ModSettings.FlushAll();
                _current = titles[i];
                ui.Window.Rebuild();
            });
        }
        else
        {
            ui.Header(_current);
        }
        ui.Space(10f);

        ConfigEditor.Build(ui, sections[index]);
        // A dragged slider writes its file a moment after it settles.
        ui.Window.AddRefresher(ModSettings.FlushAll);
    }
}
