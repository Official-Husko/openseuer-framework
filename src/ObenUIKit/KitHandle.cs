using System;
using ObenLoader;
using ObenLoader.Config;
using ObenUIKit.Menu.Settings;
using UnityEngine;

namespace ObenUIKit;

/// <summary>
/// The kit's per-mod services as calls on the mod itself, next to ObenFramework's <c>ModHandle</c>:
/// <c>this.Hotkey(...)</c> like the reference framework's <c>CoreMod.Key</c>, and <c>this.SettingsPage(...)</c>.
/// Each is the same as the static API with the mod as owner.
/// </summary>
public static class KitHandle
{
    /// <summary>A rebindable hotkey shown on the Hotkeys page: <see cref="Hotkeys.Register"/>.</summary>
    public static Hotkey Hotkey(this ObenMod mod, string id, string label, KeyCode defaultKey, Action onPressed,
        KeyModifiers modifiers = KeyModifiers.None, HotkeyScope scope = HotkeyScope.InGame) =>
        Hotkeys.Register(mod, id, label, defaultKey, onPressed, modifiers, scope);

    /// <summary>The mod's config file as a section of the Settings page: <see cref="ModSettings.Register{T}"/>.</summary>
    public static void SettingsPage<T>(this ObenMod mod, ConfigFile<T> config, string title = null) where T : class, new() =>
        ModSettings.Register(mod, config, title);
}
