// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace ObenFramework.Assets;

/// <summary>
/// The game's own sounds: the pickup sounds items make (by <c>PickupSoundName</c>), the shared sound
/// library (foley, harvest, arrest, prank ...; each plays a random clip of its set), and the level's ambient
/// soundscapes. For a mod's own clips see <see cref="ModSounds"/>. Main thread, in a level.
/// </summary>
public static class GameSounds
{
    private static readonly string[] Pickups = Enum.GetNames(typeof(PickupSoundName));
    private static readonly FieldInfo[] LibraryFields = Array.FindAll(
        typeof(RandomAudioLibrary).GetFields(BindingFlags.Public | BindingFlags.Instance), f => f.FieldType == typeof(RandomAudio));

    /// <summary>The names <see cref="PlayPickup"/> takes ("Default", "Batterypack", "Bottle" ...).</summary>
    public static List<string> PickupNames() => new List<string>(Pickups);

    /// <summary>Plays an item pickup sound. False before the game's item sounds exist; an unknown name throws.</summary>
    public static bool PlayPickup(string name)
    {
        if (Array.IndexOf(Pickups, name) < 0)
            throw new ArgumentException($"'{name}' isn't one of the game's pickup sounds; see PickupNames().", nameof(name));
        var sounds = ItemSounds.instance;
        if (sounds == null)
            return false;
        sounds.PlaySound(name);
        return true;
    }

    /// <summary>The sound sets of the game's library ("foley", "harvest", "arrestSounds" ...).</summary>
    public static List<string> LibraryNames()
    {
        var names = new List<string>(LibraryFields.Length);
        foreach (var field in LibraryFields)
            names.Add(field.Name);
        return names;
    }

    /// <summary>Plays a random clip of a library set, heard everywhere. False when the library or player isn't there.</summary>
    public static bool PlayLibrary(string name)
    {
        var set = Library(name);
        if (set == null || MiscAudioPlayer.instance == null)
            return false;
        set.PlayRandomClip((AudioSource)null);
        return true;
    }

    /// <summary>Plays a random clip of a library set at <paramref name="position"/> in the world.</summary>
    public static bool PlayLibraryAt(string name, Vector3 position)
    {
        var set = Library(name);
        if (set == null || MiscAudioPlayer.instance == null)
            return false;
        // The game copies the position into a sound object of its own, so the marker can go at once.
        var marker = new GameObject("ObenFramework sound marker");
        try
        {
            marker.transform.position = position;
            set.PlayRandomClip(marker);
        }
        finally
        {
            UnityEngine.Object.Destroy(marker);
        }
        return true;
    }

    /// <summary>The id of the ambient soundscape playing now, or null.</summary>
    public static string Soundscape
    {
        get
        {
            var controller = SoundscapeController.instance;
            var area = controller != null ? controller.currentSoundscape : null;
            return area != null ? area.id : null;
        }
    }

    /// <summary>The soundscape ids the level knows.</summary>
    public static List<string> SoundscapeIds()
    {
        var ids = new List<string>();
        var controller = SoundscapeController.instance;
        if (controller == null || controller.soundscapeAreas == null)
            return ids;
        foreach (var area in controller.soundscapeAreas)
        {
            if (area != null && !string.IsNullOrEmpty(area.id))
                ids.Add(area.id);
        }
        return ids;
    }

    /// <summary>
    /// Switches the ambience to a soundscape of the level, at once. The game switches again when the player
    /// walks into one of its soundscape zones. False when the level has no such soundscape.
    /// </summary>
    public static bool SetSoundscape(string id)
    {
        var controller = SoundscapeController.instance;
        var area = controller != null ? controller.FindSoundscapeAreaById(id) : null;
        if (area == null)
            return false;
        controller.PlaySoundscape(area, playImmediately: true);
        return true;
    }

    /// <summary>Goes back to the level's general soundscape.</summary>
    public static bool ResetSoundscape()
    {
        var controller = SoundscapeController.instance;
        if (controller == null)
            return false;
        controller.PlaySoundscape(null, playImmediately: true);
        return true;
    }

    private static RandomAudio Library(string name)
    {
        var field = Array.Find(LibraryFields, f => f.Name == name)
            ?? throw new ArgumentException($"'{name}' isn't a set of the game's sound library; see LibraryNames().", nameof(name));
        var library = RandomAudioLibrary.instance;
        return library != null ? field.GetValue(library) as RandomAudio : null;
    }
}
