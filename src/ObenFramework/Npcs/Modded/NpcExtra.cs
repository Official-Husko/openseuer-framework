using UnityEngine;

namespace ObenFramework.Npcs;

/// <summary>
/// A body standing in the scene for a mod (see <see cref="NpcExtras"/>): a copy of a game character's look
/// with nothing running on it. Its <see cref="Animator"/> still plays the body's idle and takes
/// <see cref="NpcAnimation.Play"/> for a pose or a loop.
/// </summary>
public sealed class NpcExtra
{
    internal NpcExtra(string owner, string name, int template, int body, Character clone, GameObject root)
    {
        Owner = owner;
        Name = name;
        Template = template;
        Body = body;
        Clone = clone;
        Root = root;
    }

    internal Character Clone { get; }

    /// <summary>The mod that spawned it.</summary>
    public string Owner { get; }

    /// <summary>The name the mod gave it.</summary>
    public string Name { get; }

    /// <summary>The game character whose look it has.</summary>
    public int Template { get; }

    /// <summary>The game character whose body it uses.</summary>
    public int Body { get; }

    /// <summary>The body's object, or null once it is gone. Add your own components to it if you like.</summary>
    public GameObject Root { get; private set; }

    /// <summary>False once it was despawned, its mod unloaded or the level changed.</summary>
    public bool Loaded => Root != null;

    public Vector3 Position => Loaded ? Root.transform.position : Vector3.zero;

    /// <summary>The body's Animator, for <see cref="NpcAnimation.Play"/>; null when the body has none.</summary>
    public Animator Animator => Loaded ? Root.GetComponentInChildren<Animator>() : null;

    /// <summary>Puts it elsewhere in the scene, facing <paramref name="yaw"/>. No ground check: it stands where it is put.</summary>
    public bool Teleport(Vector3 position, float yaw)
    {
        if (!Loaded)
            return false;
        Root.transform.SetPositionAndRotation(position, Quaternion.Euler(0f, yaw, 0f));
        return true;
    }

    public bool Despawn() => NpcExtras.Despawn(Owner, Name);

    internal void Destroy()
    {
        if (Root != null)
        {
            Root.SetActive(false);
            Object.Destroy(Root);
        }
        Root = null;
        if (Clone != null)
            Object.Destroy(Clone);
    }

    public override string ToString() => $"extra {Owner}/{Name} (look {Template}, body {Body})";
}
