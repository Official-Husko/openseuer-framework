using System.Collections.Generic;

namespace ObenLoader.Config;

/// <summary>Stored in &lt;game&gt;/ObenLoader/settings.json.</summary>
public sealed class LoaderSettings
{
    /// <summary>Ids of mods the player switched off. Takes effect on the next launch.</summary>
    public List<string> DisabledMods = new List<string>();

    /// <summary>Ids of mods that are off by default (mod.json "enabledByDefault": false) the player switched on.</summary>
    public List<string> EnabledMods = new List<string>();

    /// <summary>Reload hot-reload mods automatically when their DLLs change.</summary>
    public bool AutoReload = true;

    /// <summary>Write debug lines to latest.log as well.</summary>
    public bool VerboseLog;

    /// <summary>
    /// Refuse every mod whose gameVersion or gameBuilds doesn't match the installed game, as if each had
    /// strictGameVersion: for players who'd rather go without a mod than risk one made for another version.
    /// </summary>
    public bool RefuseOtherGameVersions;

    /// <summary>Show the small loading panel over the game window while it is black at start.</summary>
    public bool LoadingPanel = true;
}
