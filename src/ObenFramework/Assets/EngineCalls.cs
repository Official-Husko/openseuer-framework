using System;
using HarmonyLib;
using UnityEngine;

namespace ObenFramework.Assets;

/// <summary>
/// Two engine calls that have newer overloads taking <c>ReadOnlySpan</c>. The framework compiles against the
/// .NET Framework reference assemblies, which have no such type, so the compiler can't pick between the
/// overloads. Calling the array versions through delegates avoids that.
/// </summary>
internal static class EngineCalls
{
    private static readonly Func<Texture2D, byte[], bool, bool> LoadImageCall =
        AccessTools.MethodDelegate<Func<Texture2D, byte[], bool, bool>>(
            AccessTools.Method(typeof(ImageConversion), "LoadImage", new[] { typeof(Texture2D), typeof(byte[]), typeof(bool) }));

    private static readonly Func<AudioClip, float[], int, bool> SetDataCall =
        AccessTools.MethodDelegate<Func<AudioClip, float[], int, bool>>(
            AccessTools.Method(typeof(AudioClip), "SetData", new[] { typeof(float[]), typeof(int) }));

    /// <summary><c>ImageConversion.LoadImage(texture, bytes, markNonReadable)</c>.</summary>
    public static bool LoadImage(Texture2D texture, byte[] bytes, bool markNonReadable) =>
        LoadImageCall(texture, bytes, markNonReadable);

    /// <summary><c>clip.SetData(samples, 0)</c>.</summary>
    public static bool SetData(AudioClip clip, float[] samples, int offset) =>
        SetDataCall(clip, samples, offset);
}
