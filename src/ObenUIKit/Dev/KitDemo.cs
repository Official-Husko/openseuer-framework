using System;
using ObenFramework.Items;
using ObenUIKit.Controls;
using ObenUIKit.Windows;
using UnityEngine;

namespace ObenUIKit.Dev;

/// <summary>
/// A window with every kit control on it, for trying the kit out and for checking it after changes. Open it
/// from the dev bridge (<c>oben call ui.demo</c>) or from code with <see cref="Open"/>.
/// </summary>
public static class KitDemo
{
    public const string WindowId = "obenuikit.demo";

    private static readonly string[] Speeds = { "Slow", "Normal", "Fast", "Silly" };
    private static readonly Color[] Colours =
    {
        new Color(0.9f, 0.2f, 0.2f), new Color(1f, 0.6f, 0.1f), new Color(0.95f, 0.85f, 0.2f),
        new Color(0.3f, 0.8f, 0.3f), new Color(0.2f, 0.6f, 0.95f), new Color(0.6f, 0.35f, 0.9f), Color.white,
    };

    private static int _speed = 1;
    private static int _colour;
    private static float _copies = 3f;
    private static float _scale = 1f;
    private static string _name = "Rat";

    /// <summary>Opens the demo window (or brings it back).</summary>
    public static UiWindow Open()
    {
        var kit = UIKitMod.Instance ?? throw new InvalidOperationException("ObenUIKit isn't loaded.");
        return UiKit.Open(kit, WindowId, "UI kit demo", Build, new WindowOptions { Width = 900f });
    }

    private static void Build(UiBuilder ui)
    {
        ui.Label("Every control of the kit. Tabs keep their page when the window rebuilds.", muted: true);
        ui.Tabs(new[] { "Choices", "Lists", "Model", "Windows", "HUD", "More" }, (i, tab) =>
        {
            switch (i)
            {
                case 0: Choices(tab); break;
                case 1: Lists(tab); break;
                case 2: Model(tab); break;
                case 3: KitDemoWindows.Build(tab); break;
                case 4: KitDemoHud.Build(tab); break;
                default: KitDemoMore.Build(tab); break;
            }
        });
    }

    private static void Choices(UiBuilder ui)
    {
        ui.Segmented("Segmented", Speeds, _speed, i => { _speed = i; UiKit.Toast("Speed: " + Speeds[i]); });
        ui.Tooltip("A segmented choice shows every option; left and right pick.");
        ui.Swatches("Swatches", Colours, _colour, i => _colour = i);
        ui.Tooltip("Colours are drawn the way an image editor shows them.");
        ui.Stepper("Stepper (whole)", _copies, 1f, 10f, 1f, v => _copies = v);
        ui.Stepper("Stepper (decimal)", _scale, 0.25f, 4f, 0.25f, v => _scale = v, "0.00x");
        ui.TextField("Text field", _name, v => _name = v);
        ui.Card("A card", c =>
        {
            c.Label("Cards group controls in a framed box.", muted: true);
            c.Toggle("Inside a card", true, on => { });
            c.Row(r =>
            {
                r.Button("Toast", () => UiKit.Toast("Hello from a card"));
                r.Tooltip("Shows a notice through the game's notifications.");
                r.Button("Confirm", () => UiKit.Confirm(UIKitMod.Instance, "Sure?", "Nothing happens either way.", () => UiKit.Toast("Yes")));
            });
        });
        ui.Live(() => $"Picked: {Speeds[_speed]}, colour {_colour + 1}, {_copies:0} copies at {_scale:0.00}x, name '{_name}'", muted: true);
    }

    private static void Lists(UiBuilder ui)
    {
        var items = ItemLookup.All;
        ui.Label($"{items.Count} items, only the visible rows exist. Enter or a click picks.", muted: true);
        ui.List(items.Count, (i, row) =>
        {
            var item = items[i];
            row.Text = item.Title;
            row.Detail = "#" + item.ID;
            row.Muted = item.Appearance == null || string.IsNullOrEmpty(item.Appearance.PrefabPath);
        }, i => UiKit.Toast(items[i].Title + " (#" + items[i].ID + ")"), rows: 9);

        var numbers = ui.List(100000, (i, row) =>
        {
            row.Text = "Row " + i;
            row.Detail = (i * 7 % 13).ToString();
        }, rows: 5);
        ui.Row(r =>
        {
            r.Button("Top", () => numbers.ScrollTo(0));
            r.Button("Middle", () => numbers.ScrollTo(50000));
            r.Button("End", () => numbers.ScrollTo(99999));
        });
    }

    private static void Model(UiBuilder ui)
    {
        var models = ObenFramework.Assets.ModelRegistry.All();
        ui.Label("Drag to turn, scroll to zoom, Enter puts it back. It only draws while it changes or spins.", muted: true);
        var preview = ui.ModelPreview(380f);
        int item = 0;
        preview.ShowItem(item);
        preview.Spin = 25f;

        ui.Stepper("Item id", item, 0, Math.Max(0, ItemLookup.Count - 1), 1, v =>
        {
            if (!preview.ShowItem((int)v))
                UiKit.Toast("That item has no world model.");
        });
        ui.Segmented("Spin", new[] { "Still", "Slow", "Fast" }, 1, i => preview.Spin = i == 0 ? 0f : i == 1 ? 25f : 90f);
        if (models.Count > 0)
        {
            var names = new string[models.Count];
            for (int i = 0; i < names.Length; i++)
                names[i] = models[i].Name;
            ui.Cycle("Mod model", names, 0, i => preview.Show(models[i]));
        }
        else
        {
            ui.Label("No mod has loaded an OBJ model; ModelRegistry models show here too.", muted: true);
        }
    }
}
