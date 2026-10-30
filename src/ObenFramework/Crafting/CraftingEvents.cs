// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
using ObenFramework.World;

namespace ObenFramework.Crafting;

/// <summary>
/// Crafting at any station: the player's own crafts and stations that run by themselves (stills, grow
/// boxes, autocrafting machines). Subscribe with an owner (<c>CraftingEvents.Crafted.Subscribe(this, ...)</c>).
/// The hooks return at once while nobody listens.
/// </summary>
public static class CraftingEvents
{
    /// <summary>
    /// The framework's crafting hooks are in place. False when they failed to apply (a game update changed what
    /// they patch); the events then stay quiet and what this class changes may not take.
    /// </summary>
    public static bool IsAvailable => CraftingModule.HooksInPlace;

    /// <summary>A station took the ingredients and started a recipe.</summary>
    public static readonly WorldEvent<CraftStart> CraftStarted = new WorldEvent<CraftStart>(nameof(CraftStarted));

    /// <summary>A recipe finished and its output (or fail output) was created.</summary>
    public static readonly WorldEvent<CraftDone> Crafted = new WorldEvent<CraftDone>(nameof(Crafted));
}
