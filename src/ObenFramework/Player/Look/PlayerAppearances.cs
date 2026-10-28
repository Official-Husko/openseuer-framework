using HarmonyLib;
using UnityEngine;

namespace ObenFramework.Player.Look;

/// <summary>Finds the player's Appearance and tells whether an Appearance has been through its Start.</summary>
internal static class PlayerAppearances
{
    // Private in the game; set by Appearance.Start, so null means the Appearance hasn't started yet.
    private static readonly AccessTools.FieldRef<Appearance, SkinnedMeshRenderer[]> BodyModels = FindBodyModels();

    private static PlayerLocator _locator;
    private static Appearance _cached;

    public static Appearance Resolve()
    {
        var locator = PlayerLocator.instance;
        if (locator == null)
            return null;
        if (_cached != null && _locator == locator)
            return _cached;

        _locator = locator;
        _cached = Find(locator);
        return _cached;
    }

    /// <summary>True when <paramref name="appearance"/> is the player's body.</summary>
    public static bool IsPlayer(Appearance appearance)
    {
        var player = Resolve();
        return player != null && player == appearance;
    }

    /// <summary>
    /// The player's own look as the character editor leaves it: the shared "Created Character" asset. The
    /// editor's preview body uses it, so edits made at the main menu count as the player's look.
    /// </summary>
    public static bool UsesCreatedCharacter(Appearance appearance) =>
        appearance != null && appearance.createdCharacter != null && appearance.createdCharacter.name == "Created Character";

    /// <summary>The Appearance ran Start, so it has its body meshes and <c>LoadCharacter</c> will work.</summary>
    public static bool IsStarted(Appearance appearance) => BodyModels == null || BodyModels(appearance) != null;

    public static void Forget()
    {
        _locator = null;
        _cached = null;
    }

    private static Appearance Find(PlayerLocator locator)
    {
        var field = locator.playerAppearance;
        if (field != null && !IsFirstPerson(field))
            return field;

        foreach (var candidate in locator.GetComponentsInChildren<Appearance>(true))
        {
            if (candidate != null && !IsFirstPerson(candidate))
                return candidate;
        }
        return null;
    }

    private static bool IsFirstPerson(Appearance appearance) =>
        appearance.GetComponentInParent<FirstPersonHands>() != null;

    private static AccessTools.FieldRef<Appearance, SkinnedMeshRenderer[]> FindBodyModels()
    {
        try
        {
            return AccessTools.FieldRefAccess<Appearance, SkinnedMeshRenderer[]>("bodyModels");
        }
        catch (System.Exception)
        {
            return null;
        }
    }
}
