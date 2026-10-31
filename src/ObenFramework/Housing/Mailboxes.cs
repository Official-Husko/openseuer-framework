// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
using System;
using System.Collections.Generic;
using ObenFramework.Items;
using ObenFramework.Patching;
using ObenFramework.Saves;
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

/// <summary>Letters were taken out of a mailbox or mail dropped in a post box, as raised by <see cref="Mailboxes.Used"/>.</summary>
public readonly struct MailboxUse
{
    public MailboxUse(Mailbox mailbox, MailboxMode mode, int count)
        : this(mailbox, mode, count, ChangeSource.LocalPlayer)
    {
    }

    public MailboxUse(Mailbox mailbox, MailboxMode mode, int count, ChangeSource source)
    {
        Mailbox = mailbox;
        Mode = mode;
        Count = count;
        Source = source;
    }

    public Mailbox Mailbox { get; }

    public MailboxMode Mode { get; }

    /// <summary>How many letters were taken or pieces dropped.</summary>
    public int Count { get; }

    /// <summary><see cref="ChangeSource.LocalPlayer"/> when the player used the box, <see cref="ChangeSource.Mod"/> for the framework's calls.</summary>
    public ChangeSource Source { get; }

    public string Id => ObjectIds.IdOf(Mailbox);
}

/// <summary>
/// Mailboxes: the player's own (receive) and post boxes (send). What is in the player's mail is
/// <see cref="Economy.PlayerMail"/>; this is about the boxes in the world. Refused while the game saves.
/// </summary>
public static class Mailboxes
{
    /// <summary>Letters were taken out of a mailbox or mail dropped in a post box, by the player or a mod. Not raised for an empty use.</summary>
    public static readonly WorldEvent<MailboxUse> Used = new WorldEvent<MailboxUse>(nameof(Used));

    /// <summary>The hooks behind <see cref="Used"/> and the quiet takes are in place (false when a game update broke them).</summary>
    public static bool IsAvailable => PatchAreas.IsAvailable(PatchAreas.Framework, "Housing");

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
    /// taken; 0 while the game saves. Throws for a post box.
    /// </summary>
    public static int TakeMail(Mailbox mailbox) => TakeMail(mailbox, true);

    /// <summary>As <see cref="TakeMail(Mailbox)"/>; with <paramref name="notify"/> false the game shows no toast per letter.</summary>
    public static int TakeMail(Mailbox mailbox, bool notify)
    {
        if (mailbox == null)
            throw new ArgumentNullException(nameof(mailbox));
        if (mailbox.mode != Mailbox.Mode.Receive)
            throw new InvalidOperationException("That is a post box; mail is only taken from the player's own box.");
        if (SaveGate.IsBusy || !MailReady())
            return 0;
        int count = MailController.instance.GetMailAmount();
        if (count == 0)
            return 0;
        using (WorldChanges.EnterMod())
        using (MailPatches.Quiet(!notify))
            mailbox.TakeMail();
        return count - MailController.instance.GetMailAmount();
    }

    /// <summary>
    /// Drops every letter the player carries into a post box, like using it: they go out into the world (to
    /// their addressee, or the story's listeners). Returns how many pieces went in; 0 while the game saves.
    /// Throws for the player's own box.
    /// </summary>
    public static int Post(Mailbox mailbox)
    {
        if (mailbox == null)
            throw new ArgumentNullException(nameof(mailbox));
        if (mailbox.mode != Mailbox.Mode.Send)
            throw new InvalidOperationException("That is the player's own box; mail is posted in a post box.");
        if (SaveGate.IsBusy || !MailReady())
            return 0;
        int count = CountOf(mailbox);
        if (count == 0)
            return 0;
        using (WorldChanges.EnterMod())
            mailbox.InsertMail();
        return count - CountOf(mailbox);
    }

    internal static MailboxMode ModeOf(Mailbox mailbox) => mailbox.mode == Mailbox.Mode.Send ? MailboxMode.Send : MailboxMode.Receive;

    internal static int CountOf(Mailbox mailbox)
    {
        if (mailbox.mode == Mailbox.Mode.Send)
        {
            if (Inventory.instance == null || TaskItemsManager.instance == null)
                return 0;
            // A post box skips mailable packages and leaves them in the inventory.
            int letters = 0;
            var slots = Inventory.instance.GetAllSlotsWithItemWithMeta<ItemMailableData>();
            if (slots != null)
            {
                foreach (var slot in slots)
                {
                    var mailable = slot.itemStack != null ? slot.itemStack.GetMetaOfType<ItemMailableData>() : null;
                    if (mailable != null && !mailable.isPackage)
                        letters++;
                }
            }
            return letters + TaskItemsManager.instance.MailCount();
        }
        return MailController.instance != null ? MailController.instance.GetMailAmount() : 0;
    }

    /// <summary>Taking mail can hand out items, notes, recipes, money, furniture and tasks; all of those have to exist.</summary>
    internal static bool MailReady() =>
        MailController.instance != null && Inventory.instance != null && Notifications.instance != null && TaskItemsManager.instance != null
        && NoteController.instance != null && RecipeController.instance != null && Money.instance != null && BuildingSystem.instance != null
        && TaskController.instance != null && PlayerLocator.instance != null;
}
