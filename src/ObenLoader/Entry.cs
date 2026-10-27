using System;
using UnityEngine;

namespace ObenLoader;

/// <summary>
/// Startup hook. Unity calls <see cref="Init"/> before the first scene loads because the installer
/// lists it in Obenseuer_Data/RuntimeInitializeOnLoads.json, next to the game's own startup methods.
/// </summary>
public static class Entry
{
    private static bool _started;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    public static void Init()
    {
        if (_started)
            return;
        _started = true;

        try
        {
            Loader.Start();
        }
        catch (Exception e)
        {
            // Last resort: the logger itself may be what failed.
            Debug.LogError("[ObenLoader] Failed to start: " + e);
        }
    }
}
