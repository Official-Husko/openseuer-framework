using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using ObenFramework.Patching;
using ObenLoader.Runtime;
using UnityEngine;
using UnityEngine.Audio;

namespace ObenFramework.Assets;

/// <summary>The game's volume sliders, as its mixer knows them.</summary>
public enum VolumeChannel
{
    Master,

    /// <summary>Sound effects (the Sound slider, which also covers the game's other sounds).</summary>
    Sound,

    Music,

    /// <summary>The game's separate slider for unpleasant sounds.</summary>
    Nasty,
}

/// <summary>
/// A channel turned down by a mod, from <see cref="SoundVolumes.Lower"/>. Change <see cref="Share"/> to fade it;
/// dispose it to give the player's volume back.
/// </summary>
public sealed class VolumeLowering : IDisposable
{
    private float _share;

    internal VolumeLowering(VolumeChannel channel, float share)
    {
        Channel = channel;
        _share = Clamp(share);
    }

    public VolumeChannel Channel { get; }

    /// <summary>The share of the player's volume left, from 0 (silent) to 1 (unchanged).</summary>
    public float Share
    {
        get => _share;
        set
        {
            _share = Clamp(value);
            SoundVolumes.Apply(Channel);
        }
    }

    public void Dispose() => SoundVolumes.Remove(this);

    private static float Clamp(float share) => float.IsNaN(share) ? 1f : Mathf.Clamp01(share);
}

/// <summary>
/// Turns the game's volume channels down for a while, to duck the music under a scene or quiet the world
/// during a call. The player's sliders and their volume file are never written: the options menu shows and
/// saves the player's own levels, and each channel returns to them when the last lowering goes. When several
/// mods lower one channel, the lowest share wins.
/// </summary>
public static class SoundVolumes
{
    private static readonly AccessTools.FieldRef<VolumeSave, AudioMixer> MixerField = AccessTools.FieldRefAccess<VolumeSave, AudioMixer>("audioMixer");
    private static readonly List<VolumeLowering> Live = new List<VolumeLowering>();

    // The options menu reads the mixer back into its sliders and saves them; it must see the player's levels.
    private static int _suspended;

    /// <summary>
    /// False when the framework's volume hooks failed to apply after a game update. Lowerings then have no effect,
    /// so the options menu can't save a lowered level as the player's.
    /// </summary>
    public static bool IsAvailable => PatchAreas.IsAvailable(PatchAreas.Framework, "Assets");

    /// <summary>
    /// Lowers <paramref name="channel"/> to <paramref name="share"/> of the player's volume (0 silent, 1 unchanged)
    /// until the result is disposed or <paramref name="owner"/> unloads.
    /// </summary>
    public static VolumeLowering Lower(ITracker owner, VolumeChannel channel, float share)
    {
        if (owner == null)
            throw new ArgumentNullException(nameof(owner));
        var lowering = new VolumeLowering(channel, share);
        Live.Add(lowering);
        owner.Track(lowering);
        Apply(channel);
        return lowering;
    }

    /// <summary>The lowest share on a channel now, 1 when no mod lowers it.</summary>
    public static float Share(VolumeChannel channel)
    {
        float share = 1f;
        foreach (var lowering in Live)
        {
            if (lowering.Channel == channel)
                share = Mathf.Min(share, lowering.Share);
        }
        return share;
    }

    /// <summary>The level the channel plays at now in decibels (the player's with any lowering), or null before the audio is set up.</summary>
    public static float? Level(VolumeChannel channel)
    {
        var mixer = Mixer();
        return mixer != null && mixer.GetFloat(channel.ToString(), out float level) ? level : (float?)null;
    }

    internal static void Remove(VolumeLowering lowering)
    {
        if (Live.Remove(lowering))
            Apply(lowering.Channel);
    }

    internal static void Reset()
    {
        Live.Clear();
        _suspended = 0;
        foreach (VolumeChannel channel in Enum.GetValues(typeof(VolumeChannel)))
            Apply(channel);
    }

    internal static void Apply(VolumeChannel channel)
    {
        var save = VolumeSave.instance;
        var mixer = Mixer();
        if (save == null || mixer == null)
            return;
        float player = PlayerLevel(save.soundSave, channel);
        float share = _suspended > 0 || !IsAvailable ? 1f : Share(channel);
        // A share in decibels, down to the mixer's floor of -80.
        float level = share >= 1f ? player : Mathf.Max(-80f, player + 20f * Mathf.Log10(Mathf.Max(share, 1e-4f)));
        mixer.SetFloat(channel.ToString(), level);
        if (channel == VolumeChannel.Sound)
            mixer.SetFloat("OtherSounds", level);
    }

    private static void ApplyAll()
    {
        if (Live.Count == 0 || _suspended > 0)
            return;
        foreach (VolumeChannel channel in Enum.GetValues(typeof(VolumeChannel)))
            Apply(channel);
    }

    private static float PlayerLevel(gameVolume volume, VolumeChannel channel) => channel switch
    {
        VolumeChannel.Master => volume.master,
        VolumeChannel.Sound => volume.sound,
        VolumeChannel.Music => volume.music,
        _ => volume.nasty,
    };

    private static AudioMixer Mixer()
    {
        var save = VolumeSave.instance;
        return save != null ? MixerField(save) : null;
    }

    // The game sets the mixer from the player's levels when it reads the volume file and when a slider moves.
    [HarmonyPatch]
    private static class VolumePatch
    {
        [HarmonyTargetMethods]
        private static IEnumerable<MethodBase> Targets()
        {
            yield return AccessTools.Method(typeof(VolumeSave), nameof(VolumeSave.GetVolume));
            yield return AccessTools.Method(typeof(VolumeSave), nameof(VolumeSave.SaveVolume));
        }

        [HarmonyPostfix]
        private static void After()
        {
            try { ApplyAll(); }
            catch (Exception e) { AssetsLog.Error("Volume hook failed", e); }
        }
    }

    [HarmonyPatch(typeof(OptionsMenu), nameof(OptionsMenu.StartInit))]
    private static class OptionsPatch
    {
        [HarmonyPrefix]
        private static void Before()
        {
            if (_suspended++ > 0 || Live.Count == 0)
                return;
            try
            {
                foreach (VolumeChannel channel in Enum.GetValues(typeof(VolumeChannel)))
                    Apply(channel);
            }
            catch (Exception e) { AssetsLog.Error("Options volume hook failed", e); }
        }

        [HarmonyFinalizer]
        private static void After()
        {
            if (_suspended > 0)
                _suspended--;
            try { ApplyAll(); }
            catch (Exception e) { AssetsLog.Error("Options volume hook failed", e); }
        }
    }
}
