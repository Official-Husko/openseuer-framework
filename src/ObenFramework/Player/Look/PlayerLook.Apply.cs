// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
using System;
using System.Collections.Generic;
using ObenFramework.Events;
using UnityEngine;

namespace ObenFramework.Player.Look;

public static partial class PlayerLook
{
    // Characters this class created. Only these are ever destroyed; the game's own assets never are.
    private static readonly HashSet<Character> Costumes = new HashSet<Character>();

    /// <summary>
    /// Dresses <paramref name="target"/> (the local player or a puppet's Appearance) in <paramref name="look"/>.
    /// The game builds a body from a <c>Character</c> asset, and the assets it hands out are shared: the
    /// character editor writes into one, and every NPC without a prefab of its own shares "Random NPC".
    /// So this gives the target a private Character of its own and never touches a shared one.
    /// </summary>
    /// <remarks>
    /// If the Appearance has not started yet (a puppet built inactive), the Character is just handed over
    /// and the game applies it in Start. Otherwise the body is rebuilt right away. Names the game does not
    /// know fall back to the slot's empty item, a missing head to the first head of that gender. Call
    /// <see cref="Release"/> when the puppet is destroyed to free its Character.
    /// </remarks>
    /// <returns>False when the look could not be applied (the error is logged).</returns>
    public static bool Apply(Appearance target, LookData look)
    {
        if (target == null)
            throw new ArgumentNullException(nameof(target));
        if (look == null)
            throw new ArgumentNullException(nameof(look));

        try
        {
            var costume = BuildCharacter(look);
            DiscardCostume(target.createdCharacter);
            target.createdCharacter = costume;
            Costumes.Add(costume);
            if (PlayerAppearances.IsStarted(target))
                target.LoadCharacter();
            return true;
        }
        catch (Exception e)
        {
            GameEvents.Log.Error("Applying a look failed", e);
            return false;
        }
    }

    /// <summary>Dresses the local player. Lasts until the level reloads; false without a player.</summary>
    public static bool ApplyToPlayer(LookData look)
    {
        var appearance = PlayerAppearance;
        return appearance != null && Apply(appearance, look);
    }

    /// <summary>
    /// Moves the body and face sliders of <paramref name="target"/> to <paramref name="look"/>'s values on the live
    /// body without rebuilding it, cheap enough for every frame while a slider is dragged. Clothes, hair, the head
    /// and gender stay as they are, and nothing is written into the target's Character, so <see cref="Capture(Appearance)"/>
    /// reads the previewed values but the next level load shows the look it really has. Call <see cref="Apply"/>
    /// to keep a look, or preview a look captured beforehand to go back. False before the body is built.
    /// </summary>
    public static bool Preview(Appearance target, LookData look)
    {
        if (target == null)
            throw new ArgumentNullException(nameof(target));
        if (look == null)
            throw new ArgumentNullException(nameof(look));
        if (!PlayerAppearances.IsStarted(target) || target.activeMeshes.Count == 0)
            return false;
        try
        {
            foreach (var dial in LookDials.All)
                dial.Live(target, dial.Clamp(dial.Get(look)));
            target.UpdateShapes();
            return true;
        }
        catch (Exception e)
        {
            GameEvents.Log.Error("Previewing a look failed", e);
            return false;
        }
    }

    /// <summary><see cref="Preview"/> on the local player; false without one.</summary>
    public static bool PreviewOnPlayer(LookData look)
    {
        var appearance = PlayerAppearance;
        return appearance != null && Preview(appearance, look);
    }

    /// <summary>Frees the private Character <see cref="Apply"/> gave to an Appearance. Does nothing for any other Character.</summary>
    public static void Release(Appearance target)
    {
        if (target != null)
            DiscardCostume(target.createdCharacter);
    }

    internal static void DestroyAllCostumes()
    {
        foreach (var costume in Costumes)
        {
            if (costume != null)
                UnityEngine.Object.Destroy(costume);
        }
        Costumes.Clear();
    }

    private static void DiscardCostume(Character previous)
    {
        if (previous != null && Costumes.Remove(previous))
            UnityEngine.Object.Destroy(previous);
        Costumes.RemoveWhere(c => c == null);
    }

    /// <summary>
    /// Writes a look into a <c>Character</c> the caller owns (a private copy, never one of the game's
    /// shared assets), the same way <see cref="Apply"/> fills its own. Unknown names fall back like there.
    /// </summary>
    public static void WriteTo(Character character, LookData look)
    {
        if (character == null)
            throw new ArgumentNullException(nameof(character));
        if (look == null)
            throw new ArgumentNullException(nameof(look));
        character.GenderString = look.male ? "Male" : "Female";
        character.Advanced = look.advanced;
        foreach (var dial in LookDials.All)
            dial.Write(character, dial.Clamp(dial.Get(look)));
        foreach (var slot in LookSlots.All)
        {
            var wearable = WearableTable.Find(slot, look.male, slot.Get(look));
            if (wearable == null && slot.Name == "head")
                wearable = WearableTable.First(slot, look.male);
            slot.Put(character, wearable);
        }
    }

    /// <summary>
    /// The Appearance ran its Start, so changing its Character needs <c>LoadCharacter()</c> to show. Before
    /// that the game applies whatever Character it holds when it starts.
    /// </summary>
    public static bool IsStarted(Appearance appearance) =>
        appearance != null ? PlayerAppearances.IsStarted(appearance) : throw new ArgumentNullException(nameof(appearance));

    private static Character BuildCharacter(LookData look)
    {
        var character = ScriptableObject.CreateInstance<Character>();
        character.name = "Mod Look";
        WriteTo(character, look);
        return character;
    }
}
