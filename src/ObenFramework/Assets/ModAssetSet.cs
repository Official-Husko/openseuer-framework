using System;
using System.Collections.Generic;
using ObenLoader;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ObenFramework.Assets;

/// <summary>
/// Everything one mod has loaded: its textures, sprites, sounds and models, keyed by file. Destroyed as a
/// whole when the mod unloads, so nothing leaks across hot reloads.
/// </summary>
internal sealed class ModAssetSet
{
    public readonly ObenMod Owner;
    public readonly Dictionary<string, Texture2D> Textures = new Dictionary<string, Texture2D>();
    public readonly Dictionary<string, Sprite> Sprites = new Dictionary<string, Sprite>();
    public readonly Dictionary<string, AudioClip> Sounds = new Dictionary<string, AudioClip>();
    public readonly Dictionary<string, ModelTemplate> Models = new Dictionary<string, ModelTemplate>();

    /// <summary>Callers waiting on a sound that is still loading, by file.</summary>
    public readonly Dictionary<string, List<SoundRequest>> SoundWaiting = new Dictionary<string, List<SoundRequest>>();

    /// <summary>Callers waiting on a model that is still loading, by key.</summary>
    public readonly Dictionary<string, List<ModelRequest>> ModelWaiting = new Dictionary<string, List<ModelRequest>>();

    private readonly List<Object> _extras = new List<Object>();
    private GameObject _soundHolder;

    public ModAssetSet(ObenMod owner)
    {
        Owner = owner;
    }

    public string ModId => Owner.Info.Id;

    public bool Released { get; private set; }

    /// <summary>Parent for the audio sources this mod plays; created on first use.</summary>
    public GameObject SoundHolder
    {
        get
        {
            if (_soundHolder == null)
                _soundHolder = Owner.CreateObject("sounds");
            return _soundHolder;
        }
    }

    /// <summary>Hands an object to the set so it is destroyed with everything else.</summary>
    public T Adopt<T>(T asset) where T : Object
    {
        if (asset != null)
            _extras.Add(asset);
        return asset;
    }

    public void Release()
    {
        if (Released)
            return;
        Released = true;

        foreach (var model in Models.Values)
            model.Destroy();
        foreach (var sprite in Sprites.Values)
            Kill(sprite);
        foreach (var texture in Textures.Values)
            Kill(texture);
        foreach (var clip in Sounds.Values)
            Kill(clip);
        foreach (var extra in _extras)
            Kill(extra);
        if (_soundHolder != null)
            Object.Destroy(_soundHolder);

        Models.Clear();
        Sprites.Clear();
        Textures.Clear();
        Sounds.Clear();
        _extras.Clear();
        SoundWaiting.Clear();
        ModelWaiting.Clear();
    }

    private static void Kill(Object obj)
    {
        if (obj != null)
            Object.Destroy(obj);
    }
}

/// <summary>A caller of <c>SoundAsync</c> waiting for a clip.</summary>
internal readonly struct SoundRequest
{
    public readonly Action<AudioClip> Done;
    public readonly Action<string> Failed;

    public SoundRequest(Action<AudioClip> done, Action<string> failed)
    {
        Done = done;
        Failed = failed;
    }
}

/// <summary>A caller of <c>ModelRegistry.Load</c> waiting for a template.</summary>
internal readonly struct ModelRequest
{
    public readonly Action<ModelTemplate> Done;
    public readonly Action<string> Failed;

    public ModelRequest(Action<ModelTemplate> done, Action<string> failed)
    {
        Done = done;
        Failed = failed;
    }
}
