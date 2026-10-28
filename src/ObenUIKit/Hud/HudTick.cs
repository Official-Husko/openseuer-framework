using System;
using ObenUIKit.Runtime;
using UnityEngine;

namespace ObenUIKit;

/// <summary>
/// The HUD's per-frame hook. It is enabled only while a hint, a subtitle or a bar exists, and a fault
/// switches it off (with the error logged once) instead of repeating every frame.
/// </summary>
internal sealed class HudTick : MonoBehaviour
{
    private HudRuntime _runtime;

    public void Init(HudRuntime runtime)
    {
        _runtime = runtime;
        enabled = false;
    }

    private void Update()
    {
        try
        {
            _runtime.Tick(Time.unscaledDeltaTime);
        }
        catch (Exception e)
        {
            enabled = false;
            KitRuntime.Current?.Kit.Log.Error("The HUD stopped updating", e);
        }
    }
}
