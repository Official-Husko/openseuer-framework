using System.Collections.Generic;
using System.Linq;
using ObenUIKit.Controls;
using ObenUIKit.Menu.Changelog;
using TMPro;

namespace ObenUIKit.Menu.Pages;

/// <summary>
/// Release notes from changelog.json, newest release first. Inside a release the lines are grouped by
/// mod, then by Added, Changed, Fixed and Removed.
/// </summary>
internal static class WhatsNewPage
{
    public const string Title = "What's New";

    private const int MaxReleases = 10;
    private const string NoArea = "General";

    public static ModPage Create() => new ModPage(Title, Build) { Order = 20 };

    private static void Build(UiBuilder ui)
    {
        var document = ChangelogFeed.Load();
        if (document == null || document.Releases.Count == 0)
        {
            ui.Label("No changelog found.", muted: true);
            return;
        }

        for (int i = 0; i < document.Releases.Count && i < MaxReleases; i++)
            BuildRelease(ui, document.Releases[i]);
        if (document.Releases.Count > MaxReleases)
            ui.Label("Older releases are in changelog.json.", muted: true);
    }

    private static void BuildRelease(UiBuilder ui, ChangelogRelease release)
    {
        bool inProgress = !release.Released;
        ui.Header(inProgress ? "In progress" : $"Version {release.Version}");
        if (!string.IsNullOrEmpty(release.Date))
            ui.Label(inProgress ? $"Not released yet, as of {release.Date}." : release.Date, muted: true);

        var kinds = new (string Name, List<ChangelogItem> Items)[]
        {
            ("Added", release.Added),
            ("Changed", release.Changed),
            ("Fixed", release.Fixed),
            ("Removed", release.Removed),
        };
        var areas = kinds.SelectMany(k => k.Items ?? new List<ChangelogItem>())
            .Select(item => item.Area ?? NoArea)
            .Distinct();

        foreach (string area in areas)
        {
            ui.Space(6f);
            ui.Label(area, align: TextAlignmentOptions.Left);
            foreach (var (name, items) in kinds)
            {
                if (items == null)
                    continue;
                var lines = items.Where(item => (item.Area ?? NoArea) == area).ToList();
                if (lines.Count == 0)
                    continue;
                ui.Label(name, muted: true, align: TextAlignmentOptions.Left);
                foreach (var item in lines)
                    ui.Label("- " + item.Text, align: TextAlignmentOptions.Left);
            }
        }
        ui.Space(16f);
    }
}
