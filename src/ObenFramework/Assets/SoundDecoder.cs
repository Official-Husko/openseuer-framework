using System;
using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;

namespace ObenFramework.Assets;

/// <summary>
/// Reads OGG, MP3 and other audio files with Unity's own loader, through a <c>file://</c> request so
/// decoding doesn't block the frame. Run as a coroutine on the owning mod.
/// </summary>
internal static class SoundDecoder
{
    // Short effects decode once up front; longer files stay compressed in memory and decode while playing.
    private const long KeepCompressedAbove = 1024 * 1024;

    public static IEnumerator Load(ModAssetSet set, string full, Action<ModAssetSet, string, AudioClip, string> finish)
    {
        AudioType type = TypeOf(full);
        if (type == AudioType.UNKNOWN)
        {
            finish(set, full, null, "unknown audio format '" + Path.GetExtension(full) + "', use WAV, OGG or MP3");
            yield break;
        }

        using (var request = UnityWebRequestMultimedia.GetAudioClip(new Uri(full).AbsoluteUri, type))
        {
            var handler = (DownloadHandlerAudioClip)request.downloadHandler;
            handler.streamAudio = false;
            handler.compressed = new FileInfo(full).Length > KeepCompressedAbove;
            yield return request.SendWebRequest();

            if (request.result != UnityWebRequest.Result.Success)
            {
                finish(set, full, null, request.error);
                yield break;
            }

            AudioClip clip = handler.audioClip;
            if (clip == null)
            {
                finish(set, full, null, "Unity could not decode the file");
                yield break;
            }
            clip.name = Path.GetFileNameWithoutExtension(full);
            finish(set, full, clip, null);
        }
    }

    private static AudioType TypeOf(string path)
    {
        switch (Path.GetExtension(path).ToLowerInvariant())
        {
            case ".ogg": return AudioType.OGGVORBIS;
            case ".mp3": return AudioType.MPEG;
            case ".wav": return AudioType.WAV;
            case ".aif":
            case ".aiff": return AudioType.AIFF;
            default: return AudioType.UNKNOWN;
        }
    }
}
