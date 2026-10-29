using System.Collections.Generic;
using ObenUIKit.Controls;

namespace ObenUIKit.Menu.Settings;

/// <summary>Builds a control for each public field of a settings object, its action buttons and the reset button.</summary>
internal static class ConfigEditor
{
    /// <summary>
    /// Builds the section. With a search <paramref name="query"/> only the fields and actions it finds are
    /// built, without group cards or the reset button (all of them when the query finds the section's title).
    /// Returns how many fields and actions were built.
    /// </summary>
    public static int Build(UiBuilder ui, SettingsSection section, string query = null, bool showReset = true)
    {
        object settings = section.Value;
        if (settings == null)
            return 0;
        object defaults = section.Defaults;
        var views = FieldView.For(settings.GetType());

        if (!string.IsNullOrWhiteSpace(query))
        {
            bool everything = FieldText.Matches(query, section.Title);
            int found = 0;
            foreach (var view in views)
            {
                if (!everything && !FieldText.Matches(query, view.SearchText))
                    continue;
                FieldControls.Build(ui, section, settings, defaults, view);
                found++;
            }
            return found + ActionRows.BuildMethods(ui, section, settings, everything ? null : query);
        }

        bool methods = ActionRows.HasMethods(settings.GetType());
        if (views.Count == 0 && !methods)
            ui.Label("Nothing to change here.", muted: true);
        // The views come grouped: ungrouped ones first, then a run per group, each in its own card.
        for (int start = 0, end; start < views.Count; start = end)
        {
            string group = views[start].Group;
            for (end = start + 1; end < views.Count && views[end].Group == group; end++) { }
            int from = start, to = end;
            if (group == null)
                BuildFields(ui, section, settings, defaults, views, from, to);
            else
                ui.Card(group, card => BuildFields(card, section, settings, defaults, views, from, to));
        }
        int built = views.Count;
        if (methods)
        {
            ui.Space(10f);
            built += ActionRows.BuildMethods(ui, section, settings);
        }
        if (showReset && views.Count > 0)
        {
            ui.Space(10f);
            BuildReset(ui, section);
        }
        return built;
    }

    /// <summary>Whether the search finds anything in the section, without building it.</summary>
    public static bool Finds(SettingsSection section, string query)
    {
        object settings = section.Value;
        if (settings == null)
            return false;
        if (FieldText.Matches(query, section.Title))
            return true;
        foreach (var view in FieldView.For(settings.GetType()))
        {
            if (FieldText.Matches(query, view.SearchText))
                return true;
        }
        return ActionRows.FindsMethods(settings.GetType(), query);
    }

    private static void BuildFields(UiBuilder ui, SettingsSection section, object settings, object defaults,
        List<FieldView> views, int from, int to)
    {
        for (int i = from; i < to; i++)
            FieldControls.Build(ui, section, settings, defaults, views[i]);
    }

    private static void BuildReset(UiBuilder ui, SettingsSection section)
    {
        ui.Button("Reset to defaults", () => UiKit.Confirm(UIKitMod.Instance, "Reset to defaults",
            $"Put every setting of {section.Title} back to its default?", () =>
            {
                section.Reset();
                ui.Window.Rebuild();
            }));
        ui.Label("Every value on this page goes back to what the mod comes with.", muted: true);
    }
}
