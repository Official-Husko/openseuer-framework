using System.Linq;
using TMPro;
using UnityEngine;

namespace ObenUIKit.Theme;

/// <summary>
/// Where the look comes from. Sources are found by component type, so renamed objects don't break
/// them: the main menu's buttons and its options sign, or a level's pause menu.
/// </summary>
internal static class ThemeProbes
{
    public static bool MainMenu(UiTheme theme)
    {
        var helper = SceneObject<MainMenuNavigationHelper>();
        var options = helper != null ? Native.MenuOptions(helper) : null;
        bool button = options != null && ThemeReader.FromTextButton(theme, options.GetComponent<TextButtonColorEffectTMP>());
        ReadOptions(theme, mainMenu: true);
        return button;
    }

    public static bool World(UiTheme theme)
    {
        var pause = PauseMenu.instance;
        if (pause == null || pause.resumeMenuButton == null)
            return false;
        bool button = ThemeReader.FromTextButton(theme, pause.resumeMenuButton.GetComponent<TextButtonColorEffectTMP>());
        ReadOptions(theme, mainMenu: false);
        return button;
    }

    /// <summary>Last resort for the font: the game's typewriter font by name.</summary>
    public static void FontByName(UiTheme theme)
    {
        if (theme.Font != null)
            return;
        var fonts = Resources.FindObjectsOfTypeAll<TMP_FontAsset>();
        theme.Font = fonts.FirstOrDefault(f => f.name == "Erika Ormig SDF")
                     ?? fonts.FirstOrDefault(f => f.name == "Erika Ormig SDF Menu")
                     ?? TMP_Settings.defaultFontAsset;
        theme.FontMaterial = theme.Font != null ? theme.Font.material : null;
    }

    private static void ReadOptions(UiTheme theme, bool mainMenu)
    {
        foreach (var menu in Resources.FindObjectsOfTypeAll<OptionsMenu>())
        {
            if (menu.mainMenu != mainMenu || !menu.gameObject.scene.IsValid())
                continue;
            ThemeReader.FromOptionRow(theme, Native.OptionsLanguage(menu));
            ThemeReader.FromSlider(theme, Native.OptionsFov(menu));
            return;
        }
    }

    // Includes inactive objects, but only ones in a scene (not prefab assets).
    private static T SceneObject<T>() where T : Component
    {
        foreach (var candidate in Resources.FindObjectsOfTypeAll<T>())
        {
            if (candidate.gameObject.scene.IsValid())
                return candidate;
        }
        return null;
    }
}
