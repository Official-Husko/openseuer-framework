// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
using System;
using System.Collections.Generic;
using NPC;
using ObenFramework.Player.Look;
using PixelCrushers.DialogueSystem;
using UnityEngine;

namespace ObenFramework.Npcs;

/// <summary>
/// Builds a mod NPC: copies the template's <c>Character</c> under a new id and name, spawns the body's
/// prefab under an inactive holder (so nothing in it wakes up before it is set up), strips what would tie
/// it to the template (save ids, persistence, barks, by default the talk) and hands it to the game's NPC list.
/// </summary>
internal static class ModNpcBuilder
{
    internal sealed class Plan
    {
        public string Owner;
        public string Name;
        public int Template;
        public int Body;
        public string FirstName;
        public string LastName;
        public Vector3 Position;
        public float Yaw;
        public bool KeepDialogue;
        public bool Saved;
        public LookData Look;
        public List<ModNpcShape> Shapes;
    }

    /// <summary>Builds the NPC, or returns null with the reason logged once.</summary>
    public static ModNpc Build(Plan plan)
    {
        var template = ModNpcTemplates.Find(plan.Template);
        if (template == null)
            return Refuse(plan, $"there is no game character {plan.Template} to copy");
        var body = ModNpcTemplates.BodyFor(template, plan.Body);
        var prefab = body != null ? ModNpcTemplates.PrefabOf(body) : null;
        if (prefab == null || prefab.GetComponent<NPCInfo>() == null)
            return Refuse(plan, $"character {(plan.Body != 0 ? plan.Body : plan.Template)} has no NPC body to spawn");
        if (!ModNpcTemplates.Ground(plan.Position, out var ground))
            return Refuse(plan, $"nothing to stand on below {plan.Position}");

        bool sliders = prefab.GetComponentInChildren<Appearance>(true) != null;
        var rotation = Quaternion.Euler(0f, plan.Yaw, 0f);
        var clone = UnityEngine.Object.Instantiate(template);
        clone.name = plan.Owner + "/" + plan.Name;
        clone.ID = ModNpcIds.Allocate(plan.Owner, plan.Name, Taken);
        clone.Name = plan.FirstName;
        clone.LastName = plan.LastName ?? string.Empty;
        clone.resident = null;
        if (plan.Look != null && sliders)
            ModNpcLooks.Write(clone, plan.Look);
        if (plan.Shapes != null)
        {
            foreach (var shape in plan.Shapes)
                ModNpcLooks.PutShape(clone, shape.Mesh, shape.Shape, shape.Weight);
        }

        var holder = new GameObject("ObenFramework NPC holder");
        holder.SetActive(false);
        GameObject go = null;
        bool listed = false;
        try
        {
            var at = NPCSpawnUtilities.FindValidSpawnPosition(ground, clone.FullName, 0.25f, 1.8f, rotation);
            go = UnityEngine.Object.Instantiate(prefab, at, rotation, holder.transform);
            var info = go.GetComponent<NPCInfo>();
            Configure(go, info, template, body, clone, plan.KeepDialogue);
            CharacterDatabase.Characters.Add(clone);
            listed = true;
            go.name = clone.name;
            go.transform.SetParent(null, true);
            UnityEngine.Object.DestroyImmediate(holder);
            holder = null;
            NPCManager.instance.AddNpcToList(info);
            return new ModNpc(plan.Owner, plan.Name, template, body == template ? 0 : body.ID, clone, go, info, sliders, plan.Saved);
        }
        catch
        {
            if (listed)
                CharacterDatabase.Characters.Remove(clone);
            if (go != null)
                UnityEngine.Object.DestroyImmediate(go);
            if (holder != null)
                UnityEngine.Object.DestroyImmediate(holder);
            UnityEngine.Object.Destroy(clone);
            throw;
        }
    }

    /// <summary>Takes a built NPC out of the game: off the NPC list and the character database, its objects destroyed.</summary>
    public static void Tear(ModNpc npc)
    {
        CharacterDatabase.Characters.Remove(npc.Clone);
        if (npc.Root != null)
        {
            var manager = NPCManager.instance;
            if (manager != null && npc.Info != null)
                manager.RemoveNpcFromList(npc.Info);
            npc.Root.SetActive(false);
            UnityEngine.Object.Destroy(npc.Root);
        }
        if (npc.Clone != null)
            UnityEngine.Object.Destroy(npc.Clone);
    }

    private static void Configure(GameObject go, NPCInfo info, Character template, Character body, Character clone, bool keepDialogue)
    {
        // Copies of the template's save ids would make the game save and load this body as the template.
        foreach (var part in go.GetComponentsInChildren<MonoBehaviour>(true))
        {
            if (part != null)
                SaveController.SetObjectGUID(part, string.Empty);
        }
        info.character = clone;
        var controller = info.npcController != null ? info.npcController : go.GetComponent<NPCController>();
        if (controller != null)
            controller.character = clone;
        Repoint(go, template, body, clone);

        bool otherBody = body != template;
        foreach (var talk in go.GetComponentsInChildren<InteractableTalk>(true))
        {
            talk.title = clone.FullName;
            if (!keepDialogue || otherBody)
                talk.talkDisabled = true;
        }
        foreach (var bark in go.GetComponentsInChildren<BarkStarter>(true))
            bark.enabled = false;
        foreach (var trigger in go.GetComponentsInChildren<DialogueSystemTrigger>(true))
        {
            if (!string.IsNullOrEmpty(trigger.barkConversation))
                trigger.enabled = false;
        }
        foreach (var position in go.GetComponentsInChildren<PersistentPositionData>(true))
            UnityEngine.Object.DestroyImmediate(position);
        foreach (var increment in go.GetComponentsInChildren<IncrementOnDestroy>(true))
            UnityEngine.Object.DestroyImmediate(increment);
        foreach (var destructible in go.GetComponentsInChildren<PersistentDestructible>(true))
            UnityEngine.Object.DestroyImmediate(destructible);
    }

    // Points the body's look components at the copy, so the copy's look (not the template's asset) is what
    // they build from and what edits change.
    private static void Repoint(GameObject go, Character template, Character body, Character clone)
    {
        foreach (var appearance in go.GetComponentsInChildren<Appearance>(true))
            appearance.createdCharacter = clone;
        foreach (var baked in go.GetComponentsInChildren<CharacterAppearance>(true))
        {
            if (baked.createdCharacter == template || (baked.createdCharacter == body && MeshesMatch(clone, baked.transform)))
                baked.createdCharacter = clone;
        }
    }

    private static bool MeshesMatch(Character clone, Transform root)
    {
        for (int i = 0; i < root.childCount; i++)
        {
            if (ModNpcLooks.Entry(clone, root.GetChild(i).name) != null)
                return true;
        }
        return false;
    }

    private static bool Taken(int id)
    {
        foreach (var character in CharacterDatabase.Characters)
        {
            if (character != null && character.ID == id)
                return true;
        }
        return false;
    }

    private static ModNpc Refuse(Plan plan, string reason)
    {
        NpcsLog.Warn($"Mod NPC {plan.Owner}/{plan.Name} was not spawned: {reason}.");
        return null;
    }
}
