// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
using System;
using System.Collections.Generic;

namespace ObenFramework.SceneLook;

/// <summary>
/// The looks of each scene, scanned once and kept (a failed scan is kept too, with its reason). Scenes are
/// matched to files by build index: scene n of the build settings is the file <c>leveln</c>.
/// </summary>
internal sealed class SceneLookIndex
{
    private readonly SceneLookFiles _files;
    private readonly IList<string> _scenes;
    private readonly Dictionary<string, List<SceneLookInfo>> _lists = new Dictionary<string, List<SceneLookInfo>>();
    private readonly Dictionary<string, string> _failures = new Dictionary<string, string>();

    public SceneLookIndex(SceneLookFiles files, IList<string> scenes)
    {
        _files = files;
        _scenes = scenes;
    }

    public IList<string> Scenes => _scenes;

    public int ScannedCount => _lists.Count;

    public List<SceneLookInfo> List(string scene, out string reason)
    {
        string level = LevelOf(_scenes, scene, out reason);
        if (level == null)
            return null;
        if (_lists.TryGetValue(level, out var looks))
            return looks;
        if (!_failures.TryGetValue(level, out string failure))
        {
            var scan = SceneLookScan.ScanFile(_files, level);
            if (scan.Error == null)
            {
                looks = Entries(SceneName(scene), level, scan);
                _lists[level] = looks;
                return looks;
            }
            failure = level + ": " + scan.Error;
            _failures[level] = failure;
        }
        reason = failure;
        return null;
    }

    /// <summary>A look by body name or character name, ignoring case.</summary>
    public SceneLookInfo FindByName(string scene, string name, out string reason)
    {
        var looks = List(scene, out reason);
        if (looks == null)
            return null;
        foreach (var look in looks)
        {
            if (string.Equals(look.Name, name, StringComparison.OrdinalIgnoreCase))
                return look;
            if (look.HasCharacter && string.Equals(look.CharacterName, name, StringComparison.OrdinalIgnoreCase))
                return look;
        }
        reason = "no look named " + name + " in " + scene;
        return null;
    }

    /// <summary>The first look linked to a character, searching every scene in build order. Scans what it has to.</summary>
    public SceneLookInfo FindByCharacter(int characterId, out string reason)
    {
        string firstFailure = null;
        foreach (string scene in _scenes)
        {
            var looks = List(scene, out string failure);
            if (looks == null)
            {
                firstFailure ??= failure;
                continue;
            }
            var look = WithCharacter(looks, characterId);
            if (look != null)
            {
                reason = null;
                return look;
            }
        }
        reason = "no look linked to character " + characterId + (firstFailure == null ? "" : "; " + firstFailure);
        return null;
    }

    public SceneLookInfo FindByCharacter(string scene, int characterId, out string reason)
    {
        var looks = List(scene, out reason);
        if (looks == null)
            return null;
        var look = WithCharacter(looks, characterId);
        reason = look == null ? "no look linked to character " + characterId + " in " + scene : null;
        return look;
    }

    /// <summary>A look by its full path, or by name when only one look has it.</summary>
    public SceneLookInfo Root(string scene, string root, out string reason)
    {
        var looks = List(scene, out reason);
        if (looks == null)
            return null;
        SceneLookInfo named = null;
        int namedCount = 0;
        foreach (var look in looks)
        {
            if (look.Path == root)
                return look;
            if (look.Name != root)
                continue;
            named = look;
            namedCount++;
        }
        if (namedCount == 1)
            return named;
        reason = namedCount == 0 ? "no look " + root + " in " + scene : "more than one look named " + root + " in " + scene + ", use its path";
        return null;
    }

    public static string LevelOf(IList<string> scenes, string scene, out string reason)
    {
        reason = null;
        for (int i = 0; i < scenes.Count; i++)
        {
            if (string.Equals(scenes[i], scene, StringComparison.OrdinalIgnoreCase))
                return "level" + i;
        }
        reason = "scene not in build settings: " + scene;
        return null;
    }

    /// <summary>Only the looks that are characters: NPCs, the game's rigs, or animated bodies with a skeleton.</summary>
    public static List<SceneLookInfo> Characters(List<SceneLookInfo> looks)
    {
        var found = new List<SceneLookInfo>();
        foreach (var look in looks)
        {
            if (IsCharacter(look))
                found.Add(look);
        }
        return found;
    }

    public static bool IsCharacter(SceneLookInfo look) =>
        look.IsNpc || look.RigKind == "Bip001" || look.RigKind == "Blender-style" || (look.HasAnimator && look.Bones >= 30);

    private string SceneName(string scene)
    {
        foreach (string name in _scenes)
        {
            if (string.Equals(name, scene, StringComparison.OrdinalIgnoreCase))
                return name;
        }
        return scene;
    }

    private static SceneLookInfo WithCharacter(List<SceneLookInfo> looks, int characterId)
    {
        foreach (var look in looks)
        {
            if (look.HasCharacter && look.CharacterId == characterId)
                return look;
        }
        return null;
    }

    private static List<SceneLookInfo> Entries(string scene, string level, SceneLookFileScan scan)
    {
        var looks = new List<SceneLookInfo>();
        foreach (var root in scan.Roots)
        {
            var info = new SceneLookInfo
            {
                Scene = scene,
                Level = level,
                Name = root.Name,
                Path = root.Path,
                Root = root.GameObject,
                Renderers = root.Renderers,
                Bones = root.BoneNames.Count,
                RigKind = root.Rig,
                AvatarKind = root.Avatar,
                HasAnimator = root.HasAnimator,
                IsNpc = root.IsNpc,
                Clean = root.Clean,
                Problems = root.Reasons,
            };
            SetCharacter(info, root.Character);
            looks.Add(info);
        }
        // NPCs whose body has no skinned renderer of its own still show, so they can be found by character.
        foreach (var npc in scan.Npcs)
        {
            if (npc.Body != 0)
                continue;
            var info = new SceneLookInfo
            {
                Scene = scene,
                Level = level,
                Name = npc.Name,
                Path = npc.Path,
                Root = npc.GameObject,
                RigKind = "no bones",
                AvatarKind = "none",
                IsNpc = true,
            };
            SetCharacter(info, npc.Character);
            looks.Add(info);
        }
        return looks;
    }

    private static void SetCharacter(SceneLookInfo info, SceneLookCharacter character)
    {
        if (character == null)
            return;
        info.HasCharacter = true;
        info.CharacterId = character.Id;
        info.CharacterName = character.Name;
    }
}
