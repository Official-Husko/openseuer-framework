// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
using System;
using System.Collections.Generic;
using HarmonyLib;
using ObenFramework.Items;
using ObenFramework.World;
using OS.Items;

namespace ObenFramework.Housing;

public enum MailboxMode
{
    /// <summary>The player's own box: letters arrive here.</summary>
    Receive,

    /// <summary>A post box: the player drops outgoing mail here.</summary>
    Send,
}

/// <summary>A snapshot of one mailbox.</summary>
public sealed class MailboxInfo
{
    internal MailboxInfo(string id, string name, MailboxMode mode, int count)
    {
        Id = id;
        Name = name;
        Mode = mode;
        Count = count;
    }

    public string Id { get; }

    public string Name { get; }

    public MailboxMode Mode { get; }

    /// <summary>Letters waiting (receive), or pieces of mail the player carries that can be dropped (send).</summary>
    public int Count { get; }
}

/// <summary>The player used a mailbox with something in it or to drop, as raised by <see cref="Mailboxes.Used"/>.</summary>
public readonly struct MailboxUse
{
    public MailboxUse(Mailbox mailbox, MailboxMode mode, int count)
    {
        Mailbox = mailbox;
        Mode = mode;
        Count = count;
    }

    public Mailbox Mailbox { get; }

    public MailboxMode Mode { get; }

    /// <summary>How many letters were taken or pieces dropped.</summary>
    public int Count { get; }

    public string Id => ObjectIds.IdOf(Mailbox);
}

/// <summary>
/// Mailboxes: the player's own (receive) and post boxes (send). What is in the player's mail is
/// <see cref="Economy.PlayerMail"/>; this is about the boxes in the world.
/// </summary>
public static class Mailboxes
{
    /// <summary>The player took letters out of a mailbox or dropped mail in a post box. Not raised for an empty use.</summary>
    public static readonly WorldEvent<MailboxUse> Used = new WorldEvent<MailboxUse>(nameof(Used));

    public static List<MailboxInfo> All()
    {
        var list = new List<MailboxInfo>();
        foreach (var mailbox in ObjectIds.Active<Mailbox>())
            list.Add(Describe(mailbox));
        return list;
    }

    public static Mailbox Find(string id) => ObjectIds.Find<Mailbox>(id);

    public static MailboxInfo Describe(Mailbox mailbox) => new MailboxInfo(ObjectIds.IdOf(mailbox), mailbox.name, ModeOf(mailbox), CountOf(mailbox));

    /// <summary>
    /// Takes every waiting letter into the player's inventory, like opening the box. Returns how many were
    /// taken. Throws for a post box.
    /// </summary>
    public static int TakeMail(Mailbox mailbox)
    {
        if (mailbox == null)
            throw new ArgumentNullException(nameof(mailbox));
        if (mailbox.mode != Mailbox.Mode.Receive)
            throw new InvalidOperationException("That is a post box; mail is only taken from the player's own box.");
        if (!MailReady())
            return 0;
        int count = MailController.instance.GetMailAmount();
        if (count == 0)
            return 0;
        using (WorldChanges.EnterMod())
            mailbox.TakeMail();
        return count - MailController.instance.GetMailAmount();
    }

    internal static MailboxMode ModeOf(Mailbox mailbox) => mailbox.mode == Mailbox.Mode.Send ? MailboxMode.Send : MailboxMode.Receive;

    internal static int CountOf(Mailbox mailbox)
    {
        if (mailbox.mode == Mailbox.Mode.Send)
        {
            if (Inventory.instance == null || TaskItemsManager.instance == null)
                return 0;
            var slots = Inventory.instance.GetAllSlotsWithItemWithMeta<ItemMailableData>();
            return (slots != null ? slots.Count : 0) + TaskItemsManager.instance.MailCount();
        }
        return MailController.instance != null ? MailController.instance.GetMailAmount() : 0;
    }

    /// <summary>Taking mail can hand out items, notes, recipes, money, furniture and tasks; all of those have to exist.</summary>
    internal static bool MailReady() =>
        MailController.instance != null && Inventory.instance != null && Notifications.instance != null && TaskItemsManager.instance != null
        && NoteController.instance != null && RecipeController.instance != null && Money.instance != null && BuildingSystem.instance != null
        && TaskController.instance != null && PlayerLocator.instance != null;
}

[HarmonyPatch(typeof(Mailbox), nameof(Mailbox.Interact))]
internal static class MailboxUsePatch
{
    [HarmonyPrefix]
    private static void Before(Mailbox __instance, out int __state)
    {
        __state = 0;
        if (!Mailboxes.Used.HasSubscribers)
            return;
        try
        {
            __state = Mailboxes.CountOf(__instance);
        }
        catch (Exception e)
        {
            WorldLog.Failed("Mailbox hook", e);
        }
    }

    [HarmonyPostfix]
    private static void After(Mailbox __instance, int __state)
    {
        if (__state <= 0 || !WorldChanges.Active)
            return;
        try
        {
            Mailboxes.Used.Raise(new MailboxUse(__instance, Mailboxes.ModeOf(__instance), __state));
        }
        catch (Exception e)
        {
            WorldLog.Failed("Mailbox hook", e);
        }
    }
}
