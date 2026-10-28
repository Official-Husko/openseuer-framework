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

    private static Character BuildCharacter(LookData look)
    {
        var character = ScriptableObject.CreateInstance<Character>();
        character.name = "Mod Look";
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
        return character;
    }
}
