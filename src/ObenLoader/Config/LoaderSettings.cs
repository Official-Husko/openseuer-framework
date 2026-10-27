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
}
