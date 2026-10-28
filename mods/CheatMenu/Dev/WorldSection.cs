// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
using System.Collections.Generic;
using ObenFramework.Game;
using ObenFramework.Items;
using ObenFramework.Player;
using ObenFramework.Transport;
using ObenUIKit.Controls;

namespace CheatMenu.Dev;

/// <summary>
/// Player and world tools beyond the Cheats window: set any player stat by its console name, go to an
/// entrypoint of the level, and find any item by name or id to hand out. Through the framework's
/// <see cref="PlayerStat"/>, <see cref="Travel"/>, <see cref="ItemLookup"/> and <see cref="PlayerItems"/>.
/// </summary>
internal static class WorldSection
{
    private const int MaxItems = 10;

    private static int _stat;
    private static int _statShown = -1;
    private static float _statValue;
    private static int _entry;
    private static string _itemFilter = "";

    public static void Build(UiBuilder ui, CheatContext ctx)
    {
        if (!WorldState.IsReady)
        {
            ui.Header("Player and world");
            ui.Label(CheatContext.NeedsGame, muted: true);
            return;
        }
        BuildStats(ui, ctx);
        BuildEntrypoints(ui, ctx);
        BuildItems(ui, ctx);
    }

    private static void BuildStats(UiBuilder ui, CheatContext ctx)
    {
        ui.Header("Player stats");
        var stats = PlayerStat.All;
        if (stats.Count == 0)
            return;
        if (_stat >= stats.Count)
            _stat = 0;
        var names = new string[stats.Count];
        for (int i = 0; i < stats.Count; i++)
            names[i] = stats[i].Name;

        var stat = stats[_stat];
        if (_statShown != _stat)
        {
            // The slider starts at the stat's current value whenever another stat is picked.
            _statShown = _stat;
            _statValue = stat.TryGet(out float now) ? now : 0f;
        }
        ui.Cycle("Stat", names, _stat, i =>
        {
            _stat = i;
            ui.Window.Rebuild();
        });
        ui.Live(() => stat.TryGet(out float value) ? $"{stat.Description} Now {value:0.##}." : "Not available here.", muted: true);
        float min = stat.Min ?? -100f;
        float max = stat.Max ?? System.Math.Max(100f, min + 100f);
        ui.Slider("Value", min, max, UnityEngine.Mathf.Clamp(_statValue, min, max), v => _statValue = v, "0.#");
        ui.Button("Set it", () => DevActions.Change(ctx, ui, $"{stat.Name} to {_statValue:0.#}", () => stat.Set(_statValue)));
    }

    private static void BuildEntrypoints(UiBuilder ui, CheatContext ctx)
    {
        ui.Header("Entrypoints of this level");
        // A level can list the same entrypoint more than once.
        var points = new List<string>();
        foreach (string name in Travel.Entrypoints())
        {
            if (!points.Contains(name))
                points.Add(name);
        }
        if (points.Count == 0)
        {
            ui.Label("This level has no entrypoints.", muted: true);
            return;
        }
        if (_entry >= points.Count)
            _entry = 0;
        ui.Cycle("Entrypoint", points.ToArray(), _entry, i => _entry = i);
        ui.Button("Go there", () => ctx.Run("Teleport", () =>
        {
            string name = points[_entry];
            ctx.Say(Travel.TeleportTo(name) ? $"At {name}." : $"Couldn't go to {name}.");
        }));
    }

    private static void BuildItems(UiBuilder ui, CheatContext ctx)
    {
        ui.Header("Find an item");
        ui.TextField("Name or id (Enter to search)", _itemFilter, t => _itemFilter = t, t =>
        {
            _itemFilter = t;
            ui.Window.Rebuild();
        });
        string filter = (_itemFilter ?? "").Trim();
        if (filter.Length == 0)
            return;

        var found = Find(filter, out int matched);
        ui.Label(matched == 0 ? "No item matches." : $"Showing {found.Count} of {matched} items.", muted: true);
        foreach (var item in found)
        {
            int id = item.ID;
            string title = item.Title;
            ui.Row(r =>
            {
                r.Button($"Give {title} ({id})", () => Give(ctx, id, title, 1));
                r.Button("x10", () => Give(ctx, id, title, 10));
            });
        }
    }

    private static List<Item> Find(string filter, out int matched)
    {
        var found = new List<Item>();
        matched = 0;
        bool byId = int.TryParse(filter, out int wanted);
        foreach (var item in ItemLookup.All)
        {
            if (item == null || !(byId ? item.ID == wanted : DevRules.Matches(item.Title, filter)))
                continue;
            matched++;
            if (found.Count < MaxItems)
                found.Add(item);
        }
        return found;
    }

    private static void Give(CheatContext ctx, int id, string title, int amount) =>
        ctx.Run("Give item", () =>
        {
            bool fitted = PlayerItems.Give(id, amount);
            ctx.Say(fitted ? $"Gave {amount} x {title}." : $"Gave {amount} x {title}; what didn't fit is at your feet.");
        });
}
