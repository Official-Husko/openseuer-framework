using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using Object = UnityEngine.Object;

namespace ObenFramework.Npcs;

// What an extra keeps of the body it copies: the look and the Animator, nothing that acts.
public static partial class NpcExtras
{
    private const int MaxStripPasses = 8;

    // Everything but the look builders goes, so nothing of the NPC (its AI, talk, barks, save ids, story
    // wiring, distance culling) runs. A component another one requires goes in a later pass, after that one
    // (Unity refuses, with an error in the log, to remove it first).
    private static bool Strip(GameObject go)
    {
        for (int pass = 0; pass < MaxStripPasses; pass++)
        {
            var parts = Removable(go);
            if (parts.Count == 0)
                return true;
            var needed = new List<(GameObject, Type)>();
            foreach (var part in parts)
            {
                foreach (var type in Required(part.GetType()))
                    needed.Add((part.gameObject, type));
            }
            foreach (var part in parts)
            {
                if (!needed.Exists(n => n.Item1 == part.gameObject && n.Item2.IsInstanceOfType(part)))
                    Object.DestroyImmediate(part);
            }
        }
        return Removable(go).Count == 0;
    }

    private static List<MonoBehaviour> Removable(GameObject go)
    {
        var list = new List<MonoBehaviour>();
        foreach (var part in go.GetComponentsInChildren<MonoBehaviour>(true))
        {
            if (part != null && !(part is Appearance) && !(part is CharacterAppearance))
                list.Add(part);
        }
        return list;
    }

    private static readonly Dictionary<Type, List<Type>> RequiredCache = new Dictionary<Type, List<Type>>();

    private static List<Type> Required(Type type)
    {
        if (!RequiredCache.TryGetValue(type, out var list))
        {
            list = new List<Type>();
            foreach (RequireComponent attribute in type.GetCustomAttributes(typeof(RequireComponent), true))
            {
                foreach (var required in new[] { attribute.m_Type0, attribute.m_Type1, attribute.m_Type2 })
                {
                    if (required != null)
                        list.Add(required);
                }
            }
            RequiredCache[type] = list;
        }
        return list;
    }

    // What is left must not move the body or fire the NPC's animation events at scripts that are gone.
    private static void Quiet(GameObject go)
    {
        foreach (var animator in go.GetComponentsInChildren<Animator>(true))
        {
            animator.fireEvents = false;
            animator.applyRootMotion = false;
        }
        foreach (var agent in go.GetComponentsInChildren<NavMeshAgent>(true))
            agent.enabled = false;
        foreach (var body in go.GetComponentsInChildren<Rigidbody>(true))
            body.isKinematic = true;
    }
}
