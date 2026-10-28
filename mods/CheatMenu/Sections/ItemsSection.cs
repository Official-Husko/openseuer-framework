using System.Collections.Generic;
using ObenFramework.Game;
using ObenFramework.Items;
using ObenUIKit.Controls;

namespace CheatMenu.Sections;

/// <summary>A short list of useful items to hand out. There are no text fields in the kit yet to search with.</summary>
internal static class ItemsSection
{
    // Ids from the game's Items.json (version 0.5.13). One that isn't in the database is left out.
    private static readonly int[] Curated =
    {
        4,      // Lock Pick
        10940,  // Bandage
        14880,  // Water
        24100,  // Rye Bread
        24080,  // Canned Meat
        13380,  // Coffee
        140010, // Painkillers
        14180,  // Stollandia Vodka
        100010, // Pack of Ducky Strike Cigarettes
        15460,  // Lighter
        10570,  // Flashlight
        19690,  // Crowbar
        35100,  // Duct Tape
        35180,  // Scrap Metal
        35050,  // Nails
        35010,  // Plank
        13450,  // Wood Log
        12680,  // Cloth Scraps
        15770,  // Copper Wire
        20170,  // Car Battery
    };

    public static void Build(UiBuilder ui, CheatContext ctx)
    {
        ui.Header("Items");
        if (!WorldState.IsReady || !PlayerItems.IsReady)
        {
            ui.Label(CheatContext.NeedsGame, muted: true);
            return;
        }

        var ids = new List<int>();
        var titles = new List<string>();
        foreach (int id in Curated)
        {
            var item = ItemLookup.ById(id);
            if (item == null)
                continue;
            ids.Add(id);
            titles.Add(item.Title);
        }
        if (ids.Count == 0)
        {
            ui.Label("None of the listed items exist in this version of the game.", muted: true);
            return;
        }

        int chosen = 0;
        ui.Cycle("Item", titles.ToArray(), chosen, i => chosen = i);
        ui.Row(r =>
        {
            r.Button("Give 1", () => Give(ctx, ids[chosen], titles[chosen], 1));
            r.Button("Give 10", () => Give(ctx, ids[chosen], titles[chosen], 10));
        });
    }

    private static void Give(CheatContext ctx, int id, string title, int amount) =>
        ctx.Run("Give item", () =>
        {
            bool fitted = PlayerItems.Give(id, amount);
            ctx.Say(fitted
                ? $"Gave {amount} x {title}."
                : $"Gave {amount} x {title}; what didn't fit is on the ground at your feet.");
        });
}
