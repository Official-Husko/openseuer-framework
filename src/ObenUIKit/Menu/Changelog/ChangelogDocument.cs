using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace ObenUIKit.Menu.Changelog;

/// <summary>The changelog.json format written by "oben changelog --write".</summary>
internal sealed class ChangelogDocument
{
    [JsonProperty("releases")] public List<ChangelogRelease> Releases = new List<ChangelogRelease>();

    /// <summary>The newest release that was actually published, not the unreleased commits after it.</summary>
    public ChangelogRelease NewestReleased => Releases.Find(r => r.Released);
}

internal sealed class ChangelogRelease
{
    [JsonProperty("version")] public string Version;
    [JsonProperty("date")] public string Date;
    [JsonProperty("released")] public bool Released;
    [JsonProperty("added")] public List<ChangelogItem> Added = new List<ChangelogItem>();
    [JsonProperty("changed")] public List<ChangelogItem> Changed = new List<ChangelogItem>();
    [JsonProperty("fixed")] public List<ChangelogItem> Fixed = new List<ChangelogItem>();
    [JsonProperty("removed")] public List<ChangelogItem> Removed = new List<ChangelogItem>();

    /// <summary>The version as a number, or null when it isn't one ("Unreleased").</summary>
    public Version Number => System.Version.TryParse(Version, out var v) ? v : null;
}

internal sealed class ChangelogItem
{
    [JsonProperty("text")] public string Text;
    [JsonProperty("area")] public string Area;
}
