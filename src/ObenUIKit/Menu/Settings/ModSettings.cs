using System.Collections.Generic;
using ObenLoader;
using ObenLoader.Config;
using ObenLoader.Runtime;

namespace ObenUIKit.Menu.Settings;

/// <summary>
/// Lets a mod show its config file on the mod menu's Settings page. The public fields of the
/// settings class become controls: bool is an Off/On switch, an enum a cycle, int and float a slider
/// when <see cref="SettingAttribute"/> gives bounds (a cycle of usual values otherwise), string a
/// read-only line for now. Edits are written to the config file as they happen.
/// </summary>
public static class ModSettings
{
    private static readonly List<SettingsSection> Registered = new List<SettingsSection>();

    /// <summary>Every registered config, in the order they were added.</summary>
    public static IReadOnlyList<SettingsSection> Sections => Registered;

    /// <summary>
    /// Shows <paramref name="config"/> on the Settings page for as long as <paramref name="owner"/> is
    /// loaded. The title defaults to the mod's name.
    /// </summary>
    public static void Register<T>(ObenMod owner, ConfigFile<T> config, string title = null) where T : class, new()
    {
        var section = new SettingsSection(title ?? owner.Info.Manifest.DisplayName, owner.Info.Id, () => config.Value, config.Save);
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
}
