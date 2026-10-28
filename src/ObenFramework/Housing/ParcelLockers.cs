// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
using System;
using System.Collections.Generic;
using HarmonyLib;
using ObenFramework.Economy;
using ObenFramework.Items;
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

/// <summary>
/// Parcel lockers: parcels sent to the player wait in the mail system until the player opens the locker,
/// which puts each into a compartment to be taken. Waiting parcels are <see cref="PlayerMail.Inbox"/> with
/// <see cref="MailKind.Parcel"/>.
/// </summary>
public static class ParcelLockers
{
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
                var parcel = slot != null ? Parcel(slot) : null;
                slots.Add(new LockerSlotInfo(i, slot != null && IsOpen(slot), parcel != null ? MailDescriber.Describe(parcel, MailKind.Parcel) : null));
            }
        }
        return new ParcelLockerInfo(ObjectIds.IdOf(locker), locker.name, slots);
    }

    /// <summary>
    /// Opens the locker like the player does: every waiting parcel is put into a compartment. Returns how
    /// many parcels came out; 0 when none were waiting.
    /// </summary>
    public static int Open(ParcelLocker locker)
    {
        if (locker == null)
            throw new ArgumentNullException(nameof(locker));
        if (!Mailboxes.MailReady())
            return 0;
        int before = MailController.instance.GetPackageAmount();
        if (before == 0)
            return 0;
        using (WorldChanges.EnterMod())
            locker.Interact();
        return before - MailController.instance.GetPackageAmount();
    }

    /// <summary>Takes the parcel out of one compartment into the player's hands, like picking it up. False when it is empty.</summary>
    public static bool TakeParcel(ParcelLocker locker, int index)
    {
        if (locker == null)
            throw new ArgumentNullException(nameof(locker));
        if (locker.lockers == null || index < 0 || index >= locker.lockers.Length)
            throw new ArgumentOutOfRangeException(nameof(index));
        var slot = locker.lockers[index];
        if (slot == null || Parcel(slot) == null || !Mailboxes.MailReady())
            return false;
        using (WorldChanges.EnterMod())
            locker.TakeParcelFromLocker(index);
        return Parcel(slot) == null;
    }
}
