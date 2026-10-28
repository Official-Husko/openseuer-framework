// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
using System;
using System.Collections.Generic;
using UnityEngine;

namespace ObenFramework.SceneLook;

/// <summary>
/// Copies the body's humanoid pose onto a retargeted look every frame (after the body's animator ran), so
/// the look moves with the body. Its pose buffer is made once; syncing doesn't allocate.
/// </summary>
internal sealed class SceneLookPoseDriver : MonoBehaviour
{
    private HumanPoseHandler _from;
    private HumanPoseHandler _to;
    private HumanPose _pose;

    /// <summary>Sync on its own every frame; off lets a mod drive the look's pose itself.</summary>
    public bool Auto = true;

    public void Setup(HumanPoseHandler source, HumanPoseHandler look)
    {
        _from = source;
        _to = look;
        _pose = new HumanPose { muscles = new float[HumanTrait.MuscleCount] };
    }

    public void Sync()
    {
        if (_from == null || _to == null)
            return;
        _from.GetHumanPose(ref _pose);
        _to.SetHumanPose(ref _pose);
    }

    private void LateUpdate()
    {
        if (!Auto)
            return;
        try { Sync(); }
        catch (Exception e)
        {
            SceneLooks.Log.Error("pose driver", e);
            Auto = false;
        }
    }

    private void OnDestroy()
    {
        _from?.Dispose();
        _to?.Dispose();
        _from = null;
        _to = null;
    }
}

/// <summary>
/// Sits on a look's holder and lets the framework forget the look when the holder goes, which happens when
/// the body is destroyed with its scene (or by anything else that destroys the holder).
/// </summary>
internal sealed class SceneLookBodyWatch : MonoBehaviour
{
    internal SceneLookApplied State;

    private void OnDestroy() => SceneLookApply.HolderGone(State);
}

/// <summary>
/// Keeps a re-skinned NPC's own renderers hidden: the game turns them back on now and then (appearance
/// changes, LOD), so they are checked twice a second and switched off again.
/// </summary>
internal sealed class SceneLookLiveGuard : MonoBehaviour
{
    private const float Period = 0.5f;
    private readonly List<SkinnedMeshRenderer> _renderers = new List<SkinnedMeshRenderer>();
    private GameObject[] _own;
    private float _next;

    public void Setup(List<GameObject> ownObjects)
    {
        _own = ownObjects.ToArray();
        _next = Time.unscaledTime + Period;
    }

    private void LateUpdate()
    {
        if (_own == null || Time.unscaledTime < _next)
            return;
        _next = Time.unscaledTime + Period;
        int hidden = 0;
        foreach (var go in _own)
        {
            if (go == null)
                continue;
            go.GetComponents(_renderers);
            foreach (var renderer in _renderers)
            {
                if (renderer == null || !renderer.enabled)
                    continue;
                renderer.enabled = false;
                hidden++;
            }
        }
        if (hidden > 0)
            SceneLooks.Log.Warn("reskin reapplied", "the game turned an NPC's own renderers back on after a re-skin; they were hidden again");
    }
}
