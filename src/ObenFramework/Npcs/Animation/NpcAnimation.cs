// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
using System;
using System.Collections.Generic;
using ObenFramework.Events;
using ObenLoader;
using ObenLoader.Runtime;
using UnityEngine;

namespace ObenFramework.Npcs;

/// <summary>
/// Plays an <see cref="AnimationClip"/> on a character over what its Animator does (a wave, a dance, a
/// pose), blended in and out so the character returns to its own animation afterwards. Works on any
/// Animator: a puppet, a mod NPC's body, or a game NPC through its claim (<see cref="NpcClaim.PlayAnimation"/>).
/// One playback per Animator; a second mod's playback is refused until the first ends. Everything a mod
/// started stops when it unloads, and all of it stops when the level changes.
/// </summary>
public static class NpcAnimation
{
    private static readonly List<AnimationPlayback> Playing = new List<AnimationPlayback>();
    private static readonly HashSet<string> Tracked = new HashSet<string>();

    /// <summary>
    /// Plays <paramref name="clip"/> on <paramref name="animator"/>, fading in over <paramref name="fadeSeconds"/>.
    /// A clip that does not loop fades out by itself when it ends. If the same mod already plays something on
    /// that Animator, the new clip crossfades over it and the same playback is returned. Null when another
    /// mod plays on it, the Animator is inactive, or a save or load runs.
    /// </summary>
    public static AnimationPlayback Play(ObenMod owner, Animator animator, AnimationClip clip, bool loop = true, float fadeSeconds = 0.2f)
    {
        if (owner == null)
            throw new ArgumentNullException(nameof(owner));
        var playback = Start(owner.Info.Id, animator, clip, loop, fadeSeconds);
        if (playback != null && Tracked.Add(owner.Info.Id))
        {
            string id = owner.Info.Id;
            owner.Track(Disposable.Create(() =>
            {
                Tracked.Remove(id);
                StopAll(id);
            }));
        }
        return playback;
    }

    /// <summary>The playback running on this Animator, or null.</summary>
    public static AnimationPlayback On(Animator animator)
    {
        if (animator == null)
            return null;
        for (int i = Playing.Count - 1; i >= 0; i--)
        {
            if (Playing[i].Animator == animator && Playing[i].Alive)
                return Playing[i];
        }
        return null;
    }

    /// <summary>Stops everything the mod plays at once. Returns how many there were.</summary>
    public static int StopAll(ObenMod owner) => StopAll(owner.Info.Id);

    internal static AnimationPlayback Start(string owner, Animator animator, AnimationClip clip, bool loop, float fadeSeconds)
    {
        if (clip == null)
            throw new ArgumentNullException(nameof(clip));
        if (clip.legacy)
            throw new ArgumentException("Legacy animation clips cannot be played on an Animator.", nameof(clip));
        if (float.IsNaN(fadeSeconds) || float.IsInfinity(fadeSeconds) || fadeSeconds < 0f)
            throw new ArgumentException("The fade must be a finite number of seconds, 0 or more.", nameof(fadeSeconds));
        if (animator == null || !animator.gameObject.activeInHierarchy || Saves.SaveGate.IsBusy)
            return null;

        // A game NPC another mod has claimed is that mod's to animate.
        var npc = animator.GetComponentInParent<NPCInfo>();
        string claimant = npc != null && npc.character != null ? NpcControl.ClaimedBy(npc.character.ID) : null;
        if (claimant != null && claimant != owner)
            return null;

        var current = On(animator);
        if (current != null && current.Owner != owner)
        {
            if (!current.Ending)
                return null;
            Remove(current);
            current = null;
        }
        if (current != null)
        {
            try
            {
                current.Replace(clip, loop, fadeSeconds);
                return current;
            }
            catch (Exception e)
            {
                NpcsLog.Failed("Switching an animation", e);
                Remove(current);
                return null;
            }
        }

        try
        {
            var playback = new AnimationPlayback(owner, animator, clip, loop, fadeSeconds);
            Playing.Add(playback);
            return playback;
        }
        catch (Exception e)
        {
            NpcsLog.Failed("Starting an animation", e);
            return null;
        }
    }

    internal static void Init(ObenMod framework)
    {
        GameEvents.SceneLoaded.Subscribe(framework, _ => Clear());
        framework.Track(Disposable.Create(() =>
        {
            Clear();
            Tracked.Clear();
        }));
    }

    /// <summary>Called every frame by the framework; does nothing while nothing plays.</summary>
    internal static void Tick()
    {
        if (Playing.Count == 0)
            return;
        float deltaTime = Time.deltaTime;
        for (int i = Playing.Count - 1; i >= 0; i--)
        {
            if (i >= Playing.Count)
                continue;
            var playback = Playing[i];
            bool keep;
            try { keep = playback.Advance(deltaTime); }
            catch (Exception e)
            {
                NpcsLog.Failed("Playing an animation", e);
                keep = false;
            }
            if (!keep)
                Remove(playback);
        }
    }

    internal static void Remove(AnimationPlayback playback)
    {
        Playing.Remove(playback);
        playback.DestroyGraph();
    }

    private static int StopAll(string owner)
    {
        int count = 0;
        for (int i = Playing.Count - 1; i >= 0; i--)
        {
            if (Playing[i].Owner != owner)
                continue;
            Remove(Playing[i]);
            count++;
        }
        return count;
    }

    private static void Clear()
    {
        var all = Playing.ToArray();
        Playing.Clear();
        foreach (var playback in all)
            playback.DestroyGraph();
    }
}
