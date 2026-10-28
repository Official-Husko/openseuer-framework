// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace ObenFramework.SceneLook;

/// <summary>
/// Scans one scene file for character looks: groups its skinned renderers by the body they belong to
/// (an NPC or an animated object), checks each can be rebuilt, and matches the NPCs to their bodies and
/// <c>Character</c> assets. A scan of a big level reads a few megabytes and takes a moment, so results are
/// cached by <see cref="SceneLookIndex"/>.
/// </summary>
internal static class SceneLookScan
{
    // Opened by nearly every look; kept open between scans.
    private const string KeptFile = "resources.assets";

    public static SceneLookFileScan ScanFile(SceneLookFiles files, string fileName)
    {
        var result = new SceneLookFileScan { FileName = fileName };
        var watch = Stopwatch.StartNew();
        try
        {
            var file = files.Get(fileName, out string reason);
            if (file == null)
                result.Error = "unreadable or not installed: " + reason;
            else
                ScanOpened(files, file, result);
        }
        catch (Exception e)
        {
            result.Error = e.Message;
        }
        finally
        {
            files.CloseAllExcept(KeptFile);
            result.Seconds = watch.Elapsed.TotalSeconds;
        }
        return result;
    }

    /// <summary>The skinned renderers whose body is <paramref name="root"/>.</summary>
    public static List<long> RenderersUnder(SceneLookFile file, long root)
    {
        var found = new List<long>();
        foreach (long pathId in file.Order)
        {
            if (file.ClassId(pathId) == SceneLookClass.SkinnedMeshRenderer && BodyOf(file, pathId) == root)
                found.Add(pathId);
        }
        return found;
    }

    private static void ScanOpened(SceneLookFiles files, SceneLookFile file, SceneLookFileScan result)
    {
        var state = new SceneLookChecks.State();
        var renderers = new List<SceneLookChecks.RendererCheck>();
        foreach (long pathId in file.Order)
        {
            if (file.ClassId(pathId) == SceneLookClass.SkinnedMeshRenderer)
                renderers.Add(SceneLookChecks.Renderer(files, file, pathId, state));
        }

        var groups = new Dictionary<long, List<SceneLookChecks.RendererCheck>>();
        var bodies = new List<long>();
        var unrooted = new List<SceneLookChecks.RendererCheck>();
        foreach (var renderer in renderers)
        {
            long body = renderer.GameObject == 0 ? 0 : SceneLookReader.BodyRoot(file, renderer.GameObject);
            if (body == 0)
            {
                unrooted.Add(renderer);
                continue;
            }
            if (!groups.ContainsKey(body))
            {
                groups[body] = new List<SceneLookChecks.RendererCheck>();
                bodies.Add(body);
            }
            groups[body].Add(renderer);
        }
        foreach (long body in bodies)
            result.Roots.Add(GroupRoot(file, body, groups[body]));
        foreach (long pathId in SceneLookReader.NpcBehaviours(file))
            result.Npcs.Add(ScanNpc(file, pathId));
        AttachNpcs(file, result.Roots, result.Npcs);

        foreach (long pathId in file.Order)
        {
            int classId = file.ClassId(pathId);
            if (classId == SceneLookClass.Texture2D)
                result.TextureFormats.Add(SceneLookTextureReader.HeadFormat(file, pathId));
            else if (classId == SceneLookClass.Shader)
                result.ShaderObjects.Add(SceneLookMaterialReader.ShaderLabel(file, pathId));
        }

        result.Renderers = renderers.Count;
        foreach (var renderer in renderers)
        {
            if (renderer.Reasons.Count == 0)
                result.CleanRenderers++;
            foreach (string reason in new HashSet<string>(renderer.Reasons))
                result.ReasonCounts.Add(reason);
            foreach (int format in renderer.Formats)
                result.FormatsUsed.Add(format);
            foreach (string shader in renderer.Shaders)
                result.ShadersUsed.Add(shader);
        }
        result.Unrooted = unrooted.Count;
        foreach (var renderer in unrooted)
            result.UnrootedRigs.Add(SceneLookReader.RigKind(new HashSet<string>(renderer.Bones)));
    }

    // A renderer's game object is the first reference in its bytes.
    private static long BodyOf(SceneLookFile file, long pathId)
    {
        long go = new SceneLookCursor(file.ReadObjectHead(pathId, 12), 0).Pptr().PathId;
        return file.Contains(go) ? SceneLookReader.BodyRoot(file, go) : 0;
    }

    private static SceneLookRoot GroupRoot(SceneLookFile file, long body, List<SceneLookChecks.RendererCheck> renderers)
    {
        var names = new HashSet<string>();
        var reasons = new HashSet<string>();
        var formats = new HashSet<int>();
        var shaders = new HashSet<string>();
        long vertices = 0;
        long triangles = 0;
        foreach (var renderer in renderers)
        {
            names.UnionWith(renderer.Bones);
            reasons.UnionWith(renderer.Reasons);
            formats.UnionWith(renderer.Formats);
            shaders.UnionWith(renderer.Shaders);
            vertices += renderer.Vertices;
            triangles += renderer.Triangles;
        }
        var kinds = SceneLookReader.Kinds(file, body);
        var hierarchy = new HashSet<string>();
        foreach (long go in SceneLookReader.Subtree(file, body))
            hierarchy.Add(SceneLookReader.GameObject(file, go).Name);

        var root = new SceneLookRoot
        {
            GameObject = body,
            Name = SceneLookReader.GameObject(file, body).Name,
            Path = SceneLookReader.PathName(file, body),
            IsNpc = kinds.Contains("NPCInfo"),
            HasAnimator = kinds.Contains("Animator"),
            Renderers = renderers.Count,
            BoneNames = Sorted(names),
            Rig = SceneLookReader.RigKind(names),
            Avatars = new List<SceneLookAvatarInfo>(),
            Vertices = vertices,
            Triangles = triangles,
            Reasons = Sorted(reasons),
            Formats = new List<int>(formats),
            Shaders = Sorted(shaders),
            Hierarchy = Sorted(hierarchy),
        };
        foreach (var animator in SceneLookReader.Animators(file, body))
            root.Avatars.Add(SceneLookReader.AvatarInfo(file, animator));
        root.Avatar = AvatarSummary(root.Avatars);
        root.Clean = root.Reasons.Count == 0;
        root.Formats.Sort();
        return root;
    }

    private static SceneLookNpc ScanNpc(SceneLookFile file, long pathId)
    {
        try { return SceneLookReader.ReadNpc(file, pathId); }
        catch (Exception e) { return new SceneLookNpc { PathId = pathId, Name = "?", Path = e.Message }; }
    }

    // An NPC belongs to a body when it sits on it, or its character model is the body, above it or below it.
    private static void AttachNpcs(SceneLookFile file, List<SceneLookRoot> roots, List<SceneLookNpc> npcs)
    {
        var chains = new Dictionary<long, List<long>>();
        foreach (var root in roots)
            chains[root.GameObject] = SceneLookReader.Chain(file, root.GameObject);
        var linked = new HashSet<long>();
        foreach (var npc in npcs)
        {
            foreach (var root in roots)
            {
                long body = root.GameObject;
                bool match = npc.GameObject == body
                    || (npc.ModelGameObject != 0 && (SceneLookReader.Chain(file, npc.ModelGameObject).Contains(body) || chains[body].Contains(npc.ModelGameObject)));
                if (!match)
                    continue;
                if (linked.Add(body))
                {
                    root.Character = npc.Character;
                    root.CharacterNote = npc.CharacterNote;
                }
                npc.Body = body;
                break;
            }
        }
    }

    private static string AvatarSummary(List<SceneLookAvatarInfo> infos)
    {
        bool humanoid = false;
        bool generic = false;
        foreach (var info in infos)
        {
            if (info.Kind == "humanoid")
                humanoid = true;
            else if (info.Kind == "generic")
                generic = true;
        }
        return humanoid ? "humanoid" : generic ? "generic" : "none";
    }

    private static List<string> Sorted(ICollection<string> values)
    {
        var list = new List<string>(values);
        list.Sort(StringComparer.Ordinal);
        return list;
    }
}
