using System;
using ObenLoader.Logging;
using UnityEngine;

namespace ObenUIKit;

/// <summary>
/// The per-frame hook of prompts and guides. It is enabled only while one exists, and a fault switches it
/// off (with the error logged once) instead of repeating every frame.
/// </summary>
internal sealed class PromptTick : MonoBehaviour
{
    private PromptRuntime _runtime;
    private ModLogger _log;

    public void Init(PromptRuntime runtime, ModLogger log)
    {
        _runtime = runtime;
        _log = log;
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
            _log.Error("Prompts stopped updating", e);
        }
    }
}
