using System.Collections.Generic;
using System.Linq;
using ObenUIKit.Controls;
using UnityEngine;

namespace ObenUIKit.Menu.Pages;

/// <summary>
/// Every hotkey the loaded mods registered with <see cref="Hotkeys"/>, grouped by mod, each with a
/// key binding row to change the key and a cycle to add Ctrl, Shift or Alt. A line under them says what
/// the chord is now and whether it clashes with something.
/// </summary>
internal static class HotkeysPage
{
    public const string Title = "Hotkeys";

    // The cycle's index is the modifier flags themselves: Ctrl is 1, Shift 2, Alt 4.
    private static readonly string[] ModifierNames =
    {
        "None", "Ctrl", "Shift", "Ctrl + Shift", "Alt", "Ctrl + Alt", "Shift + Alt", "Ctrl + Shift + Alt",
    };

    public static ModPage Create() => new ModPage(Title, Build) { Order = 15 };

    private static void Build(UiBuilder ui)
    {
        var all = Hotkeys.All;
        if (all.Count == 0)
        {
            ui.Label("No mod has registered a hotkey yet.", muted: true);
            return;
        }

        ui.Label("Click a key, then press the new one. Pick Ctrl, Shift or Alt under it to make a combination.", muted: true);
        ui.Space(6f);

        foreach (var group in all.GroupBy(h => h.OwnerName).OrderBy(g => g.Key))
        {
            ui.Header(group.Key);
            foreach (var hotkey in group)
                BuildRow(ui, hotkey);
        }

        ui.Space(10f);
        ui.Row(r => r.Button("Reset all", ConfirmResetAll));
    }

    private static void BuildRow(UiBuilder ui, Hotkey hotkey)
    {
        KeyBindHandle bind = ui.KeyBind(hotkey.Label, hotkey.Chord.Key,
            key => hotkey.Rebind(new KeyChord(key, hotkey.Chord.Modifiers)));
        CycleHandle modifiers = ui.Cycle("With", ModifierNames, (int)hotkey.Chord.Modifiers,
            i => hotkey.Rebind(new KeyChord(hotkey.Chord.Key, (KeyModifiers)i)));
        ui.Live(() => Status(hotkey), muted: true);
        ui.Row(r =>
        {
            r.Button("Reset", () =>
            {
                hotkey.Reset();
                bind.Set(hotkey.Chord.Key);
                modifiers.Set((int)hotkey.Chord.Modifiers);
            });
            r.Button("Unbind", () =>
            {
                hotkey.Rebind(default);
                bind.Set(KeyCode.None);
                modifiers.Set(0);
            });
        });
    }

    private static string Status(Hotkey hotkey)
    {
        string text = hotkey.Chord.ToString();
        if (!hotkey.IsDefault)
            text += $"  (default {hotkey.Default})";
        string clash = Hotkeys.ConflictOf(hotkey);
        return clash == null ? text : text + "\n" + clash;
    }

    private static void ConfirmResetAll()
    {
        var kit = UIKitMod.Instance;
        if (kit == null)
            return;
        UiKit.Confirm(kit, "Reset all hotkeys?", "Every mod's hotkeys go back to their defaults.", () =>
        {
            foreach (var hotkey in new List<Hotkey>(Hotkeys.All))
                hotkey.Reset();
            ModMenu.Rebuild();
        });
    }
}
