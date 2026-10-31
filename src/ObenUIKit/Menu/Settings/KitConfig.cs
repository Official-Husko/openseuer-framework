namespace ObenUIKit.Menu.Settings;

/// <summary>The kit's own settings, in its config.json.</summary>
internal sealed class KitConfig
{
    [Setting("Show What's New after updates", Description = "Open the mod menu on What's New once after a new version is installed.")]
    public bool ShowWhatsNewOnUpdate = true;

    [Setting("Show mod hints", Description = "Hints from mods in the side panel. Off drops them, except the few a mod says must show.")]
    public bool ShowModHints = true;

    [Setting("Show mod key prompts", Description = "Key prompts from mods in the middle of the screen. Off keeps them out of sight (their keys do nothing then).")]
    public bool ShowModPrompts = true;

    [Setting("Show mod difficulty presets", Description = "Difficulty presets from mods in the New Game screen's selector, after the game's own levels.")]
    public bool ShowModDifficultyPresets = true;

    /// <summary>Newest changelog version the player has been shown.</summary>
    [Setting(Hidden = true)]
    public string LastSeenVersion = "";

    [SettingAction("Show What's New at the next start", Description = "The release notes open by themselves the next time the main menu comes up.")]
    public string ForgetSeenVersion()
    {
        LastSeenVersion = "";
        return "What's New opens at the next start.";
    }
}
