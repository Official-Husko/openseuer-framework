using ObenUIKit.Theme;
using UnityEngine;

namespace ObenUIKit.Runtime;

/// <summary>
/// Plays the game's own hover and click sounds for kit controls on a 2D source of the kit's own,
/// through the game's mixer group. It ignores AudioListener.pause, which the pause menu sets.
/// </summary>
internal sealed class UiSounds
{
    private readonly AudioSource _source;
    private readonly ThemeCapture _themes;

    public UiSounds(GameObject root, ThemeCapture themes)
    {
        _themes = themes;
        _source = root.AddComponent<AudioSource>();
        _source.playOnAwake = false;
        _source.spatialBlend = 0f;
        _source.ignoreListenerPause = true;
    }

    public void Hover() => Play(_themes.Current.HoverClip, _themes.Current.HoverVolume);

    public void Press() => Play(_themes.Current.PressClip, _themes.Current.PressVolume);

    private void Play(AudioClip clip, float volume)
    {
        if (clip == null || _source == null)
            return;
        _source.outputAudioMixerGroup = _themes.Current.Mixer;
        _source.PlayOneShot(clip, volume);
    }
}
