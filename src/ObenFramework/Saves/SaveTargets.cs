using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ObenFramework.Saves;

/// <summary>Finds the savable components the game itself would visit.</summary>
internal static class SaveTargets
{
    /// <summary>The savable components under <paramref name="root"/>, with every phase enabled.</summary>
    public static List<SaveTarget> Under(GameObject root, bool includeInactive)
    {
        var targets = new List<SaveTarget>();
        foreach (var script in root.GetComponentsInChildren<SavableScript>(includeInactive))
            targets.Add(new SaveTarget(script, SavePhases.All));
        return targets;
    }

    /// <summary>
    /// What a game save visits: active savables under the root objects of the active scene (all
    /// phases except Special), and active savables under the game's extra root list (Special only).
    /// </summary>
    public static List<SaveTarget> WholeGame()
    {
        var targets = new List<SaveTarget>();
        foreach (var root in SceneManager.GetActiveScene().GetRootGameObjects())
            Collect(root, SavePhases.Scene, targets);

        var extra = SaveController.DontDestroyOnLoadObjects;
        if (extra != null)
        {
            foreach (var root in extra)
            {
                if (root != null && root.transform.parent == null)
                    Collect(root, SavePhases.Special, targets);
            }
        }
        return targets;
    }

    private static void Collect(GameObject root, SavePhases phases, List<SaveTarget> into)
    {
        if (root == null)
            return;
        foreach (var script in root.GetComponentsInChildren<SavableScript>(false))
        {
            if (script.gameObject.activeInHierarchy)
                into.Add(new SaveTarget(script, phases));
        }
    }
}
