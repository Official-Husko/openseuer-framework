using System;
using System.Collections.Generic;
using ObenUIKit.Controls;
using UnityEngine;

namespace ObenUIKit.Menu.Settings;

/// <summary>Builds a control for each public field of a settings object and writes edits back, then its action buttons.</summary>
internal static class ConfigEditor
{
    // Offered for colour fields, with the field's own colour first when it isn't one of these.
    private static readonly Color[] Palette =
    {
        Color.white, new Color(0.6f, 0.6f, 0.6f), new Color(0.2f, 0.2f, 0.2f), new Color(0.9f, 0.2f, 0.2f),
        new Color(1f, 0.55f, 0.1f), new Color(1f, 0.85f, 0.2f), new Color(0.35f, 0.8f, 0.3f), new Color(0.2f, 0.75f, 0.8f),
        new Color(0.25f, 0.45f, 0.95f), new Color(0.6f, 0.35f, 0.9f), new Color(0.95f, 0.45f, 0.7f),
    };

    public static void Build(UiBuilder ui, SettingsSection section)
    {
        object settings = section.Value;
        if (settings == null)
            return;

        var views = FieldView.For(settings.GetType());
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
                BuildFields(ui, section, settings, views, from, to);
            else
                ui.Card(group, card => BuildFields(card, section, settings, views, from, to));
        }
        if (methods)
        {
            ui.Space(10f);
            ActionRows.BuildMethods(ui, section, settings);
        }
        if (views.Count > 0)
        {
            ui.Space(10f);
            BuildReset(ui, section);
        }
    }

    private static void BuildFields(UiBuilder ui, SettingsSection section, object settings, List<FieldView> views, int from, int to)
    {
        for (int i = from; i < to; i++)
        {
            var view = views[i];
            BuildField(ui, section, settings, view);
            if (!string.IsNullOrEmpty(view.Tooltip))
                ui.Tooltip(view.Tooltip);
            if (!string.IsNullOrEmpty(view.Description))
                ui.Label(view.Description, muted: true);
        }
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

    private static void BuildField(UiBuilder ui, SettingsSection section, object settings, FieldView view)
    {
        var field = view.Field;
        object value = field.GetValue(settings);

        void Set(object v, bool deferred = false)
        {
            field.SetValue(settings, v);
            section.Edited(deferred);
        }

        switch (view.Kind)
        {
            case FieldKind.Switch:
                ui.Toggle(view.Label, (bool)value, on => Set(on));
                break;
            case FieldKind.Choice:
                int current = Math.Max(0, Array.IndexOf(view.ChoiceNames, value.ToString()));
                ui.Cycle(view.Label, view.ChoiceLabels, current, i => Set(Enum.Parse(field.FieldType, view.ChoiceNames[i])));
                break;
            case FieldKind.Whole:
            case FieldKind.Decimal:
                BuildNumber(ui, view, Convert.ToDouble(value), (v, deferred) => Set(Convert.ChangeType(v, field.FieldType), deferred));
                break;
            case FieldKind.Text:
                // Typing is written like a slider drag, once it settles; leaving the field writes it at once.
                ui.TextField(view.Label, (string)value ?? "", v => Set(v, deferred: true), v => Set(v));
                break;
            case FieldKind.Key:
                ui.KeyBind(view.Label, (KeyCode)value, k => Set(k));
                break;
            case FieldKind.Color:
                BuildColor(ui, view, (Color)value, c => Set(c));
                break;
            default:
                ui.Label($"{view.Label}: edit in the config file", muted: true);
                break;
        }
    }

    private static void BuildColor(UiBuilder ui, FieldView view, Color value, Action<Color> set)
    {
        int index = Array.FindIndex(Palette, c => Close(c, value));
        var colors = Palette;
        if (index < 0)
        {
            colors = new Color[Palette.Length + 1];
            colors[0] = value;
            Array.Copy(Palette, 0, colors, 1, Palette.Length);
            index = 0;
        }
        ui.Swatches(view.Label, colors, index, i => set(colors[i]));
    }

    private static bool Close(Color a, Color b) =>
        Mathf.Abs(a.r - b.r) < 0.01f && Mathf.Abs(a.g - b.g) < 0.01f && Mathf.Abs(a.b - b.b) < 0.01f && Mathf.Abs(a.a - b.a) < 0.01f;

    private static void BuildNumber(UiBuilder ui, FieldView view, double value, Action<double, bool> set)
    {
        bool whole = view.Kind == FieldKind.Whole;
        if (view.HasRange)
        {
            string format = whole || view.Decimals == 0 ? "0" : "0." + new string('0', view.Decimals);
            ui.Slider(view.Label, view.Min, view.Max, (float)value, v =>
                set(whole ? Math.Round(v) : Math.Round(v, view.Decimals), true), format, whole);
            return;
        }

        double[] steps = NumberSteps.Around(value, whole, out int index);
        ui.Cycle(view.Label, NumberSteps.Labels(steps, whole), index, i => set(steps[i], false));
    }
}
