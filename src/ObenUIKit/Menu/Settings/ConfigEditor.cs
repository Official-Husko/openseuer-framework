using System;
using ObenUIKit.Controls;

namespace ObenUIKit.Menu.Settings;

/// <summary>Builds a control for each public field of a settings object and writes edits back.</summary>
internal static class ConfigEditor
{
    public static void Build(UiBuilder ui, SettingsSection section)
    {
        object settings = section.Value;
        if (settings == null)
            return;

        var views = FieldView.For(settings.GetType());
        if (views.Count == 0)
            ui.Label("Nothing to change here.", muted: true);
        foreach (var view in views)
        {
            BuildField(ui, section, settings, view);
            if (!string.IsNullOrEmpty(view.Description))
                ui.Label(view.Description, muted: true);
        }
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
                ui.Label($"{view.Label}: {value}");
                ui.Label("Edit this one in the config file for now.", muted: true);
                break;
            default:
                ui.Label($"{view.Label}: edit in the config file", muted: true);
                break;
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
}
