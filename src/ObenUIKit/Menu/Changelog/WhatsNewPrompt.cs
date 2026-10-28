using System.Collections;
using ObenFramework.Events;
using ObenFramework.Game;
using ObenLoader;
using ObenLoader.Config;
using ObenUIKit.Menu.Pages;
using ObenUIKit.Menu.Settings;
using ObenUIKit.Theme;

namespace ObenUIKit.Menu.Changelog;

/// <summary>
/// Opens the mod menu on What's New once after a newer version was installed. The newest published
/// version in changelog.json is remembered in the kit's config; the menu opens at the main menu,
/// after the kit has taken the game's look, so it doesn't appear in the fallback style.
/// </summary>
internal static class WhatsNewPrompt
{
    private const int ThemeWaitFrames = 120;

    public static void Init(ObenMod kit, ConfigFile<KitConfig> config)
    {
        GameEvents.MainMenu.Subscribe(kit, () => kit.StartCoroutine(Check(config)));
        if (WorldState.InMainMenu)
            kit.StartCoroutine(Check(config));
    }

    private static IEnumerator Check(ConfigFile<KitConfig> config)
    {
        var newest = ChangelogFeed.Load()?.NewestReleased;
        var settings = config.Value;
        if (newest == null || newest.Number == null || newest.Version == settings.LastSeenVersion)
            yield break;

        var seen = System.Version.TryParse(settings.LastSeenVersion, out var v) ? v : null;
        if (seen != null && newest.Number <= seen)
            yield break;

        for (int i = 0; i < ThemeWaitFrames && !ThemeReady(); i++)
            yield return null;
        if (!ThemeReady() || !WorldState.InMainMenu || ModMenu.IsOpen)
            yield break;

        settings.LastSeenVersion = newest.Version;
        config.Save();
        if (settings.ShowWhatsNewOnUpdate)
            ModMenu.Open(WhatsNewPage.Title);
    }

    private static bool ThemeReady() => UiKit.Theme.Level == ThemeLevel.MainMenu;
}
