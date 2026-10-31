// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
using System;
using System.Collections.Generic;
using HarmonyLib;
using ObenFramework.Economy;
using ObenFramework.Items;
using ObenFramework.Patching;
using ObenFramework.Saves;
using ObenFramework.World;

namespace ObenFramework.Housing;

/// <summary>One compartment of a parcel locker.</summary>
public sealed class LockerSlotInfo
{
    internal LockerSlotInfo(int index, bool open, MailInfo parcel)
    {
        Index = index;
        Open = open;
        Parcel = parcel;
    }

    public int Index { get; }

    /// <summary>The hatch is open (a parcel was put out and waits to be taken).</summary>
    public bool Open { get; }

    /// <summary>The parcel in it, or null when it is empty.</summary>
    public MailInfo Parcel { get; }
}

/// <summary>A snapshot of one parcel locker.</summary>
public sealed class ParcelLockerInfo
{
    internal ParcelLockerInfo(string id, string name, List<LockerSlotInfo> slots)
    {
        Id = id;
        Name = name;
        Slots = slots;
    }

    public string Id { get; }

    public string Name { get; }

    public List<LockerSlotInfo> Slots { get; }
}

public enum LockerUseKind
{
    /// <summary>Waiting parcels were put into compartments.</summary>
    Claimed,

    /// <summary>A parcel was taken out of its compartment.</summary>
    Taken,
}

/// <summary>A parcel locker was used, as raised by <see cref="ParcelLockers.Used"/>.</summary>
public readonly struct LockerUse
{
    public LockerUse(ParcelLocker locker, LockerUseKind kind, int count, int index, MailInfo parcel, ChangeSource source)
    {
        Locker = locker;
        Kind = kind;
        Count = count;
        Index = index;
        Parcel = parcel;
        Source = source;
    }

    public ParcelLocker Locker { get; }

    public LockerUseKind Kind { get; }

    /// <summary>Parcels put into compartments (claimed), or 1 (taken).</summary>
    public int Count { get; }

    /// <summary>The compartment a parcel was taken from; -1 for a claim.</summary>
    public int Index { get; }

    /// <summary>The parcel taken; null for a claim.</summary>
    public MailInfo Parcel { get; }

    /// <summary><see cref="ChangeSource.LocalPlayer"/> when the player used the locker, <see cref="ChangeSource.Mod"/> for the framework's calls.</summary>
    public ChangeSource Source { get; }

    public string Id => ObjectIds.IdOf(Locker);
}

/// <summary>
/// Parcel lockers: parcels sent to the player wait in the mail system until the player opens the locker,
/// which puts each into a compartment to be taken. Waiting parcels are <see cref="PlayerMail.Inbox"/> with
/// <see cref="MailKind.Parcel"/>. Refused while the game saves.
/// </summary>
public static class ParcelLockers
{
    /// <summary>Parcels were claimed into compartments or one was taken out. Not raised for an empty use.</summary>
    public static readonly WorldEvent<LockerUse> Used = new WorldEvent<LockerUse>(nameof(Used));

    /// <summary>The hooks behind <see cref="Used"/> and the quiet take are in place (false when a game update broke them).</summary>
    public static bool IsAvailable => PatchAreas.IsAvailable(PatchAreas.Framework, "Housing");

    private static readonly AccessTools.FieldRef<ParcelLocker.Locker, bool> IsOpen = AccessTools.FieldRefAccess<ParcelLocker.Locker, bool>("isOpen");
    private static readonly AccessTools.FieldRef<ParcelLocker.Locker, MailController.Mail> Parcel =
        AccessTools.FieldRefAccess<ParcelLocker.Locker, MailController.Mail>("currentParcel");

    public static List<ParcelLockerInfo> All()
    {
        var list = new List<ParcelLockerInfo>();
        foreach (var locker in ObjectIds.Active<ParcelLocker>())
            list.Add(Describe(locker));
        return list;
    }

    public static ParcelLocker Find(string id) => ObjectIds.Find<ParcelLocker>(id);

    public static ParcelLockerInfo Describe(ParcelLocker locker)
    {
        var slots = new List<LockerSlotInfo>();
        if (locker.lockers != null)
        {
            for (int i = 0; i < locker.lockers.Length; i++)
            {
                var slot = locker.lockers[i];
                slots.Add(new LockerSlotInfo(i, slot != null && IsOpen(slot), ParcelIn(slot)));
            }
        }
        return new ParcelLockerInfo(ObjectIds.IdOf(locker), locker.name, slots);
    }

    /// <summary>
    /// Opens the locker like the player does: each waiting parcel goes into a free compartment it fits; the
    /// rest keep waiting when the locker is full. Returns how many parcels came out; 0 when none were waiting,
    /// or while the game saves.
    /// </summary>
    public static int Open(ParcelLocker locker)
    {
        if (locker == null)
            throw new ArgumentNullException(nameof(locker));
        if (SaveGate.IsBusy || !Mailboxes.MailReady())
            return 0;
        int before = MailController.instance.GetPackageAmount();
        if (before == 0)
            return 0;
        using (WorldChanges.EnterMod())
            locker.Interact();
        return before - MailController.instance.GetPackageAmount();
    }

    /// <summary>Takes the parcel out of one compartment into the player's hands, like picking it up. False when it is empty, or while the game saves.</summary>
    public static bool TakeParcel(ParcelLocker locker, int index) => TakeParcel(locker, index, true);

    /// <summary>As <see cref="TakeParcel(ParcelLocker, int)"/>; with <paramref name="notify"/> false the game shows no toast for the contents.</summary>
    public static bool TakeParcel(ParcelLocker locker, int index, bool notify)
    {
        if (locker == null)
            throw new ArgumentNullException(nameof(locker));
        if (locker.lockers == null || index < 0 || index >= locker.lockers.Length)
            throw new ArgumentOutOfRangeException(nameof(index));
        var slot = locker.lockers[index];
        if (SaveGate.IsBusy || slot == null || Parcel(slot) == null || !Mailboxes.MailReady())
            return false;
        using (WorldChanges.EnterMod())
        using (MailPatches.Quiet(!notify))
            locker.TakeParcelFromLocker(index);
        return Parcel(slot) == null;
    }

    internal static MailInfo ParcelIn(ParcelLocker.Locker slot)
    {
        var parcel = slot != null ? Parcel(slot) : null;
        return parcel != null ? MailDescriber.Describe(parcel, MailKind.Parcel) : null;
    }
}
