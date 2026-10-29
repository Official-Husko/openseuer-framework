using System.Collections.Generic;
using UnityEngine;

namespace ObenFramework.Player;

/// <summary>
/// Some levels start with the object that holds the player's worn meshes switched off (the tenement's outside, for
/// one). The game's own ShowPlayerModel only reaches active meshes, so third person (or a body kept in first person)
/// had no body there. While the body should show, the switched-off objects between the player's rig and the player
/// are switched on; they go back off when the body is hidden again, and are forgotten with the level.
/// </summary>
internal static class BodyHolders
{
    private static readonly List<GameObject> Opened = new List<GameObject>();

    /// <summary>Switches the holders on and lets the game show the body again, now that it can reach the meshes.</summary>
    internal static void Open(ThirdPersonCameraCollision view)
    {
        var locator = PlayerLocator.instance;
        if (locator == null || locator.playerRig == null)
            return;
        bool any = false;
        for (var t = locator.playerRig.transform.parent; t != null && t != locator.transform; t = t.parent)
        {
            if (t.gameObject.activeSelf)
                continue;
            t.gameObject.SetActive(true);
            Opened.Add(t.gameObject);
            any = true;
        }
        if (any && view != null)
            view.ShowPlayerModel();
    }

    internal static void Close()
    {
        for (int i = Opened.Count - 1; i >= 0; i--)
        {
            if (Opened[i] != null)
                Opened[i].SetActive(false);
        }
        Opened.Clear();
    }

    /// <summary>The level is gone, and the objects with it.</summary>
    internal static void Forget() => Opened.Clear();
}
