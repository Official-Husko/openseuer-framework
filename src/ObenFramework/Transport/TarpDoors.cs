// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
using System;
using System.Collections.Generic;
using HarmonyLib;
using ObenFramework.Items;
using ObenFramework.Patching;
using ObenFramework.Saves;
using ObenFramework.World;

namespace ObenFramework.Transport;

/// <summary>A tarp door was drawn open or let down.</summary>
public readonly struct TarpDoorChange
{
    public TarpDoorChange(TarpDoor tarp, bool open, ChangeSource source)
    {
        Tarp = tarp;
        Open = open;
        Source = source;
    }

    public TarpDoor Tarp { get; }

    public bool Open { get; }

    public ChangeSource Source { get; }

    public string Id => TarpDoors.IdOf(Tarp);
}

/// <summary>
/// Tarp doors: the cloth curtains hung in doorways that the player pushes aside. Open or close them from code
/// and hear when they change. The game saves their state with the level.
/// </summary>
public static class TarpDoors
{
    internal static readonly AccessTools.FieldRef<TarpDoor, bool> OpenedField = AccessTools.FieldRefAccess<TarpDoor, bool>("opened");

    public static readonly WorldEvent<TarpDoorChange> Changed = new WorldEvent<TarpDoorChange>(nameof(Changed));

    /// <summary>True when the tarp door hook is in place, so <see cref="Changed"/> is raised.</summary>
    public static bool IsAvailable => PatchAreas.IsAvailable(PatchAreas.Framework, "Transport");

    public static List<TarpDoor> All() => ObjectIds.Active<TarpDoor>();

    public static TarpDoor Find(string id) => ObjectIds.Find<TarpDoor>(id);

    public static string IdOf(TarpDoor tarp) => ObjectIds.IdOf(tarp);

    public static bool IsOpen(TarpDoor tarp) => tarp != null && OpenedField(tarp);

    /// <summary>
    /// Opens or closes the tarp with its animation; nothing happens when it already is that way. False while a
    /// save is written or loaded.
    /// </summary>
    public static bool SetOpen(TarpDoor tarp, bool open)
    {
        if (tarp == null)
            throw new ArgumentNullException(nameof(tarp));
        if (OpenedField(tarp) == open)
            return true;
        if (SaveGate.IsBusy)
            return false;
        using (WorldChanges.EnterMod())
            tarp.Toggle(open);
        return OpenedField(tarp) == open;
    }
}

[HarmonyPatch(typeof(TarpDoor), nameof(TarpDoor.Toggle), typeof(bool))]
internal static class TarpDoorPatch
{
    [HarmonyPrefix]
    private static void Before(bool ___opened, out bool __state) => __state = ___opened;

    [HarmonyPostfix]
    private static void After(TarpDoor __instance, bool ___opened, bool __state)
    {
        try
        {
            if (__state != ___opened && TarpDoors.Changed.HasSubscribers && WorldChanges.Active)
                TarpDoors.Changed.Raise(new TarpDoorChange(__instance, ___opened, WorldChanges.Current));
        }
        catch (Exception e) { TransportModule.Log.Error("Tarp door hook", e); }
    }
}
