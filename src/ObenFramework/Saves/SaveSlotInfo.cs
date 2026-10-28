using System;

namespace ObenFramework.Saves;

/// <summary>One character's save folder under the game's <c>Saves</c> directory.</summary>
public sealed class SaveCharacterInfo
{
    /// <summary>Name as the load menu shows it (underscores turned into spaces, duplicates numbered).</summary>
    public string Name { get; internal set; }

    /// <summary>Full path of the character's folder.</summary>
    public string Folder { get; internal set; }

    /// <summary>Folder name only, e.g. <c>Esko_Virtanen</c>.</summary>
    public string FolderName { get; internal set; }

    /// <summary>The game's own "latest date" text for the character, if it set one.</summary>
    public string LatestDate { get; internal set; }

    /// <summary>The <c>Autosave</c> slot exists.</summary>
    public bool HasAutosave { get; internal set; }
}

/// <summary>The header of one save slot (<c>Info.tnmt</c>), read without loading it.</summary>
public sealed class SaveSlotInfo
{
    /// <summary>Folder name of the slot: <c>Autosave</c>, <c>Autosave2</c> or <c>Slot1</c> to <c>Slot10</c>.</summary>
    public string Slot { get; internal set; }

    /// <summary>Full path of the slot folder.</summary>
    public string Folder { get; internal set; }

    /// <summary>Character display name from the slot's header.</summary>
    public string Character { get; internal set; }

    /// <summary>Scene the player was in (or was headed to) when the slot was saved.</summary>
    public string Level { get; internal set; }

    /// <summary>When the slot was saved, or null if the header had no readable date.</summary>
    public DateTime? Saved { get; internal set; }

    /// <summary>Game version the slot was written by.</summary>
    public string GameVersion { get; internal set; }

    /// <summary>The header could not be read; the game shows such a slot as corrupted.</summary>
    public bool Corrupted { get; internal set; }
}
