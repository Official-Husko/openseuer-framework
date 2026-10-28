using System;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ObenFramework.World.Props;

/// <summary>
/// Makes a prop usable with the game's own interact key and prompt. It is the game's
/// <c>Interactable</c> without a GUID, so the save system never writes it. One sits on every part of the
/// prop that has a collider, because the game only looks for interactables on the object its ray hit.
/// </summary>
internal sealed class PropUse : Interactable
{
    // Layers the game's interact ray treats as usable (InteractObjects.IsUsableLayer), most common first.
    private static readonly int[] UsableLayers = { 17, 23, 29, 20, 15 };

    internal Action Used;

    public override void Interact()
    {
        try { Used?.Invoke(); }
        catch (Exception e) { WorldProps.Log.Error("prop use handler", e); }
    }

    /// <summary>Adds the use prompt to every collider part of <paramref name="go"/>. False when the game can't target it.</summary>
    public static bool Attach(GameObject go, string title, Action used)
    {
        var objects = InteractObjects.instance;
        if (objects == null)
            return false;

        bool any = false;
        foreach (var collider in go.GetComponentsInChildren<Collider>(true))
        {
            var part = collider.gameObject;
            int layer = ChooseLayer(objects, part.layer);
            if (layer < 0)
                continue;
            part.layer = layer;
            var use = part.GetComponent<PropUse>();
            if (use == null)
                use = part.AddComponent<PropUse>();
            use.title = title;
            use.useMessage = "Use";
            use.Used = used;
            any = true;
        }
        return any;
    }

    public static void Detach(GameObject go)
    {
        foreach (var use in go.GetComponentsInChildren<PropUse>(true))
            Object.Destroy(use);
    }

    private static int ChooseLayer(InteractObjects objects, int current)
    {
        int mask = objects.MaskList.value;
        if (objects.IsUsableLayer(current) && (mask & (1 << current)) != 0)
            return current;
        foreach (int layer in UsableLayers)
        {
            if ((mask & (1 << layer)) != 0)
                return layer;
        }
        return -1;
    }
}
