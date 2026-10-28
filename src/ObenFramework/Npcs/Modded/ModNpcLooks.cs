// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
using System.Collections.Generic;
using ObenFramework.Player.Look;
using UnityEngine;

namespace ObenFramework.Npcs;

/// <summary>
/// Looks of mod NPCs. A mod NPC owns its <c>Character</c> copy, so a look is written straight into it
/// (sliders and worn items, the same tables <see cref="PlayerLook"/> uses) and the body rebuilt from it;
/// blend shapes go into the copy's mesh list, which bodies without sliders are built from.
/// </summary>
internal static class ModNpcLooks
{
    public const float MaxShapeWeight = 200f;

    /// <summary>Writes a look into the copy. The body picks it up when it starts, or at <see cref="Rebuild"/>.</summary>
    public static void Write(Character copy, LookData look)
    {
        copy.GenderString = look.male ? "Male" : "Female";
        copy.Advanced = look.advanced;
        foreach (var dial in LookDials.All)
            dial.Write(copy, dial.Clamp(dial.Get(look)));
        foreach (var slot in LookSlots.All)
        {
            var wearable = WearableTable.Find(slot, look.male, slot.Get(look));
            if (wearable == null && slot.Name == "head")
                wearable = WearableTable.First(slot, look.male);
            slot.Put(copy, wearable);
        }
    }

    /// <summary>Rebuilds a started slider body from its Character after a change.</summary>
    public static void Rebuild(GameObject body)
    {
        var appearance = body.GetComponentInChildren<Appearance>(true);
        if (appearance != null && PlayerAppearances.IsStarted(appearance))
            appearance.LoadCharacter();
        var baked = body.GetComponentInChildren<CharacterAppearance>(true);
        if (baked != null)
            baked.UpdateAppearance();
    }

    /// <summary>The look a slider body shows now, or null for a body without sliders.</summary>
    public static LookData Capture(GameObject body)
    {
        var appearance = body.GetComponentInChildren<Appearance>(true);
        return appearance != null && PlayerAppearances.IsStarted(appearance) ? PlayerLook.Capture(appearance) : null;
    }

    /// <summary>Copies the template's slider values, worn items and blend shapes back over the copy.</summary>
    public static void Reset(Character copy, Character template)
    {
        copy.GenderString = template.GenderString;
        copy.Advanced = template.Advanced;
        copy.Height = template.Height;
        copy.Weight = template.Weight;
        copy.Gender = template.Gender;
        copy.HeadSize = template.HeadSize;
        copy.FaceSize = template.FaceSize;
        copy.NeckLength = template.NeckLength;
        copy.ShoulderWidth = template.ShoulderWidth;
        copy.BreastSize = template.BreastSize;
        copy.HipWidth = template.HipWidth;
        copy.HandSize = template.HandSize;
        copy.FootSize = template.FootSize;
        copy.LipThickness = template.LipThickness;
        copy.JawPosition = template.JawPosition;
        copy.ChinPosition = template.ChinPosition;
        copy.EyeSize = template.EyeSize;
        copy.EyeDistance = template.EyeDistance;
        copy.EyePosition = template.EyePosition;
        copy.EyeDepth = template.EyeDepth;
        copy.EarSize = template.EarSize;
        copy.EarRotation = template.EarRotation;
        copy.NoseRootRotation = template.NoseRootRotation;
        copy.NoseTipRotation = template.NoseTipRotation;
        copy.NoseWidth = template.NoseWidth;
        copy.Head = template.Head;
        copy.Hat = template.Hat;
        copy.Hair = template.Hair;
        copy.Glasses = template.Glasses;
        copy.Beard = template.Beard;
        copy.Jacket = template.Jacket;
        copy.ShirtB = template.ShirtB;
        copy.PantsT = template.PantsT;
        copy.PantsB = template.PantsB;
        copy.Socks = template.Socks;
        copy.Shoes = template.Shoes;
        if (copy.appearance == null || copy.appearance.appearanceMeshes == null)
            return;
        foreach (var mesh in copy.appearance.appearanceMeshes)
        {
            var original = Entry(template, mesh.meshName);
            mesh.blendShapeNames = original != null && original.blendShapeNames != null ? new List<string>(original.blendShapeNames) : null;
            mesh.blendShapeValues = original != null && original.blendShapeValues != null ? new List<float>(original.blendShapeValues) : null;
        }
    }

    /// <summary>
    /// Sets a blend shape on the body's mesh (a child renderer named <paramref name="mesh"/>) now, and in the
    /// copy's mesh list so a rebuild keeps it. False when the body has no such mesh or shape.
    /// </summary>
    public static bool SetShape(GameObject body, Character copy, string mesh, string shape, float weight)
    {
        weight = Mathf.Clamp(weight, -MaxShapeWeight, MaxShapeWeight);
        bool found = false;
        foreach (var renderer in body.GetComponentsInChildren<SkinnedMeshRenderer>(true))
        {
            if (renderer == null || renderer.sharedMesh == null || renderer.gameObject.name != mesh)
                continue;
            int index = renderer.sharedMesh.GetBlendShapeIndex(shape);
            if (index < 0)
                continue;
            renderer.SetBlendShapeWeight(index, weight);
            found = true;
        }
        if (found)
            PutShape(copy, mesh, shape, weight);
        return found;
    }

    /// <summary>Writes a blend shape into the copy's mesh list (creating the mesh entry when the copy has none).</summary>
    public static void PutShape(Character copy, string mesh, string shape, float weight)
    {
        if (copy.appearance == null)
            copy.appearance = new Character.Appearance();
        var entry = Entry(copy, mesh);
        if (entry == null)
        {
            entry = new Character.AppearanceMesh { meshName = mesh };
            copy.appearance.appearanceMeshes.Add(entry);
        }
        if (entry.blendShapeNames == null || entry.blendShapeValues == null)
        {
            entry.blendShapeNames = new List<string>();
            entry.blendShapeValues = new List<float>();
        }
        int index = entry.blendShapeNames.IndexOf(shape);
        if (index < 0)
        {
            entry.blendShapeNames.Add(shape);
            entry.blendShapeValues.Add(weight);
        }
        else
        {
            entry.blendShapeValues[index] = weight;
        }
    }

    /// <summary>The copy's mesh entry with this name, or null.</summary>
    public static Character.AppearanceMesh Entry(Character character, string mesh)
    {
        var meshes = character.appearance != null ? character.appearance.appearanceMeshes : null;
        if (meshes == null)
            return null;
        foreach (var entry in meshes)
        {
            if (entry != null && entry.meshName == mesh)
                return entry;
        }
        return null;
    }
}
