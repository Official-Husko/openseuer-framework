// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using ObenLoader;
using UnityEngine;

namespace ObenFramework.Saves;

/// <summary>
/// The game's characters and save slots, the folder of the save that is loaded, and a way to
/// write a save through the game's own save path. Listing only reads each slot's small
/// <c>Info.tnmt</c> header; nothing here deletes or edits save files (saving into a slot replaces
/// that slot, as saving does in the game).
/// </summary>
public static partial class SaveSlots
{
    /// <summary>Every slot folder the game can write, in the order the load menu lists them.</summary>
    public static readonly IReadOnlyList<string> SlotNames = new[]
    {
        "Autosave", "Autosave2", "Slot1", "Slot2", "Slot3", "Slot4", "Slot5", "Slot6", "Slot7", "Slot8", "Slot9", "Slot10",
    };

    /// <summary>The game's <c>Saves</c> folder (it does not exist until the first save is written).</summary>
    public static string SavesFolder => Application.persistentDataPath + "/Saves";

    /// <summary>
    /// Folder of the save that is loaded, or null at the main menu, in a new game before the first
    /// save, or when it does not belong to the current character. Same value mod data is stored under.
    /// </summary>
    public static string CurrentFolder
    {
        get
        {
            var folder = SaveFolders.LoadedFolder();
            if (folder == null || PlayerIdentity.identity == null)
                return null;

            var trimmed = folder.TrimEnd('/', '\\');
            var owner = Path.GetFileName(Path.GetDirectoryName(trimmed));
            var expected = SaveController.SanitizeCharacterName(PlayerIdentity.identity.firstName, PlayerIdentity.identity.lastName);
            return string.Equals(owner, expected, StringComparison.OrdinalIgnoreCase) ? folder : null;
        }
    }

    /// <summary>Slot name of the loaded save (for example <c>Slot3</c>), or null.</summary>
    public static string CurrentSlot
    {
        get
        {
            var folder = CurrentFolder;
            if (folder == null)
                return null;
            var name = Path.GetFileName(folder.TrimEnd('/', '\\'));
            return string.IsNullOrEmpty(name) ? null : name;
        }
    }

    /// <summary>All characters with at least one readable save. Empty when there are none yet.</summary>
    public static List<SaveCharacterInfo> Characters()
    {
        var characters = new List<SaveCharacterInfo>();
        if (!Directory.Exists(SavesFolder))
            return characters;
        try
        {
            foreach (var saves in SaveController.GetCharacters())
            {
                characters.Add(new SaveCharacterInfo
                {
                    Name = saves.CharacterDisplayName(),
                    Folder = saves.FolderName,
                    FolderName = Path.GetFileName(saves.FolderName.TrimEnd('/', '\\')),
                    LatestDate = saves.LatestDate,
                    HasAutosave = saves.Autosave,
                });
            }
        }
        catch (Exception e)
        {
            SaveState.Log.Warn("Listing the characters failed: " + e.Message);
        }
        return characters;
    }

    /// <summary>
    /// The slots that exist for <paramref name="character"/>, or for every character when it is
    /// null. Includes <c>Autosave2</c>, which the game's own character list ignores.
    /// </summary>
    public static List<SaveSlotInfo> Slots(SaveCharacterInfo character = null)
    {
        var slots = new List<SaveSlotInfo>();
        foreach (var owner in character != null ? new List<SaveCharacterInfo> { character } : Characters())
        {
            foreach (var slot in SlotNames)
            {
                var folder = owner.Folder + "/" + slot;
                if (File.Exists(folder + "/Info.tnmt"))
                    slots.Add(ReadHeader(owner, slot, folder));
            }
        }
        return slots;
    }

    private static SaveSlotInfo ReadHeader(SaveCharacterInfo owner, string slot, string folder)
    {
        var info = new SaveSlotInfo { Slot = slot, Folder = folder, Character = owner.Name };
        try
        {
            var header = SaveController.GetSavedGameInfo(folder, out var exception);
            if (exception != null || header == null || header.SaveName == null)
            {
                info.Corrupted = true;
                return info;
            }
            info.Level = header.LevelName;
            info.GameVersion = header.ApplicationVersion;
            if (DateTime.TryParse(header.Date, CultureInfo.InvariantCulture, DateTimeStyles.None, out var saved))
                info.Saved = saved;
        }
        catch (Exception)
        {
            info.Corrupted = true;
        }
        return info;
    }
}
