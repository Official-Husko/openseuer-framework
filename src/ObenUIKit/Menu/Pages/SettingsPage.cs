using System.Collections.Generic;
using System.Linq;
using ObenUIKit.Controls;
using ObenUIKit.Menu.Settings;

namespace ObenUIKit.Menu.Pages;

/// <summary>
/// Mods' config files (see <see cref="ModSettings"/>), one at a time (a cycle row picks the mod when there are
/// several), followed by the mod's action buttons (<see cref="ModActions"/>). Mods with actions but no config get
/// an entry of their own. A search box at the top finds settings and actions across every mod. Edits are saved
/// as they're made.
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
    private static string _query = "";

    public static ModPage Create() => new ModPage(Title, Build) { Order = 10 };

    /// <summary>The search starts empty each time the menu opens.</summary>
    internal static void ForgetSearch() => _query = "";

    private static void Build(UiBuilder ui)
    {
        var entries = Entries();
        if (entries.Count == 0)
        {
            ui.Label("No mod has settings to show yet.", muted: true);
            return;
        }

        BuildSearch(ui);
        if (_query.Length > 0)
            BuildResults(ui, entries);
        else
            BuildBrowse(ui, entries);
        // A dragged slider writes its file a moment after it settles.
        ui.Window.AddRefresher(ModSettings.FlushAll);
    }

    // The search runs when the player leaves the field (Enter, Esc or a click elsewhere), a moment later, so the
    // page isn't rebuilt under the field while it is still handling the key.
    private static void BuildSearch(UiBuilder ui)
    {
        string wanted = null;
        ui.TextField("Search", _query, null, text => wanted = (text ?? "").Trim());
        ui.Tooltip("Type a few words and press Enter. Finds settings and actions of every mod by name and description.");
        ui.Window.AddRefresher(() =>
        {
            if (wanted == null || wanted == _query)
                return;
            ModSettings.FlushAll();
            _query = wanted;
            ui.Window.Rebuild();
        });
    }

    private static void BuildBrowse(UiBuilder ui, List<Entry> entries)
    {
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
    }

    // Every mod with a match gets a card holding the matching settings and actions, editable in place.
    private static void BuildResults(UiBuilder ui, List<Entry> entries)
    {
        int found = 0;
        foreach (var entry in entries)
        {
            var actions = entry.FirstOfOwner ? ModActions.Of(entry.OwnerId) : new List<ModAction>();
            bool everything = FieldText.Matches(_query, entry.Title);
            bool fields = entry.Section != null && ConfigEditor.Finds(entry.Section, _query);
            bool anyAction = actions.Exists(a => everything
                || FieldText.Matches(_query, FieldText.SearchText(a.Title, a.Description, a.Group)));
            if (!fields && !anyAction)
                continue;
            ui.Card(entry.Title, card =>
            {
                if (fields)
                    found += ConfigEditor.Build(card, entry.Section, _query);
                if (anyAction)
                    found += ActionRows.BuildRegistered(card, actions, everything ? null : _query);
            });
        }

        if (found == 0)
            ui.Label($"Nothing found for \"{_query}\".", muted: true);
        ui.Space(10f);
        ui.Button("Clear the search", () =>
        {
            _query = "";
            ui.Window.Rebuild();
        });
    }

    private static List<Entry> Entries()
    {
        var entries = ModSettings.All()
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
