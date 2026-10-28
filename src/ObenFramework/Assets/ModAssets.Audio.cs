using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using ObenLoader;
using ObenLoader.Runtime;
using UnityEngine;

namespace ObenFramework.Assets;

public static partial class ModAssets
{
    /// <summary>
    /// Loads a WAV file (PCM 8, 16, 24 or 32 bit, or float) on the calling thread and returns the clip.
    /// Cached per file. For OGG, MP3 or long recordings use <see cref="SoundAsync"/>, which also works for
    /// WAV and doesn't block the frame.
    /// </summary>
    public static AudioClip Sound(ObenMod owner, string path)
    {
        string full = ResolveExisting(owner, path);
        var set = AssetSets.For(owner);
        if (set.Sounds.TryGetValue(full, out var cached) && cached != null)
            return cached;

        if (!string.Equals(Path.GetExtension(full), ".wav", StringComparison.OrdinalIgnoreCase))
            throw new NotSupportedException($"{full}: Sound only reads WAV files, use SoundAsync for other formats.");
        if (new FileInfo(full).Length > WavParser.MaxBytes)
            throw new InvalidDataException($"{full} is larger than {WavParser.MaxBytes >> 20} MB, use SoundAsync.");
        if (!WavParser.TryParse(File.ReadAllBytes(full), out var wav, out string error))
            throw new InvalidDataException($"{full}: {error}");
        return Store(set, full, MakeClip(Path.GetFileNameWithoutExtension(full), wav));
    }

    /// <summary>
    /// Loads WAV, OGG or MP3 without stalling the frame and calls <paramref name="done"/> on the main
    /// thread with the clip (or <paramref name="failed"/> with the reason). WAV files are decoded on a
    /// worker thread; the other formats go through Unity's own loader. Cached per file: asking again
    /// calls <paramref name="done"/> at once. Nothing is called if the mod unloads first.
    /// </summary>
    public static void SoundAsync(ObenMod owner, string path, Action<AudioClip> done, Action<string> failed = null)
    {
        string full;
        try
        {
            full = ResolveExisting(owner, path);
        }
        catch (Exception e)
        {
            Fail(failed, e.Message);
            return;
        }

        var set = AssetSets.For(owner);
        if (set.Sounds.TryGetValue(full, out var cached) && cached != null)
        {
            Deliver(set, new SoundRequest(done, failed), cached, null);
            return;
        }

        var request = new SoundRequest(done, failed);
        if (set.SoundWaiting.TryGetValue(full, out var waiting))
        {
            waiting.Add(request);
            return;
        }
        set.SoundWaiting[full] = new List<SoundRequest> { request };

        if (UsesBuiltInDecoder(full))
            Task.Run(() => DecodeWav(full)).ContinueWith(task => MainThread.Post(() => FinishWav(set, full, task)));
        else
            owner.StartCoroutine(SoundDecoder.Load(set, full, FinishClip));
    }

    /// <summary>
    /// Makes a clip from samples in the range -1 to 1 (interleaved when there is more than one
    /// channel), for tones and other generated sound. The clip is destroyed with the mod.
    /// </summary>
    public static AudioClip Clip(ObenMod owner, string name, float[] samples, int channels, int sampleRate)
    {
        if (samples == null)
            throw new ArgumentNullException(nameof(samples));
        if (channels < 1 || channels > 8)
            throw new ArgumentException("channels must be from 1 to 8", nameof(channels));
        if (sampleRate < 1000 || sampleRate > 384000)
            throw new ArgumentException("sampleRate must be from 1000 to 384000", nameof(sampleRate));
        if (samples.Length == 0 || samples.Length % channels != 0)
            throw new ArgumentException("samples must hold whole frames and not be empty", nameof(samples));

        var wav = new WavData { Channels = channels, SampleRate = sampleRate, Samples = samples };
        return AssetSets.For(owner).Adopt(MakeClip(name, wav));
    }

    private static bool UsesBuiltInDecoder(string full) =>
        string.Equals(Path.GetExtension(full), ".wav", StringComparison.OrdinalIgnoreCase) && new FileInfo(full).Length <= WavParser.MaxBytes;

    private sealed class WavResult
    {
        public WavData Wav;
        public string Error;
    }

    private static WavResult DecodeWav(string full)
    {
        var result = new WavResult();
        try
        {
            if (!WavParser.TryParse(File.ReadAllBytes(full), out result.Wav, out result.Error))
                result.Wav = null;
        }
        catch (Exception e)
        {
            result.Error = e.Message;
        }
        return result;
    }

    private static void FinishWav(ModAssetSet set, string full, Task<WavResult> task)
    {
        if (set.Released)
            return;
        if (task.IsFaulted || task.Result.Wav == null)
        {
            FinishClip(set, full, null, task.IsFaulted ? task.Exception.GetBaseException().Message : task.Result.Error);
            return;
        }
        try
        {
            FinishClip(set, full, MakeClip(Path.GetFileNameWithoutExtension(full), task.Result.Wav), null);
        }
        catch (Exception e)
        {
            FinishClip(set, full, null, e.Message);
        }
    }

    // Called on the main thread by either decoder, with a clip or the reason there is none.
    private static void FinishClip(ModAssetSet set, string full, AudioClip clip, string error)
    {
        if (set.Released)
        {
            if (clip != null)
                UnityEngine.Object.Destroy(clip);
            return;
        }
        if (!set.SoundWaiting.TryGetValue(full, out var waiting))
            return;
        set.SoundWaiting.Remove(full);
        if (clip != null)
        {
            // A synchronous Sound call may have stored the same file while this one was loading.
            if (set.Sounds.TryGetValue(full, out var existing) && existing != null)
            {
                UnityEngine.Object.Destroy(clip);
                clip = existing;
            }
            else
            {
                Store(set, full, clip);
            }
        }
        foreach (var request in waiting)
            Deliver(set, request, clip, error ?? "the file could not be decoded");
    }

    private static void Deliver(ModAssetSet set, SoundRequest request, AudioClip clip, string error)
    {
        try
        {
            if (clip != null)
                request.Done?.Invoke(clip);
            else
                Fail(request.Failed, error, set.ModId);
        }
        catch (Exception e)
        {
            AssetsLog.Error($"{set.ModId}: a sound callback threw", e);
        }
    }

    private static void Fail(Action<string> failed, string message, string modId = null)
    {
        if (failed != null)
            failed(message);
        else
            AssetsLog.Warn((modId == null ? "" : modId + ": ") + "sound not loaded: " + message);
    }

    private static AudioClip Store(ModAssetSet set, string full, AudioClip clip)
    {
        set.Sounds[full] = clip;
        return clip;
    }

    private static AudioClip MakeClip(string name, WavData wav)
    {
        var clip = AudioClip.Create(name, wav.Frames, wav.Channels, wav.SampleRate, false);
        try
        {
            EngineCalls.SetData(clip, wav.Samples, 0);
        }
        catch (Exception)
        {
            UnityEngine.Object.Destroy(clip);
            throw;
        }
        return clip;
    }
}
