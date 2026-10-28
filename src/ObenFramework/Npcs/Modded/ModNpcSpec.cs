using System;
using ObenFramework.Player.Look;
using UnityEngine;

namespace ObenFramework.Npcs;

/// <summary>What a mod NPC is made of (see <see cref="ModNpcs.Spawn"/>).</summary>
public sealed class ModNpcSpec
{
    /// <summary>
    /// Character id of the game NPC to copy: its body, voice, animations and default look. Any NPC the game
    /// has a character for works (see <c>npcs.list</c>), whether or not it is in the scene. The player
    /// (character 0) and other mod NPCs cannot be templates.
    /// </summary>
    public int Template;

    /// <summary>
    /// Character id whose body (prefab) to use instead of the template's, or 0 for the template's own. A
    /// template with no prefab of its own gets the first NPC body with character editor sliders.
    /// </summary>
    public int Body;

    /// <summary>First name shown when the player looks at or talks to it. Required.</summary>
    public string FirstName;

    /// <summary>Last name, or empty.</summary>
    public string LastName = string.Empty;

    /// <summary>Where it stands. It is put on the ground below (within a few metres) and moved aside if something is in the way.</summary>
    public Vector3 Position;

    /// <summary>Which way it faces, in degrees around the vertical axis (0 is north/+Z).</summary>
    public float Yaw;

    /// <summary>
    /// Keep the template's conversation, so talking to the copy starts the template's dialogue. Off by default:
    /// the copy cannot be talked to (start conversations yourself with <see cref="NpcActions.StartConversation"/>).
    /// Ignored when <see cref="Body"/> is another character's.
    /// </summary>
    public bool KeepDialogue;

    /// <summary>
    /// Keep the NPC in the save: it comes back in this scene every time the scene loads, while your mod is
    /// installed, with the position and look it had when the game was saved. Without it the NPC lasts until
    /// the level changes.
    /// </summary>
    public bool Saved;

    /// <summary>
    /// A look for bodies with character editor sliders (sliders and worn items; see <c>PlayerLook</c>).
    /// Null keeps the template's.
    /// </summary>
    public LookData Look;

    internal void Validate(string name)
    {
        if (!ModNpcIds.ValidName(name))
            throw new ArgumentException("An NPC name must be 1 to 64 characters without '/'.", nameof(name));
        if (string.IsNullOrEmpty(FirstName))
            throw new ArgumentException("The NPC needs a first name.", nameof(FirstName));
        if (Template <= 0)
            throw new ArgumentException("Template must be the character id of a game NPC.", nameof(Template));
        if (Body < 0)
            throw new ArgumentException("Body must be a character id or 0.", nameof(Body));
        if (!NpcWalkMath.Finite(Position.x) || !NpcWalkMath.Finite(Position.y) || !NpcWalkMath.Finite(Position.z) || !NpcWalkMath.Finite(Yaw))
            throw new ArgumentException("Position and Yaw must be finite numbers.", nameof(Position));
    }
}
