using System;
using System.Collections.Generic;
using ObenLoader;
using ObenLoader.Config;
using ObenLoader.Logging;
using ObenLoader.Runtime;

namespace ObenUIKit.Menu.Settings;

/// <summary>
/// Lets a mod show its config file on the mod menu's Settings page. The public fields of the
/// settings class become controls: bool is an Off/On switch, an enum a cycle, int and float a slider
/// when <see cref="SettingAttribute"/> gives bounds (a cycle of usual values otherwise), string a
/// text field, KeyCode a key bind and Color swatches. Fields can be grouped into cards
/// (<see cref="SettingAttribute.Group"/>), and every config gets a "Reset to defaults" button. Edits are
/// written to the config file as they happen.
/// </summary>
public static class ModSettings
{
    private static readonly List<SettingsSection> Registered = new List<SettingsSection>();

    /// <summary>Every registered config, in the order they were added.</summary>
    public static IReadOnlyList<SettingsSection> Sections => Registered;

    /// <summary>
    /// Shows <paramref name="config"/> on the Settings page for as long as <paramref name="owner"/> is
    /// loaded. The title defaults to the mod's name. Numbers outside their slider's range in the file are put
    /// back in range now, with a warning in the mod's log.
    /// </summary>
    public static void Register<T>(ObenMod owner, ConfigFile<T> config, string title = null) where T : class, new()
    {
        title = title ?? owner.Info.Manifest.DisplayName;
        if (Clamp(config.Value, config.Defaults, owner.Log, title))
            config.Save();
        var section = new SettingsSection(title, owner.Info.Id, () => config.Value, config.Save, () => config.Defaults);
        Registered.Add(section);
        owner.Track(Disposable.Create(() =>
        {
            section.Flush();
            Registered.Remove(section);
            ModMenu.Rebuild();
        }));
        ModMenu.Rebuild();
    }

    /// <summary>Writes every config with an edit still waiting (a slider that was just dragged).</summary>
    internal static void FlushAll()
    {
        for (int i = 0; i < Registered.Count; i++)
            Registered[i].Flush();
    }

    // Puts number fields with a range back inside it. True when something changed.
    private static bool Clamp(object settings, object defaults, ModLogger log, string title)
    {
        bool changed = false;
        foreach (var view in FieldView.For(settings.GetType()))
        {
            if (!view.HasRange)
                continue;
            double value = Convert.ToDouble(view.Field.GetValue(settings));
            double fixedValue = RangeFix.Fix(value, view.Min, view.Max, Convert.ToDouble(view.Field.GetValue(defaults)));
            if (fixedValue == value)
                continue;
            if (view.Kind == FieldKind.Whole)
                fixedValue = Math.Round(fixedValue);
            view.Field.SetValue(settings, Convert.ChangeType(fixedValue, view.Field.FieldType));
            string why = double.IsNaN(value) || double.IsInfinity(value) ? "not a number" : $"outside {view.Min} to {view.Max}";
            log?.Warn($"{title}: {view.Label} was {value}, {why}; it is {fixedValue} now.");
            changed = true;
        }
        return changed;
    }
}
