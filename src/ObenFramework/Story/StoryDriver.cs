using System;
using ObenFramework.Events;
using ObenLoader;
using UnityEngine;

namespace ObenFramework.Story;

/// <summary>
/// Runs <see cref="StoryPump.Tick"/> in LateUpdate, after the game's own Update calls of the frame. It is
/// disabled while the pump has nothing to do, so it costs nothing in between.
/// </summary>
internal sealed class StoryDriver : MonoBehaviour
{
    private static StoryDriver _instance;

    public static void Create(ObenMod framework)
    {
        _instance = framework.CreateObject("StoryPump").AddComponent<StoryDriver>();
        _instance.enabled = false;
    }

    /// <summary>Makes sure the next LateUpdate looks at the pump.</summary>
    public static void Wake()
    {
        if (_instance != null)
            _instance.enabled = true;
    }

    private void LateUpdate()
    {
        try
        {
            enabled = StoryPump.Tick();
        }
        catch (Exception e)
        {
            GameEvents.Log.Error("The story pump failed", e);
            enabled = false;
        }
    }

    private void OnDestroy()
    {
        if (_instance == this)
            _instance = null;
    }
}
