using System;
using ObenFramework.World;
using UnityEngine;

namespace ObenFramework.Env;

/// <summary>
/// Drives the screen effect fades. The component is only enabled while a fade runs, so Unity does not
/// call it at all the rest of the time. Uses unscaled time: fades also run in the pause menu.
/// </summary>
internal sealed class FadeTicker : MonoBehaviour
{
    private static FadeTicker _instance;

    /// <summary>Makes sure the fades are stepped next frame.</summary>
    public static void Wake()
    {
        if (_instance == null)
        {
            var framework = FrameworkMod.Instance;
            if (framework == null)
                return;
            _instance = framework.CreateObject("ScreenFx").AddComponent<FadeTicker>();
        }
        _instance.enabled = true;
    }

    private void Update()
    {
        float dt = Mathf.Min(Time.unscaledDeltaTime, 0.1f);
        bool running = false;
        try
        {
            running |= ScreenBlur.Step(dt);
            running |= ScreenVignette.Step(dt);
        }
        catch (Exception e)
        {
            WorldLog.Failed("Screen effect fade", e);
        }
        if (!running)
            enabled = false;
    }
}
