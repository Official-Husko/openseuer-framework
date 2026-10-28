using System;
using System.Collections.Generic;
using ObenLoader;
using UnityEngine;
using UnityEngine.Audio;

namespace ObenFramework.Assets;

/// <summary>The game's mixer groups a mod's sound is usually routed through. They follow the player's volume sliders.</summary>
public enum SoundGroup
{
    /// <summary>Sound effects (the game's default group, under the Sound slider).</summary>
    Effects,

    /// <summary>Music, under the Music slider.</summary>
    Music,

    /// <summary>Menu and interface sounds.</summary>
    Menu,

    /// <summary>Ambient sound.</summary>
    Ambience,

    /// <summary>Sounds the player makes.</summary>
    Player,

    /// <summary>Footsteps.</summary>
    Footsteps,
}

/// <summary>
/// Plays clips through the game's own audio mixer, so mod sound obeys the volume sliders, pauses with the
/// game and gets the same muffling and effects as the rest. The sources live under the mod's own object
/// and are destroyed when it unloads.
/// </summary>
public static class ModSounds
{
    private const string MixerPath = "Sounds/MainMixer";
    private static readonly Dictionary<string, AudioMixerGroup> Groups = new Dictionary<string, AudioMixerGroup>();
    private static AudioMixer _mixer;

    /// <summary>Finds one of the game's mixer groups by name, or null when the audio system isn't up yet.</summary>
    public static AudioMixerGroup Group(string name)
    {
        if (string.IsNullOrEmpty(name))
            throw new ArgumentException("name must not be empty", nameof(name));
        if (Groups.TryGetValue(name, out var cached) && cached != null)
            return cached;

        var mixer = Mixer();
        if (mixer == null)
            return null;
        foreach (var group in mixer.FindMatchingGroups(name))
        {
            if (group.name == name)
            {
                Groups[name] = group;
                return group;
            }
        }
        return null;
    }

    public static AudioMixerGroup Group(SoundGroup group) => Group(group.ToString());

    /// <summary>Names of every mixer group the game has, for finding the right one with <see cref="Group(string)"/>.</summary>
    public static List<string> GroupNames()
    {
        var names = new List<string>();
        var mixer = Mixer();
        if (mixer != null)
        {
            foreach (var group in mixer.FindMatchingGroups(""))
                names.Add(group.name);
        }
        return names;
    }

    /// <summary>
    /// Plays a clip without position, as a sound effect by default. Returns the source (null when the
    /// game's audio isn't ready); stop it early with <c>Stop()</c>.
    /// </summary>
    public static AudioSource Play(ObenMod owner, AudioClip clip, SoundGroup group = SoundGroup.Effects, float volume = 1f, float pitch = 1f) =>
        Spawn(owner, clip, Group(group), Vector3.zero, volume, pitch, 1f, 50f, playEverywhere: true, loop: false);

    /// <summary>Plays a clip from a point in the world; it fades with distance up to <paramref name="maxDistance"/>.</summary>
    public static AudioSource PlayAt(ObenMod owner, AudioClip clip, Vector3 position, SoundGroup group = SoundGroup.Effects,
        float volume = 1f, float pitch = 1f, float minDistance = 1f, float maxDistance = 50f) =>
        Spawn(owner, clip, Group(group), position, volume, pitch, minDistance, maxDistance, playEverywhere: false, loop: false);

    /// <summary>
    /// Plays a clip on repeat, for music and ambience. Dispose the result to stop it; it also stops when
    /// the mod unloads.
    /// </summary>
    public static IDisposable Loop(ObenMod owner, AudioClip clip, SoundGroup group = SoundGroup.Music, float volume = 1f)
    {
        var source = Spawn(owner, clip, Group(group), Vector3.zero, volume, 1f, 1f, 50f, playEverywhere: true, loop: true);
        if (source == null)
            return new Noop();
        var go = source.gameObject;
        return new Stopper(go);
    }

    internal static void Reset()
    {
        Groups.Clear();
        _mixer = null;
    }

    private static AudioSource Spawn(ObenMod owner, AudioClip clip, AudioMixerGroup mixer, Vector3 position, float volume, float pitch,
        float minDistance, float maxDistance, bool playEverywhere, bool loop)
    {
        if (clip == null)
            throw new ArgumentNullException(nameof(clip));
        if (mixer == null)
            return null;

        var go = new GameObject("sound:" + clip.name);
        go.transform.SetParent(AssetSets.For(owner).SoundHolder.transform, false);
        go.transform.position = position;
        AudioSource source;
        try
        {
            source = AudioSourceMaker.FromClip(clip, Mathf.Clamp01(volume), pitch, minDistance, maxDistance, go, true, playEverywhere, mixer, loop);
        }
        catch (Exception)
        {
            UnityEngine.Object.Destroy(go);
            throw;
        }
        if (!loop)
            UnityEngine.Object.Destroy(go, clip.length / Mathf.Max(0.01f, Mathf.Abs(pitch)) + 0.25f);
        return source;
    }

    // The game keeps its mixer on MiscAudioPlayer; before that exists it is still a plain resource.
    private static AudioMixer Mixer()
    {
        if (_mixer != null)
            return _mixer;
        var player = MiscAudioPlayer.instance;
        var group = player != null ? player.defaultMixerGroup : null;
        _mixer = group != null ? group.audioMixer : Resources.Load<AudioMixer>(MixerPath);
        return _mixer;
    }

    private sealed class Noop : IDisposable
    {
        public void Dispose() { }
    }

    private sealed class Stopper : IDisposable
    {
        private readonly GameObject _go;

        public Stopper(GameObject go)
        {
            _go = go;
        }

        public void Dispose()
        {
            if (_go != null)
                UnityEngine.Object.Destroy(_go);
        }
    }
}
