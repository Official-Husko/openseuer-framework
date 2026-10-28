using System.Collections.Generic;
using System.Linq;
using ObenUIKit.Controls;
using ObenUIKit.Menu.Settings;

namespace ObenUIKit.Menu.Pages;

/// <summary>
/// The config files mods registered with <see cref="ModSettings"/>, one at a time (a cycle row picks
/// the mod when there are several), followed by the mod's action buttons (<see cref="ModActions"/>).
/// Mods with actions but no config get an entry of their own. Edits are saved as they're made.
/// </summary>
internal static class SettingsPage
{
    public const string Title = "Settings";

    private sealed class Entry
    {
        public string Title;
        public string OwnerId;
        public SettingsSection Section;
        public bool FirstOfOwner;
    }

    private static string _current;

    public static ModPage Create() => new ModPage(Title, Build) { Order = 10 };

    private static void Build(UiBuilder ui)
    {
        var entries = Entries();
        if (entries.Count == 0)
        {
            ui.Label("No mod has settings to show yet.", muted: true);
            return;
        }

        int index = entries.FindIndex(e => e.Title == _current);
        if (index < 0)
            index = 0;
        _current = entries[index].Title;

        if (entries.Count > 1)
        {
            var titles = entries.Select(e => e.Title).ToArray();
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

        var entry = entries[index];
        if (entry.Section != null)
            ConfigEditor.Build(ui, entry.Section);
        if (entry.FirstOfOwner)
        {
            var actions = ModActions.Of(entry.OwnerId);
            if (actions.Count > 0)
            {
                ui.Space(10f);
                ActionRows.BuildRegistered(ui, actions);
            }
        }
        // A dragged slider writes its file a moment after it settles.
        ui.Window.AddRefresher(ModSettings.FlushAll);
    }

    private static List<Entry> Entries()
    {
        var entries = ModSettings.Sections
            .Select(s => new Entry { Title = s.Title, OwnerId = s.OwnerId, Section = s })
            .ToList();
        foreach (var action in ModActions.All)
        {
            if (!entries.Any(e => e.OwnerId == action.OwnerId))
                entries.Add(new Entry { Title = action.OwnerName, OwnerId = action.OwnerId });
        }
        entries = entries.OrderBy(e => e.Title).ToList();

        // A mod's actions show once, under its first entry.
        var seen = new HashSet<string>();
        foreach (var entry in entries)
            entry.FirstOfOwner = seen.Add(entry.OwnerId);
        return entries;
    }
}
