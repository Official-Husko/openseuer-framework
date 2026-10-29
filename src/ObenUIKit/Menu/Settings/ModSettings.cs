using System;
using System.Collections.Generic;
using ObenLoader;
using ObenLoader.Config;
using ObenLoader.Logging;
using ObenLoader.Runtime;
using ObenUIKit.Controls;

namespace ObenUIKit.Menu.Settings;

/// <summary>
/// Puts mods' config files on the mod menu's Settings page. Every config a loaded mod opened with
/// <c>LoadConfig</c> shows there by itself, titled with the mod's name; <see cref="Register{T}"/> gives it
/// another title or registers a file loaded some other way. The public fields of the settings class become
/// controls: bool is an Off/On switch, an enum a cycle, int and float a slider when <see cref="SettingAttribute"/>
/// gives bounds (a cycle of usual values otherwise), string a text field (or a cycle with
/// <see cref="SettingAttribute.Choices"/>), KeyCode a key bind, Color swatches, a [Flags] enum a switch per flag,
/// a string list a comma-separated text field and a vector its numbers. Fields can be grouped into cards
/// (<see cref="SettingAttribute.Group"/>); each changed field has a Reset and every config a "Reset to defaults"
/// button. Edits are written to the config file as they happen.
/// </summary>
public static class ModSettings
{
    private static readonly List<SettingsSection> Registered = new List<SettingsSection>();
    private static readonly Dictionary<IConfigFile, SettingsSection> Found = new Dictionary<IConfigFile, SettingsSection>();

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
        var section = new SettingsSection(title, owner.Info.Id, () => config.Value, config.Save, () => config.Defaults, config);
        Fix(section, owner.Log);
        Registered.Add(section);
        owner.Track(Disposable.Create(() =>
        {
            section.Flush();
            Registered.Remove(section);
            ModMenu.Rebuild();
        }));
        ModMenu.Rebuild();
    }

    /// <summary>
    /// Draws the controls of <paramref name="config"/> into any kit window, the same way the Settings page does
    /// (groups, ranges, Reset, action buttons), for a mod that wants its settings inside its own window too.
    /// Edits are saved as they're made; a dragged slider writes the file once it settles. <paramref name="title"/>
    /// names the settings in the reset question (the file name when left out).
    /// </summary>
    public static void Draw<T>(UiBuilder ui, ConfigFile<T> config, string title = null, bool showReset = true) where T : class, new()
    {
        if (ui == null)
            throw new ArgumentNullException(nameof(ui));
        if (config == null)
            throw new ArgumentNullException(nameof(config));
        var section = new SettingsSection(title ?? System.IO.Path.GetFileName(config.Path), "", () => config.Value, config.Save,
            () => config.Defaults, config);
        ConfigEditor.Build(ui, section, null, showReset);
        ui.Window.AddRefresher(section.Flush);
        ui.Window.Track(Disposable.Create(section.Flush));
    }

    /// <summary>
    /// Every config the page shows: the registered ones, then the configs loaded mods opened without registering
    /// them, unless their class is marked <c>[Browsable(false)]</c>.
    /// </summary>
    internal static List<SettingsSection> All()
    {
        var all = new List<SettingsSection>(Registered);
        var seen = new HashSet<IConfigFile>();
        foreach (var section in Registered)
        {
            if (section.Source != null)
                seen.Add(section.Source);
        }

        var stillThere = new HashSet<IConfigFile>();
        foreach (var mod in ObenLoader.Mods.ModRegistry.Mods)
        {
            var instance = mod.State == ObenLoader.Mods.ModState.Loaded ? mod.Instance : null;
            if (instance == null)
                continue;
            int listed = Listed(instance.Configs, seen);
            foreach (var config in instance.Configs)
            {
                if (seen.Contains(config) || FieldView.HiddenClass(config.ValueType))
                    continue;
                stillThere.Add(config);
                if (!Found.TryGetValue(config, out var section))
                {
                    section = Make(instance, config, listed > 1);
                    Found[config] = section;
                }
                all.Add(section);
            }
        }
        Forget(stillThere);
        return all;
    }

    /// <summary>Writes every config with an edit still waiting (a slider that was just dragged).</summary>
    internal static void FlushAll()
    {
        for (int i = 0; i < Registered.Count; i++)
            Registered[i].Flush();
        foreach (var section in Found.Values)
            section.Flush();
    }

    internal static void Init(ObenMod kit)
    {
        Found.Clear();
        kit.Track(Disposable.Create(() =>
        {
            FlushAll();
            Found.Clear();
        }));
    }

    private static int Listed(IReadOnlyList<IConfigFile> configs, HashSet<IConfigFile> registered)
    {
        int count = 0;
        foreach (var config in configs)
        {
            if (!registered.Contains(config) && !FieldView.HiddenClass(config.ValueType))
                count++;
        }
        return count;
    }

    private static SettingsSection Make(ObenMod mod, IConfigFile config, bool several)
    {
        string title = mod.Info.Manifest.DisplayName;
        string file = System.IO.Path.GetFileNameWithoutExtension(config.Path);
        var named = (System.ComponentModel.DisplayNameAttribute)Attribute.GetCustomAttribute(config.ValueType,
            typeof(System.ComponentModel.DisplayNameAttribute));
        if (named != null && !string.IsNullOrEmpty(named.DisplayName))
            title = named.DisplayName;
        else if (several && !string.Equals(file, "config", StringComparison.OrdinalIgnoreCase))
            title += $" ({file})";

        var section = new SettingsSection(title, mod.Info.Id, () => config.Value, config.Save, () => config.Defaults, config)
        {
            Registered = false,
        };
        Fix(section, mod.Log);
        return section;
    }

    // A mod that unloaded (or reloaded) takes its files with it; an edit still waiting is written first.
    private static void Forget(HashSet<IConfigFile> stillThere)
    {
        List<IConfigFile> gone = null;
        foreach (var pair in Found)
        {
            if (!stillThere.Contains(pair.Key))
                (gone ??= new List<IConfigFile>()).Add(pair.Key);
        }
        if (gone == null)
            return;
        foreach (var config in gone)
        {
            Found[config].Flush();
            Found.Remove(config);
        }
    }

    // Number fields with a range are put back inside it, with a warning; the file is written when one moved.
    private static void Fix(SettingsSection section, ModLogger log)
    {
        object settings = section.Value;
        if (settings == null)
            return;
        object defaults = section.Defaults;
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
            log?.Warn($"{section.Title}: {view.Label} was {value}, {why}; it is {fixedValue} now.");
            changed = true;
        }
        if (changed)
            section.Edited(deferred: false);
    }
}
