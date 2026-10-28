// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
using System;
using System.Collections.Generic;
using HarmonyLib;
using ObenFramework.Items;
using ObenLoader.Runtime;
using UnityEngine;
using WieldableItem;

namespace ObenFramework.World;

/// <summary>An NPC commented on the player's guitar playing.</summary>
public readonly struct GuitarComment
{
    public GuitarComment(Component witness, int npcId, bool positive)
    {
        Witness = witness;
        NpcId = npcId;
        Positive = positive;
    }

    public Component Witness { get; }

    /// <summary>The NPC's character id, or 0 for a bystander without one.</summary>
    public int NpcId { get; }

    /// <summary>They liked it (the game rolls the player's music skill).</summary>
    public bool Positive { get; }
}

/// <summary>
/// The guitar: strum it from code, play a song (a list of waits between strums), hold one of its long notes, and
/// hear strums and the NPCs' comments. The guitar must be in the player's hands. Songs and held notes stop when
/// the guitar is put away, a menu or conversation opens, the level changes or their mod unloads.
/// </summary>
public static class Instruments
{
    private static readonly AccessTools.FieldRef<WieldableGuitar, bool> CooldownField = AccessTools.FieldRefAccess<WieldableGuitar, bool>("cooldownOn");
    private static readonly AccessTools.FieldRef<WieldableGuitar, RandomAudio> StrumAudioField = AccessTools.FieldRefAccess<WieldableGuitar, RandomAudio>("primaryUseAudio");
    private static readonly AccessTools.FieldRef<WieldableGuitar, SoundInfo[]> HoldAudioField = AccessTools.FieldRefAccess<WieldableGuitar, SoundInfo[]>("primaryHoldAudio");
    private static readonly AccessTools.FieldRef<WieldableGuitar, AudioSource> SourceField = AccessTools.FieldRefAccess<WieldableGuitar, AudioSource>("audioSource");

    private sealed class Song
    {
        public WieldableGuitar Guitar;
        public GuitarSong Notes;
        public IDisposable Registration;
    }

    private static readonly List<Song> Songs = new List<Song>();
    internal static WieldableGuitar Holding;

    /// <summary>The player strummed (or a mod did) and the guitar sounded.</summary>
    public static readonly WorldEvent<WieldableGuitar> Strummed = new WorldEvent<WieldableGuitar>(nameof(Strummed));

    /// <summary>An NPC commented on the playing.</summary>
    public static readonly WorldEvent<GuitarComment> Commented = new WorldEvent<GuitarComment>(nameof(Commented));

    /// <summary>The guitar in the player's hands, or null. A scan.</summary>
    public static WieldableGuitar Held
    {
        get
        {
            foreach (var guitar in ObjectIds.Active<WieldableGuitar>())
            {
                if (guitar.equipped)
                    return guitar;
            }
            return null;
        }
    }

    /// <summary>The guitar can sound now: in hand, past its cooldown, and no menu or conversation open.</summary>
    public static bool CanPlay(WieldableGuitar guitar) =>
        guitar != null && guitar.isActiveAndEnabled && guitar.equipped && !CooldownField(guitar) && StrumAudioField(guitar) != null && !InputBlocked();

    /// <summary>How many long notes the guitar has for <see cref="Hold"/>.</summary>
    public static int HoldNotes(WieldableGuitar guitar)
    {
        var notes = guitar != null ? HoldAudioField(guitar) : null;
        return notes != null ? notes.Length : 0;
    }

    /// <summary>One strum, as the player's primary use. False when the guitar can't sound now.</summary>
    public static bool Strum(WieldableGuitar guitar)
    {
        if (!CanPlay(guitar))
            return false;
        using (WorldChanges.EnterMod())
            guitar.onPrimaryUse();
        return true;
    }

    /// <summary>
    /// Plays a song: each entry is the seconds to wait before the next strum (the first one before the first
    /// strum). Replaces a song already playing on that guitar. Lasts until it ends or <paramref name="owner"/> unloads.
    /// </summary>
    public static IDisposable PlaySong(ITracker owner, WieldableGuitar guitar, IList<float> waits)
    {
        if (owner == null)
            throw new ArgumentNullException(nameof(owner));
        if (guitar == null)
            throw new ArgumentNullException(nameof(guitar));
        var song = new Song { Guitar = guitar, Notes = new GuitarSong(waits) };
        StopSong(guitar);
        Songs.Add(song);
        song.Registration = Disposable.Create(() => Songs.Remove(song));
        owner.Track(song.Registration);
        return song.Registration;
    }

    public static void StopSong(WieldableGuitar guitar)
    {
        for (int i = Songs.Count - 1; i >= 0; i--)
        {
            if (Songs[i].Guitar == guitar)
                Songs[i].Registration.Dispose();
        }
    }

    /// <summary>Plays one of the guitar's long notes until <see cref="StopHold"/>, the guitar is put away or a menu opens.</summary>
    public static bool Hold(WieldableGuitar guitar, int note)
    {
        if (!CanPlay(guitar) || note < 0 || note >= HoldNotes(guitar))
            return false;
        var source = SourceField(guitar);
        var clip = HoldAudioField(guitar)[note] != null ? HoldAudioField(guitar)[note].Clip : null;
        if (source == null || clip == null)
            return false;
        source.clip = clip;
        source.Play();
        Holding = guitar;
        return true;
    }

    public static void StopHold()
    {
        var guitar = Holding;
        Holding = null;
        if (guitar != null)
            guitar.onPrimaryStopHold();
    }

    // Songs advance with the frame; nothing runs without one.
    internal static void Update()
    {
        if (Holding != null && (!Holding.equipped || InputBlocked()))
            StopHold();
        if (Songs.Count == 0)
            return;
        for (int i = Songs.Count - 1; i >= 0; i--)
        {
            if (i >= Songs.Count)
                continue;
            var song = Songs[i];
            if (song.Guitar == null || !song.Guitar.equipped)
            {
                song.Registration.Dispose();
                continue;
            }
            int due = song.Notes.Advance(Time.deltaTime);
            for (int n = 0; n < due; n++)
                Strum(song.Guitar);
            if (song.Notes.Done)
                song.Registration.Dispose();
        }
    }

    internal static void Reset()
    {
        foreach (var song in new List<Song>(Songs))
            song.Registration.Dispose();
        Songs.Clear();
        Holding = null;
    }

    private static bool InputBlocked()
    {
        var game = GameController.instance;
        var ui = GameUIController.instance;
        var dialogue = DialogueController.instance;
        return (game != null && game.GameIsPaused) || (ui != null && ui.IsMenuVisible) || (dialogue != null && dialogue.DialogueActive);
    }
}
