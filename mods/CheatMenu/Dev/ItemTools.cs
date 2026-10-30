// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
using System.Collections.Generic;
using ObenFramework.Items;
using ObenUIKit;
using ObenUIKit.Controls;

namespace CheatMenu.Dev;

/// <summary>
/// Items in the Player and world part: find any item by part of its name, its id or the mod that added it
/// ("mod:MyMod") and give any number of it; and the stacks of items whose mod is gone (placeholders), with
/// a button per mod that removes them from the inventory and the containers loaded. Through the framework's
/// <see cref="ItemLookup"/>, <see cref="PlayerItems"/>, <see cref="ItemRegistry"/> and <see cref="ItemPlaceholders"/>.
/// </summary>
internal static class ItemTools
{
    private const int MaxItems = 20;
    private const int MaxMissing = 20;
    private const string ModPrefix = "mod:";

    private static string _filter = "";
    private static float _count = 1f;

    public static void Build(UiBuilder ui, CheatContext ctx)
    {
        BuildFind(ui, ctx);
        BuildMissing(ui, ctx);
    }

    private static void BuildFind(UiBuilder ui, CheatContext ctx)
    {
        ui.Header("Find an item");
        ui.TextField("Name, id or mod:(mod id), Enter to search", _filter, t => _filter = t, t =>
        {
            _filter = t;
            ui.Window.Rebuild();
        });
        ui.Stepper("How many", _count, 1f, 999f, 1f, v => _count = v);
        string filter = (_filter ?? "").Trim();
        if (filter.Length == 0)
            return;

        var found = Find(filter, out int matched);
        ui.Label(matched == 0 ? "No item matches." : $"Showing {found.Count} of {matched} items.", muted: true);
        foreach (var item in found)
        {
            int id = item.ID;
            string title = item.Title;
            string owner = ItemRegistry.OwnerOf(id);
            ui.Button(UiText.Plain($"Give {title} ({id}" + (owner != null ? ", " + owner : "") + ")"), () => Give(ctx, id, title, (int)_count));
        }
    }

    private static void BuildMissing(UiBuilder ui, CheatContext ctx)
    {
        ui.Header("Items of removed mods");
        if (!ItemPlaceholders.IsAvailable)
        {
            ui.Label("The framework's placeholders are switched off, so items of removed mods aren't kept.", muted: true);
            return;
        }
        var missing = ItemPlaceholders.Scan();
        if (missing.Count == 0)
        {
            ui.Label("No stack belongs to a mod that isn't loaded. Such stacks are kept as placeholders and show here.", muted: true);
            return;
        }
        ui.Label($"{missing.Count} stacks belong to mods that aren't loaded. Removing a mod's stacks deletes them from the inventory and the containers loaded.", muted: true);
        var mods = new List<string>();
        foreach (var stack in missing)
        {
            string mod = stack.Mod ?? "";
            if (!mods.Contains(mod))
                mods.Add(mod);
        }
        foreach (string mod in mods)
        {
            string target = mod;
            string name = target.Length == 0 ? "an unknown mod" : target;
            ui.Button(UiText.Plain("Remove the stacks of " + name), () => UiKit.Confirm(DevWindow.Owner ?? (ObenLoader.Runtime.ITracker)ctx.Mod,
                "Remove these stacks?", UiText.Plain($"Every stack of {name}'s items goes from the inventory and the containers loaded. The mod can't bring them back."),
                () => DevActions.Act(ctx, ui, "Remove the stacks of " + name,
                    () => (target.Length == 0 ? ItemPlaceholders.RemoveAllMissing() : ItemPlaceholders.RemoveMissing(target)) > 0,
                    "nothing was removed (a save or load running?).")));
        }
        for (int i = 0; i < missing.Count && i < MaxMissing; i++)
            ui.Label(UiText.Plain(missing[i].ToString()), muted: true);
        if (missing.Count > MaxMissing)
            ui.Label($"And {missing.Count - MaxMissing} more.", muted: true);
    }

    private static List<Item> Find(string filter, out int matched)
    {
        var found = new List<Item>();
        matched = 0;
        bool byMod = filter.StartsWith(ModPrefix, System.StringComparison.OrdinalIgnoreCase);
        string mod = byMod ? filter.Substring(ModPrefix.Length).Trim() : null;
        bool byId = int.TryParse(filter, out int wanted);
        foreach (var item in ItemLookup.All)
        {
            if (item == null)
                continue;
            bool match = byMod ? string.Equals(ItemRegistry.OwnerOf(item.ID), mod, System.StringComparison.OrdinalIgnoreCase)
                : byId ? item.ID == wanted
                : DevRules.Matches(item.Title, filter);
            if (!match)
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
