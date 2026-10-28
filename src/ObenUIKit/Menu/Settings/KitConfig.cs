namespace ObenUIKit.Menu.Settings;

/// <summary>The kit's own settings, in its config.json.</summary>
internal sealed class KitConfig
{
    [Setting("Show What's New after updates", Description = "Open the mod menu on What's New once after a new version is installed.")]
    public bool ShowWhatsNewOnUpdate = true;

    /// <summary>Newest changelog version the player has been shown.</summary>
    [Setting(Hidden = true)]
    public string LastSeenVersion = "";
}
