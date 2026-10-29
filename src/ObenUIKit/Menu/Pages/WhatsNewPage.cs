using System.Collections.Generic;
using System.Linq;
using ObenUIKit.Controls;
using ObenUIKit.Menu.Changelog;
using TMPro;
using UnityEngine;

namespace ObenUIKit.Menu.Pages;

/// <summary>
/// Release notes from changelog.json, newest release first. A release gets a title bar, its date and how
/// many lines of each kind it has; inside it every mod gets a card (alphabetical, lines without a
/// mod last), each line tagged with its kind in that kind's color: feat (added), change, fix and remove.
/// </summary>
internal static class WhatsNewPage
{
    public const string Title = "What's New";

    private const int MaxReleases = 10;
    private const string NoArea = "General";

    private sealed class Kind
    {
        public string Tag;
        public string Color;
        public List<ChangelogItem> Items;
    }

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
        var theme = UiKit.Theme;
        // Picked as sRGB and converted: the game renders in linear space. Remove uses the game's own red.
        var kinds = new[]
        {
            new Kind { Tag = "feat", Color = Hex(new Color(0.55f, 0.85f, 0.5f).linear), Items = release.Added },
            new Kind { Tag = "change", Color = Hex(new Color(0.92f, 0.72f, 0.35f).linear), Items = release.Changed },
            new Kind { Tag = "fix", Color = Hex(new Color(0.55f, 0.75f, 0.95f).linear), Items = release.Fixed },
            new Kind { Tag = "remove", Color = Hex(theme.TextHover), Items = release.Removed },
        };
        string muted = Hex(theme.TextMuted);
        string text = Hex(theme.Text);

        ui.Header(release.Released ? $"Version {release.Version}" : "In progress");
        if (!string.IsNullOrEmpty(release.Date))
            ui.Label(release.Released ? release.Date : "Not released yet, as of " + release.Date, muted: true);
        var summary = new List<string>();
        foreach (var kind in kinds)
        {
            int count = kind.Items?.Count ?? 0;
            if (count > 0)
                summary.Add(Paint(kind.Color, $"{count} {kind.Tag}"));
        }
        if (summary.Count > 0)
            ui.Label(string.Join(Paint(muted, "  ·  "), summary));

        var areas = kinds.SelectMany(k => k.Items ?? new List<ChangelogItem>())
            .Select(item => item.Area)
            .Distinct()
            .OrderBy(area => area == null)
            .ThenBy(area => area, System.StringComparer.OrdinalIgnoreCase);

        foreach (string area in areas)
        {
            ui.Card(area ?? NoArea, card =>
            {
                foreach (var kind in kinds)
                {
                    if (kind.Items == null)
                        continue;
                    foreach (var item in kind.Items)
                    {
                        // The indent keeps wrapped lines clear of the tag, so the text reads as one column.
                        if (item.Area == area)
                            card.Label(Paint(kind.Color, kind.Tag) + "<indent=4.2em>" + Paint(text, item.Text) + "</indent>",
                                align: TextAlignmentOptions.Left);
                    }
                }
            });
        }
        ui.Space(24f);
    }

    private static string Paint(string color, string text) => KitText.Paint(color, text);

    private static string Hex(Color color) => "#" + ColorUtility.ToHtmlStringRGB(color);
}
