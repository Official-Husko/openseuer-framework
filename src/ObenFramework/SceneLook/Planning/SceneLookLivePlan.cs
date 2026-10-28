// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
using System.Collections.Generic;

namespace ObenFramework.SceneLook;

/// <summary>What to do with an NPC spawned to wear a look.</summary>
internal enum SceneLookLiveAction
{
    /// <summary>Nothing was spawned.</summary>
    Refuse,

    /// <summary>It was spawned but the look didn't go on: take it away again.</summary>
    Despawn,

    /// <summary>It wears the look: keep it.</summary>
    Keep,
}

/// <summary>
/// The decisions of re-skinning a live NPC, on instance ids so they can be tested: whether to keep a spawn,
/// which of the NPC's own renderer objects to keep hidden, and which new child is the look's holder.
/// </summary>
internal static class SceneLookLivePlan
{
    public static SceneLookLiveAction Spawn(bool spawned, bool applied)
    {
        if (!spawned)
            return SceneLookLiveAction.Refuse;
        return applied ? SceneLookLiveAction.Keep : SceneLookLiveAction.Despawn;
    }

    /// <summary>Indexes into <paramref name="hidden"/> of the objects that still exist.</summary>
    public static List<int> Watch(List<int> hidden, List<int> now)
    {
        var watch = new List<int>(hidden.Count);
        for (int i = 0; i < hidden.Count; i++)
        {
            if (now.Contains(hidden[i]))
                watch.Add(i);
        }
        return watch;
    }

    /// <summary>The one child that appeared between <paramref name="before"/> and <paramref name="after"/>, or 0.</summary>
    public static int Holder(List<int> before, List<int> after)
    {
        int found = 0;
        int count = 0;
        foreach (int id in after)
        {
            if (before.Contains(id))
                continue;
            found = id;
            count++;
        }
        return count == 1 ? found : 0;
    }
}
