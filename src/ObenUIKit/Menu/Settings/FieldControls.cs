using System;
using System.Collections.Generic;
using ObenUIKit.Controls;
using UnityEngine;

namespace ObenUIKit.Menu.Settings;

/// <summary>
/// The control for one settings field, its help text and its Reset. A field that fails to build is logged and
/// shown as a line saying so; the rest of the page builds as usual.
/// </summary>
internal static class FieldControls
{
    // Offered for colour fields, with the field's own colour first when it isn't one of these.
    private static readonly Color[] Palette =
    {
        Color.white, new Color(0.6f, 0.6f, 0.6f), new Color(0.2f, 0.2f, 0.2f), new Color(0.9f, 0.2f, 0.2f),
        new Color(1f, 0.55f, 0.1f), new Color(1f, 0.85f, 0.2f), new Color(0.35f, 0.8f, 0.3f), new Color(0.2f, 0.75f, 0.8f),
        new Color(0.25f, 0.45f, 0.95f), new Color(0.6f, 0.35f, 0.9f), new Color(0.95f, 0.45f, 0.7f),
    };

    public static void Build(UiBuilder ui, SettingsSection section, object settings, object defaults, FieldView view)
    {
        try
        {
            ResetMark mark = null;
            void Set(object v, bool deferred)
            {
                view.Field.SetValue(settings, v);
                section.Edited(deferred);
                mark?.Refresh();
            }

            if (Control(ui, section, view, view.Field.GetValue(settings), Set))
            {
                string tip = "Default: " + Describe(view.Field.GetValue(defaults));
                mark = ui.ResetMark(ui.LastHeader, tip, () => view.Differs(settings, defaults), () =>
                {
                    section.Reset(view);
                    ui.Window.Rebuild();
                });
            }
            if (!string.IsNullOrEmpty(view.Tooltip))
                ui.Tooltip(view.Tooltip);
            if (!string.IsNullOrEmpty(view.Description))
                ui.Label(view.Description, muted: true);
        }
        catch (Exception e)
        {
            Runtime.KitRuntime.Current?.Kit.Log.Error($"{section.Title}: the setting '{view.Label}' couldn't be shown", e);
            ui.Label($"{view.Label}: couldn't be shown (the details are in the log).", muted: true);
        }
    }

    // Builds the control. False for a field the page can't edit, which gets no Reset.
    private static bool Control(UiBuilder ui, SettingsSection section, FieldView view, object value, Action<object, bool> set)
    {
        var type = view.Field.FieldType;
        switch (view.Kind)
        {
            case FieldKind.Switch:
                ui.Toggle(view.Label, (bool)value, on => set(on, false));
                return true;
            case FieldKind.Choice:
                int current = Math.Max(0, Array.IndexOf(view.ChoiceNames, value.ToString()));
                ui.Cycle(view.Label, view.ChoiceLabels, current, i => set(Enum.Parse(type, view.ChoiceNames[i]), false));
                return true;
            case FieldKind.Pick:
                var options = FieldText.WithCurrent(view.ChoiceNames, (string)value, out int picked);
                ui.Cycle(view.Label, options, picked, i => set(options[i], false));
                return true;
            case FieldKind.Whole:
            case FieldKind.Decimal:
                BuildNumber(ui, view, Convert.ToDouble(value), (v, deferred) => set(Convert.ChangeType(v, type), deferred));
                return true;
            case FieldKind.Text:
                // Typing is written like a slider drag, once it settles; leaving the field writes it at once.
                ui.TextField(view.Label, (string)value ?? "", v => set(v, true), v => set(v, false));
                return true;
            case FieldKind.TextList:
                ui.TextField(view.Label, FieldText.JoinList((IEnumerable<string>)value), null, v =>
                {
                    var items = FieldText.SplitList(v);
                    set(type == typeof(string[]) ? items.ToArray() : (object)items, false);
                });
                return true;
            case FieldKind.Key:
                ui.KeyBind(view.Label, (KeyCode)value, k => set(k, false));
                return true;
            case FieldKind.Color:
                BuildColor(ui, view, (Color)value, c => set(c, false));
                return true;
            case FieldKind.Vector:
                BuildVector(ui, view, value, v => set(v, false));
                return true;
            case FieldKind.Flags:
                BuildFlags(ui, view, value, v => set(v, false));
                return false;
            default:
                string file = section.Source != null ? System.IO.Path.GetFileName(section.Source.Path) : "the config file";
                ui.Label($"{view.Label}: change it in {file}", muted: true);
                return false;
        }
    }

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

    // "x, y, z" in a text field; something that doesn't read as the right count of numbers puts the old value back.
    private static void BuildVector(UiBuilder ui, FieldView view, object value, Action<object> set)
    {
        string shown = FieldText.JoinNumbers(Numbers(value));
        FieldHandle field = null;
        field = ui.TextField(view.Label, shown, null, text =>
        {
            if (!FieldText.TryNumbers(text, view.VectorSize, out var n))
            {
                field.Set(shown);
                return;
            }
            shown = FieldText.JoinNumbers(n);
            field.Set(shown);
            set(view.VectorSize == 2 ? new Vector2(n[0], n[1])
                : view.VectorSize == 3 ? new Vector3(n[0], n[1], n[2])
                : (object)new Vector4(n[0], n[1], n[2], n[3]));
        });
    }

    private static float[] Numbers(object value) => value switch
    {
        Vector2 v => new[] { v.x, v.y },
        Vector3 v => new[] { v.x, v.y, v.z },
        Vector4 v => new[] { v.x, v.y, v.z, v.w },
        _ => Array.Empty<float>(),
    };

    // A card with an Off/On switch per flag.
    private static void BuildFlags(UiBuilder ui, FieldView view, object value, Action<object> set)
    {
        var type = view.Field.FieldType;
        long bits = Convert.ToInt64(value);
        ui.Card(view.Label, card =>
        {
            for (int i = 0; i < view.FlagValues.Length; i++)
            {
                long flag = view.FlagValues[i];
                card.Toggle(view.ChoiceLabels[i], (bits & flag) != 0, on =>
                {
                    bits = on ? bits | flag : bits & ~flag;
                    set(Enum.ToObject(type, bits));
                });
            }
        });
    }

    private static string Describe(object value) => value switch
    {
        Color c => "#" + ColorUtility.ToHtmlStringRGBA(c),
        Vector2 or Vector3 or Vector4 => FieldText.JoinNumbers(Numbers(value)),
        KeyCode key => key.ToString(),
        _ => FieldText.Describe(value),
    };
}
