// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
using System;
using System.Collections.Generic;
using System.Text;

namespace ObenFramework.SceneLook;

internal static partial class SceneLookReader
{
    /// <summary>The script class name of a MonoBehaviour, or "?" when it can't be read.</summary>
    public static string BehaviourClass(SceneLookFile f, long pathId)
    {
        if (f.Memo.BehaviourClasses.TryGetValue(pathId, out var cached))
            return cached;
        string cls;
        try { cls = ScriptClass(f, ReadBehaviour(new SceneLookCursor(f.ReadObject(pathId), 0), pathId).Script); }
        catch (Exception) { cls = "?"; }
        f.Memo.BehaviourClasses[pathId] = cls;
        return cls;
    }

    /// <summary>The class name a MonoScript names (its m_ClassName), or "?".</summary>
    public static string ScriptClass(SceneLookFile f, SceneLookRef script)
    {
        var target = f.Target(script);
        if (target.Error != null)
            return "?";
        if (target.File.Memo.ScriptClasses.TryGetValue(target.PathId, out var cached))
            return cached;
        string cls;
        try
        {
            var cursor = new SceneLookCursor(target.File.ReadObject(target.PathId), 0);
            cursor.Text();    // m_Name
            cursor.I32();     // execution order
            cursor.Skip(16);  // properties hash
            cls = cursor.Text();
        }
        catch (Exception)
        {
            cls = "?";
        }
        target.File.Memo.ScriptClasses[target.PathId] = cls;
        return cls;
    }

    public static SceneLookBehaviour ReadBehaviour(SceneLookCursor c, long pathId)
    {
        var behaviour = new SceneLookBehaviour { PathId = pathId, GameObject = c.Pptr() };
        c.U8(); // enabled
        c.Align(4);
        behaviour.Script = c.Pptr();
        behaviour.Name = c.Text();
        return behaviour;
    }

    /// <summary>Every MonoBehaviour of the file whose script is <c>NPCInfo</c>.</summary>
    public static List<long> NpcBehaviours(SceneLookFile f)
    {
        var list = new List<long>();
        foreach (long pathId in f.Order)
        {
            if (f.ClassId(pathId) != SceneLookClass.MonoBehaviour)
                continue;
            SceneLookBehaviour header;
            try { header = ReadBehaviour(new SceneLookCursor(f.ReadObjectHead(pathId, 512), 0), pathId); }
            catch (Exception) { continue; }
            if (ScriptClass(f, header.Script) == "NPCInfo")
                list.Add(pathId);
        }
        return list;
    }

    /// <summary>An NPC: its object, path, the <c>Character</c> asset it uses and the object its model is on.</summary>
    public static SceneLookNpc ReadNpc(SceneLookFile f, long pathId)
    {
        var info = ParseNpcInfo(pathId, f.ReadObject(pathId));
        var npc = new SceneLookNpc { PathId = pathId, GameObject = info.GameObject.PathId };
        npc.Name = GameObject(f, npc.GameObject).Name;
        npc.Path = PathName(f, npc.GameObject);

        var character = f.Target(info.Character);
        if (character.Error == null)
        {
            try
            {
                var head = ParseCharacter(character.File.ReadObject(character.PathId));
                if (head.Addressable.StartsWith("Assets/", StringComparison.Ordinal))
                    npc.Character = head;
                else
                    npc.CharacterNote = "not a Character";
            }
            catch (Exception)
            {
                npc.CharacterNote = "unreadable";
            }
        }

        var model = f.Target(info.CharacterModel);
        if (model.Error == null && model.File == f && f.ClassId(model.PathId) == SceneLookClass.MonoBehaviour)
            npc.ModelGameObject = ReadBehaviour(new SceneLookCursor(f.ReadObject(model.PathId), 0), model.PathId).GameObject.PathId;
        return npc;
    }

    public static SceneLookAnimator ParseAnimator(long pathId, byte[] bytes)
    {
        var c = new SceneLookCursor(bytes, 0);
        var animator = new SceneLookAnimator { PathId = pathId };
        c.Pptr(); // game object
        c.U8();   // enabled
        c.Align(4);
        animator.Avatar = c.Pptr();
        animator.Controller = c.Pptr();
        return animator;
    }

    public static List<SceneLookAnimator> Animators(SceneLookFile f, long body)
    {
        var list = new List<SceneLookAnimator>();
        foreach (long go in Subtree(f, body))
        {
            foreach (var component in GameObject(f, go).Components)
            {
                if (component.FileId == 0 && f.Contains(component.PathId) && f.ClassId(component.PathId) == SceneLookClass.Animator)
                    list.Add(ParseAnimator(component.PathId, f.ReadObject(component.PathId)));
            }
        }
        return list;
    }

    /// <summary>Whether an animator's avatar is humanoid (it names all 15 required roles), generic or missing.</summary>
    public static SceneLookAvatarInfo AvatarInfo(SceneLookFile f, SceneLookAnimator animator)
    {
        var info = new SceneLookAvatarInfo();
        var avatar = f.Target(animator.Avatar);
        if (avatar.Error == "null")
        {
            info.Kind = "none";
        }
        else if (avatar.Error != null)
        {
            info.Kind = avatar.Error;
        }
        else
        {
            info.Roles = RoleCount(avatar.File.ReadObject(avatar.PathId));
            info.Kind = info.Roles == SceneLookRoles.Required.Length ? "humanoid" : "generic";
        }
        var controller = f.Target(animator.Controller);
        if (controller.Error == null)
            info.ControllerName = new SceneLookCursor(controller.File.ReadObject(controller.PathId), 0).Text();
        return info;
    }

    /// <summary>"Bip001" (3ds Max biped), "Blender-style" (dotted names), "other" or "no bones", by majority.</summary>
    public static string RigKind(ICollection<string> names)
    {
        int count = names.Count;
        if (count == 0)
            return "no bones";
        int bip = 0;
        int blender = 0;
        foreach (string name in names)
        {
            if (name.StartsWith("Bip001", StringComparison.Ordinal))
                bip++;
            if (name.IndexOf('.') >= 0)
                blender++;
        }
        if (2 * bip >= count)
            return "Bip001";
        if (2 * blender >= count)
            return "Blender-style";
        return "other";
    }

    /// <summary>
    /// The start of the game's <c>Character</c> asset: the ScriptableObject header, then
    /// <c>addressableAssetPath</c>, <c>ID</c>, names, age, relationship, job, sprite and prefabs.
    /// </summary>
    public static SceneLookCharacter ParseCharacter(byte[] bytes)
    {
        var c = new SceneLookCursor(bytes, 0);
        c.Pptr(); // game object
        c.U32();  // enabled, padded
        c.Pptr(); // script
        var character = new SceneLookCharacter
        {
            AssetName = c.Text(),
            Addressable = c.Text(),
            Id = c.I32(),
            Name = c.Text(),
            LastName = c.Text(),
            GenderString = c.Text(),
            Age = c.I32(),
            Relationship = c.Text(),
            Job = c.Text(),
            Sprite = c.Pptr(),
            Prefab = c.Pptr(),
            PrefabWithAi = c.Pptr(),
        };
        return character;
    }

    /// <summary>The start of the game's <c>NPCInfo</c>: GUID, global, saved time (8 four-byte values), character, model.</summary>
    public static SceneLookNpcInfo ParseNpcInfo(long pathId, byte[] bytes)
    {
        var c = new SceneLookCursor(bytes, 0);
        var header = ReadBehaviour(c, pathId);
        var info = new SceneLookNpcInfo { PathId = pathId, GameObject = header.GameObject, Guid = c.Text() };
        info.Global = c.U8() != 0;
        c.Align(4);
        info.SavedTimeAndDay = c.Raw(32);
        info.Character = c.Pptr();
        info.CharacterModel = c.Pptr();
        return info;
    }

    /// <summary>Whether <paramref name="needle"/> occurs in <paramref name="haystack"/>.</summary>
    public static bool Contains(byte[] haystack, byte[] needle)
    {
        int last = haystack.Length - needle.Length;
        int start = 0;
        while (start <= last)
        {
            int i = Array.IndexOf(haystack, needle[0], start, last - start + 1);
            if (i < 0)
                return false;
            int j = 1;
            while (j < needle.Length && haystack[i + j] == needle[j])
                j++;
            if (j == needle.Length)
                return true;
            start = i + 1;
        }
        return false;
    }

    /// <summary>A string as Unity serializes it: a four-byte length, then the UTF-8 bytes.</summary>
    public static byte[] LengthPrefixed(string text)
    {
        byte[] body = Encoding.UTF8.GetBytes(text);
        var result = new byte[4 + body.Length];
        Buffer.BlockCopy(BitConverter.GetBytes(body.Length), 0, result, 0, 4);
        Buffer.BlockCopy(body, 0, result, 4, body.Length);
        return result;
    }

    // An avatar's human description lists role names as strings; counting the required ones tells humanoid from generic.
    private static int RoleCount(byte[] bytes)
    {
        int count = 0;
        foreach (string role in SceneLookRoles.Required)
        {
            if (Contains(bytes, LengthPrefixed(role)))
                count++;
        }
        return count;
    }
}
