// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace ObenFramework.SceneLook;

public static partial class SceneLooks
{
    /// <summary>
    /// A text report of a loaded look: its node tree with file poses, its renderers with bones and bind
    /// pose scales, and, for a body (the one wearing it when none is given), the rig path that would be or
    /// was taken. Meant for working out why a look comes out wrong.
    /// </summary>
    public static string Describe(LoadedSceneLook look, GameObject body = null)
    {
        try
        {
            if (look == null)
                return "no look";
            if (!look.Ready || look.Look == null)
                return "the look is not loaded: " + look.Reason;
            var data = look.Look;
            if (body == null)
                body = SceneLookApply.BodyOf(data);

            var text = new StringBuilder();
            text.AppendLine("look " + data.FileName + " " + look.Path + " in " + look.Scene);
            text.AppendLine("nodes " + data.Nodes.Count + " | renderers " + data.Renderers.Count + " | bone names " + look.BoneNames.Count + " | vertices " + look.VertexCount);
            text.AppendLine("node tree, file TRS:");
            Tree(text, data);
            text.AppendLine("renderers, bind scale:");
            var byTransform = SceneLookFrame.ByTransform(data);
            foreach (var renderer in data.Renderers)
            {
                var node = SceneLookFrame.Node(data, renderer.Source.GameObject.PathId);
                string chain = node == null ? "0" : SceneLookNotes.Number(SceneLookFrame.Scale(SceneLookFrame.RelativeWithoutRoot(data, node, byTransform)));
                int shapes = renderer.Mesh.BlendShapeNames != null ? renderer.Mesh.BlendShapeNames.Count : 0;
                text.AppendLine("  " + renderer.Name + " | bones " + renderer.Bones.Count + " | bind poses " + Poses(renderer) + " | blend shapes " + shapes
                    + " | mesh frame scale " + chain + " | bind pose scale " + BindScale(renderer));
            }

            if (body == null)
            {
                text.AppendLine("path and avatar: no body is wearing this look, apply it first or pass a body");
                return text.ToString();
            }
            var plan = SceneLookRigTools.Plan(data, body);
            text.AppendLine("path " + Label(plan.Path) + (plan.Reason == null ? "" : ": " + plan.Reason));
            text.AppendLine("human roles " + plan.RequiredCovered + " of " + plan.RequiredBones + " | mapped bones " + plan.Bones.Count);
            var state = SceneLookApply.State(body);
            var avatar = state != null && state.Avatar != null ? state.Avatar : SceneLookRigTools.HumanAvatar(body);
            text.AppendLine(avatar == null
                ? "avatar none"
                : "avatar valid " + avatar.isValid + " | human " + avatar.isHuman + " | held by the look " + (state != null && state.Avatar != null));
            return text.ToString();
        }
        catch (Exception e)
        {
            Log.Error("describe", e);
            return "describe failed: " + e.Message;
        }
    }

    private static void Tree(StringBuilder text, SceneLookLook look)
    {
        var byPath = new Dictionary<long, SceneLookNode>(look.Nodes.Count);
        SceneLookNode root = null;
        foreach (var node in look.Nodes)
        {
            byPath[node.Transform] = node;
            if (node.GameObject == look.Root)
                root = node;
        }
        if (root != null)
            Tree(text, root, byPath, "  ", 0);
    }

    private static void Tree(StringBuilder text, SceneLookNode node, Dictionary<long, SceneLookNode> byPath, string indent, int depth)
    {
        if (node == null || depth > 64)
            return;
        text.AppendLine(indent + node.Name + " | pos (" + SceneLookNotes.Number(node.Position) + ") | rot (" + SceneLookNotes.Number(node.Rotation)
            + ") | scale (" + SceneLookNotes.Number(node.Scale) + ")");
        if (node.Children == null)
            return;
        foreach (var child in node.Children)
        {
            if (child.FileId == 0 && byPath.TryGetValue(child.PathId, out var found))
                Tree(text, found, byPath, indent + "  ", depth + 1);
        }
    }

    private static int Poses(SceneLookLookRenderer renderer) =>
        renderer.Mesh?.BindPoses != null ? renderer.Mesh.BindPoses.Length : 0;

    private static string BindScale(SceneLookLookRenderer renderer)
    {
        var poses = renderer.Mesh?.BindPoses;
        return poses == null || poses.Length == 0 ? "none" : SceneLookNotes.Number(SceneLookNotes.BindScale(poses[0]));
    }
}
