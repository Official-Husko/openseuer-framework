// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
using System;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace ObenFramework.Npcs;

/// <summary>
/// One clip playing over an Animator through a playable graph of its own (see <see cref="NpcAnimation"/>).
/// The graph's output is blended in over the Animator's own state machine, so when it fades out the NPC
/// goes back to exactly what its controller was doing. Dispose to stop at once, or call <see cref="Stop"/>
/// to fade out.
/// </summary>
public sealed class AnimationPlayback : IDisposable
{
    private PlayableGraph _graph;
    private AnimationPlayableOutput _output;
    private AnimationClipPlayable _clip;
    private AnimationClipPlayable _oldClip;
    private AnimationMixerPlayable _mixer;
    private bool _crossfading;
    private float _mix;
    private float _crossSeconds;
    private float _fadeSeconds;
    private float _blend;

    internal AnimationPlayback(string owner, Animator animator, AnimationClip clip, bool loop, float fadeSeconds)
    {
        Owner = owner;
        Animator = animator;
        try
        {
            _graph = PlayableGraph.Create("ObenFramework.NpcAnimation");
            _graph.SetTimeUpdateMode(DirectorUpdateMode.GameTime);
            _output = AnimationPlayableOutput.Create(_graph, "ObenFramework.NpcAnimation", animator);
            _clip = CreateClip(clip, loop);
            _output.SetSourcePlayable(_clip);
            SetClip(clip, loop, fadeSeconds);
            _blend = _fadeSeconds <= 0f ? 1f : 0f;
            _output.SetWeight(_blend);
            _graph.Play();
        }
        catch
        {
            DestroyGraph();
            throw;
        }
    }

    /// <summary>Id of the mod that started it.</summary>
    public string Owner { get; }

    /// <summary>The Animator it plays on.</summary>
    public Animator Animator { get; }

    /// <summary>The clip playing (the newest one during a crossfade).</summary>
    public AnimationClip Clip { get; private set; }

    /// <summary>Whether the clip repeats until stopped. A clip that does not loop fades out by itself at its end.</summary>
    public bool Loop { get; private set; }

    /// <summary>The playback is fading out or over.</summary>
    public bool Ending { get; private set; }

    /// <summary>The playback has ended and its graph is gone.</summary>
    public bool Finished { get; private set; }

    /// <summary>Fades out over <paramref name="fadeSeconds"/> (0 stops at once).</summary>
    public void Stop(float fadeSeconds = 0.2f)
    {
        if (Finished)
            return;
        if (fadeSeconds <= 0f)
        {
            NpcAnimation.Remove(this);
            return;
        }
        _fadeSeconds = fadeSeconds;
        Ending = true;
    }

    /// <summary>Stops at once.</summary>
    public void Dispose() => Stop(0f);

    internal bool Alive => !Finished && Animator != null && Animator.gameObject.activeInHierarchy && _graph.IsValid();

    // Switches to another clip, crossfading when there is time to.
    internal void Replace(AnimationClip clip, bool loop, float fadeSeconds)
    {
        if (_crossfading)
            FinishCrossfade();
        var next = CreateClip(clip, loop);
        SetClip(clip, loop, fadeSeconds);
        Ending = false;
        if (_fadeSeconds > 0f && TryCrossfade(next))
            return;
        _output.SetSourcePlayable(next);
        _clip.Destroy();
        _clip = next;
        _blend = 1f;
        _output.SetWeight(_blend);
    }

    // One frame of fading and clip bookkeeping. False when the playback is over.
    internal bool Advance(float deltaTime)
    {
        if (!Alive)
            return false;
        float length = Clip.length;
        if (!Loop && _clip.GetTime() + deltaTime >= length)
        {
            _clip.SetTime(length);
            _clip.Pause();
        }
        if (!Ending)
            CheckEnd(length);
        if (_crossfading)
        {
            _mix = Approach(_mix, 1f, _crossSeconds, deltaTime);
            if (_mix >= 1f)
            {
                FinishCrossfade();
            }
            else
            {
                _mixer.SetInputWeight(0, 1f - _mix);
                _mixer.SetInputWeight(1, _mix);
            }
        }
        _blend = Approach(_blend, Ending ? 0f : 1f, _fadeSeconds, deltaTime);
        _output.SetWeight(_blend);
        return !(Ending && _blend <= 0f);
    }

    internal void DestroyGraph()
    {
        Finished = true;
        Ending = true;
        try
        {
            if (_graph.IsValid())
                _graph.Destroy();
        }
        catch (Exception e)
        {
            NpcsLog.Failed("Destroying an animation graph", e);
        }
    }

    private void SetClip(AnimationClip clip, bool loop, float fadeSeconds)
    {
        Clip = clip;
        Loop = loop;
        _fadeSeconds = loop ? fadeSeconds : Mathf.Min(fadeSeconds, clip.length * 0.5f);
    }

    private AnimationClipPlayable CreateClip(AnimationClip clip, bool loop)
    {
        var playable = AnimationClipPlayable.Create(_graph, clip);
        if (!loop)
            playable.SetDuration(clip.length);
        return playable;
    }

    private bool TryCrossfade(AnimationClipPlayable next)
    {
        _oldClip = _clip;
        _mixer = AnimationMixerPlayable.Create(_graph, 2);
        _output.SetSourcePlayable(_mixer);
        if (!_graph.Connect(_oldClip, 0, _mixer, 0) || !_graph.Connect(next, 0, _mixer, 1))
        {
            _mixer.Destroy();
            _output.SetSourcePlayable(_oldClip);
            next.Destroy();
            return false;
        }
        _mixer.SetInputWeight(0, 1f);
        _mixer.SetInputWeight(1, 0f);
        _clip = next;
        _crossfading = true;
        _mix = 0f;
        _crossSeconds = _fadeSeconds;
        return true;
    }

    private void FinishCrossfade()
    {
        _graph.Disconnect(_mixer, 0);
        _graph.Disconnect(_mixer, 1);
        _output.SetSourcePlayable(_clip);
        _oldClip.Destroy();
        _mixer.Destroy();
        _crossfading = false;
        _mix = 1f;
    }

    // A looping clip that is not marked as looping is wrapped by hand; a one-shot starts its fade-out early
    // enough to end with the clip.
    private void CheckEnd(float length)
    {
        if (Loop)
        {
            if (Clip.isLooping || length <= 0f)
                return;
            double time = _clip.GetTime();
            if (time >= length)
                _clip.SetTime(time % length);
            return;
        }
        double end = Math.Max(0d, length - _fadeSeconds);
        if (_clip.GetTime() >= end)
            Ending = true;
    }

    private static float Approach(float value, float target, float seconds, float deltaTime) =>
        seconds <= 0f ? target : Mathf.MoveTowards(value, target, deltaTime / seconds);
}
