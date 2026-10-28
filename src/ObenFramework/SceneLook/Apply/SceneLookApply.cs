// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
using System;
using UnityEngine;

namespace ObenFramework.SceneLook;

/// <summary>
/// Puts a loaded look on a body: picks the rig path, builds the renderers under a holder object on the
/// body, hides the body's own skinned renderers and records everything so it can be taken off again. One
/// look per body; a body inside or around another body that wears a look is refused.
/// </summary>
internal static partial class SceneLookApply
{
    public static bool Apply(string modId, SceneLookLook look, GameObject body, bool skeleton, out string reason)
    {
        reason = Check(look, body);
        SceneLookApplied state = null;
        var path = SceneLookRigPath.Refuse;
        try
        {
            if (reason == null)
            {
                Prune();
                reason = Occupied(body);
            }
            if (reason == null)
            {
                var plan = skeleton ? SceneLookRigTools.SkeletonPlan(look, body) : SceneLookRigTools.Plan(look, body);
                path = plan.Path;
                if (plan.Path == SceneLookRigPath.Refuse)
                {
                    reason = plan.Reason;
                }
                else
                {
                    var assets = Assets(look);
                    state = new SceneLookApplied { ModId = modId, Body = body, Look = look, Path = plan.Path };
                    bool built = plan.Path == SceneLookRigPath.Retarget
                        ? SceneLookApplyRig.Retarget(look, body, plan, assets, state, out reason)
                        : SceneLookApplyRig.Bind(look, body, plan, assets, state, out reason);
                    if (built && reason == null)
                    {
                        Hide(body, state);
                        state.Holder.AddComponent<SceneLookBodyWatch>().Body = body;
                        Bodies[body.GetInstanceID()] = state;
                        assets.Bodies++;
                        Note(modId, look, body, plan);
                        return true;
                    }
                }
            }
        }
        catch (Exception e)
        {
            reason = e.Message;
            SceneLooks.Log.Error("apply " + modId, e);
        }

        if (state != null)
            Undo(state);
        if (look != null && Built.TryGetValue(look, out var left) && left.Bodies <= 0)
            Forget(look, left);
        reason ??= "the look could not be applied";
        string note = look != null ? SceneLookNotes.ScaleNote(look) : "";
        if (note.Length > 0)
            reason += note;
        SceneLooks.Log.Warn("apply " + modId + " " + reason, reason + PathLabel(path));
        return false;
    }

    public static string PathLabel(SceneLookRigPath path)
    {
        switch (path)
        {
            case SceneLookRigPath.Name: return " | path name";
            case SceneLookRigPath.Retarget: return " | path retarget";
            case SceneLookRigPath.Skeleton: return " | path skeleton";
            default: return " | path refuse";
        }
    }

    private static string Check(SceneLookLook look, GameObject body)
    {
        if (Application.unityVersion != SceneLookFile.UnityVersion)
            return "unity version not verified: " + Application.unityVersion + " is not " + SceneLookFile.UnityVersion;
        if (look == null)
            return "no look";
        if (body == null)
            return "no body";
        if (look.Files == null)
            return "look has no open file session";
        if (look.Nodes == null || look.Renderers == null || look.Renderers.Count == 0)
            return "no skinned renderers under the root";
        return null;
    }

    private static string Occupied(GameObject body)
    {
        foreach (var state in Bodies.Values)
        {
            if (state.Body == null)
                continue;
            if (state.Body == body)
                return "the body already has an applied look";
            if (body.transform.IsChildOf(state.Body.transform) || state.Body.transform.IsChildOf(body.transform))
                return "the body is above or below a body that already has an applied look";
        }
        return null;
    }

    // The body's own skinned renderers are switched off (not destroyed), so taking the look off restores them.
    private static void Hide(GameObject body, SceneLookApplied state)
    {
        foreach (var renderer in body.GetComponentsInChildren<SkinnedMeshRenderer>(true))
        {
            if (renderer == null || state.Objects.Contains(renderer.gameObject))
                continue;
            state.Hidden.Add(new SceneLookHidden { Renderer = renderer, Enabled = renderer.enabled });
            renderer.enabled = false;
        }
    }

    private static void Note(string modId, SceneLookLook look, GameObject body, SceneLookRigPlan plan)
    {
        string text = "applied " + look.FileName + " to " + body.name + PathLabel(plan.Path) + " | bones " + plan.Bones.Count + " of " + plan.LookBones;
        if (plan.Path == SceneLookRigPath.Skeleton)
            text += " (by name " + plan.NameMapped + ", by role " + plan.RoleMapped + ")";
        SceneLooks.Log.Info(modId + ": " + text + SceneLookNotes.ScaleNote(look) + SceneLookNotes.AxisNote(look));
    }
}
