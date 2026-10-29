using System;
using System.Collections.Generic;
using ObenFramework.Events;
using ObenFramework.Items;
using ObenFramework.World;
using OS.Items;

namespace ObenFramework.Economy;

/// <summary>
/// The player's mail: send a letter or parcel through the game's <c>MailController</c>, read what is waiting
/// or on its way, and react when something arrives. Mail sent here is saved with the game like the game's own.
/// Call on the main thread while a world is loaded.
/// </summary>
public static class PlayerMail
{
    private const int MaxAmount = 1000;
    private const int MaxDelayMinutes = 525600;

    /// <summary>Mail reached the mailbox or the parcel locker, from any source: the game, a quest, a mod, or a delayed delivery. Not raised again for mail already waiting when a save loads.</summary>
    public static readonly WorldEvent<MailInfo> Received = new WorldEvent<MailInfo>(nameof(Received));

    /// <summary>True when the game's mail system exists (it does in every gameplay scene).</summary>
    public static bool IsAvailable => MailController.instance != null;

    /// <summary>
    /// Sends the player a letter, optionally with items and money attached. Returns false when there is no
    /// world to send into. Throws for an invalid request (no title, nothing to send, unknown item, bad amount).
    /// </summary>
    /// <param name="title">What the mail is called when it is taken.</param>
    /// <param name="text">The letter. It arrives as a readable paper item, the same way the game's own parcel notices do.</param>
    /// <param name="attachments">Items that come with it. Mod items work like any other.</param>
    /// <param name="kind">The mailbox, or the parcel locker for bigger deliveries.</param>
    /// <param name="delayMinutes">Game minutes until delivery. 0 delivers at once, and the mail is in the mailbox when this returns.</param>
    /// <param name="oc">Open Sewer Coin to put in.</param>
    /// <param name="rm">Real Money to put in (tenths).</param>
    public static bool Send(string title, string text = null, IEnumerable<MailAttachment> attachments = null,
        MailKind kind = MailKind.Letter, int delayMinutes = 0, int oc = 0, float rm = 0f)
    {
        if (string.IsNullOrWhiteSpace(title))
            throw new ArgumentException("A title is required.", nameof(title));
        if (delayMinutes < 0 || delayMinutes > MaxDelayMinutes)
            throw new ArgumentOutOfRangeException(nameof(delayMinutes), "0 to " + MaxDelayMinutes + ".");
        if (oc < 0 || rm < 0f || float.IsNaN(rm) || float.IsInfinity(rm))
            throw new ArgumentOutOfRangeException(nameof(oc), "Money cannot be negative.");

        var controller = MailController.instance;
        if (controller == null)
            return false;

        var bundle = new BundleMail { mailName = title, money = new Money.money(oc, Wallet.ToTenths(rm) / 10f) };
        AddAttachments(bundle, attachments);
        if (!string.IsNullOrEmpty(text) && !AddLetter(controller, bundle, title, text))
            return false;
        if (bundle.items.Count == 0 && oc == 0 && rm == 0f)
            throw new ArgumentException("There is nothing to send: give text, attachments or money.");

        controller.SendMail(bundle, kind == MailKind.Parcel ? MailController.Target.PlayerParcelLocker : MailController.Target.Player, DelayFor(delayMinutes));
        return true;
    }

    /// <summary>Convenience for one attached item.</summary>
    public static bool Send(string title, string text, Item item, int amount = 1) =>
        Send(title, text, new[] { new MailAttachment(item, amount) });

    /// <summary>Mail waiting to be taken out. Letters are in the mailbox, parcels in the locker.</summary>
    public static List<MailInfo> Inbox(MailKind kind = MailKind.Letter)
    {
        var result = new List<MailInfo>();
        var controller = MailController.instance;
        if (controller == null)
            return result;

        var source = kind == MailKind.Parcel ? controller.receivedPackages : controller.receivedMail;
        foreach (var mail in source)
        {
            if (mail != null)
                result.Add(MailDescriber.Describe(mail, kind));
        }
        return result;
    }

    /// <summary>Mail the player will receive later: delayed deliveries and the game's own in-transit mail.</summary>
    public static List<MailInfo> InTransit()
    {
        var result = new List<MailInfo>();
        var controller = MailController.instance;
        if (controller == null || controller.unprocessedMail == null)
            return result;

        foreach (var pair in controller.unprocessedMail)
        {
            if (pair.Key == null || pair.Value == null || !TryKindOf(controller, pair.Value, out var kind))
                continue;
            result.Add(MailDescriber.Describe(pair.Key, kind, pair.Value.remainingInSeconds));
        }
        return result;
    }

    private static void AddAttachments(BundleMail bundle, IEnumerable<MailAttachment> attachments)
    {
        if (attachments == null)
            return;
        foreach (var attachment in attachments)
        {
            var item = attachment.Item;
            if (item == null || PlayerItems.IsMoney(item.ID) || !ItemLookup.Exists(item.ID))
                throw new ArgumentException("Attachments must be existing items; send money with the oc and rm arguments.", nameof(attachments));
            if (attachment.Amount < 1 || attachment.Amount > MaxAmount)
                throw new ArgumentOutOfRangeException(nameof(attachments), "Each amount is 1 to " + MaxAmount + ".");

            bundle.items.Add(new BundleMail.MailItem
            {
                item = new ItemReference(item),
                amount = attachment.Amount,
                meta = item.CloneMeta(false, attachment.Amount, item),
            });
        }
    }

    // The game's parcel notice is a paper item whose text meta holds the message; letters reuse it.
    private static bool AddLetter(MailController controller, BundleMail bundle, string title, string text)
    {
        var card = controller.parcelCard != null ? controller.parcelCard.Item : null;
        if (card == null)
        {
            GameEvents.Log?.Warn("The game has no letter paper item (MailController.parcelCard); the letter was not sent.");
            return false;
        }

        var meta = card.CloneMeta(true);
        var data = ItemMetaUtilities.GetMetaOfType<ItemTextData>(meta);
        if (data == null)
        {
            GameEvents.Log?.Warn("The game's letter paper item has no text data; the letter was not sent.");
            return false;
        }

        string date = TimeOfDayAzure.instance != null ? TimeOfDayAzure.instance.currentTimeAndDay.CopyValues().ToDateTimeString() : "";
        data.content = title + "\n" + date + "\n\n" + text;
        bundle.items.Insert(0, new BundleMail.MailItem { item = new ItemReference(card), amount = 1, meta = meta });
        return true;
    }

    // Null makes the game's mail funnel deliver at once; a delay makes it queue a timer in game time.
    private static TimeAndDay DelayFor(int minutes) =>
        minutes <= 0 ? null : new TimeAndDay { hours = minutes / 60, minutes = minutes % 60 };

    private static bool TryKindOf(MailController controller, TimeOfDayAzure.Timer timer, out MailKind kind)
    {
        kind = MailKind.Letter;
        if (timer.Callback2 == null)
            return false;
        if (timer.Callback2 == (Action<TimeOfDayAzure.Timer>)controller.TimerSendMailPlayer)
            return true;
        kind = MailKind.Parcel;
        return timer.Callback2 == (Action<TimeOfDayAzure.Timer>)controller.TimerSendPackagePlayer;
    }

    internal static void RaiseReceived(MailController.Mail mail, MailKind kind)
    {
        if (Received.HasSubscribers)
            Received.Raise(MailDescriber.Describe(mail, kind));
    }

    /// <summary>
    /// Mail from the player reached someone in the world (a delivery a quest asked for, a paid invoice), at
    /// once or after its delay. The game's own mail listeners hear the same moment.
    /// </summary>
    public static readonly WorldEvent<SentMail> Sent = new WorldEvent<SentMail>(nameof(Sent));

    internal static void Init(ObenLoader.ObenMod framework)
    {
        MailController.MailSent += OnSent;
        framework.Track(ObenLoader.Runtime.Disposable.Create(() => MailController.MailSent -= OnSent));
    }

    // The game invokes this without a try/catch, from its mail timer and from SendMail.
    private static void OnSent(string name, NPC.NPCReference receiver, MailController.Mail mail)
    {
        if (!Sent.HasSubscribers)
            return;
        try
        {
            var items = mail as MailController.ItemMail;
            Sent.Raise(new SentMail(name ?? "", receiver != null ? receiver.ID : -1, receiver != null ? receiver.name ?? "" : "",
                items != null && items.item != null ? items.item.ID : 0, items != null ? items.amount : 0));
        }
        catch (Exception e)
        {
            WorldLog.Failed("Mail hook (sent)", e);
        }
    }
}

/// <summary>Mail the player sent arrived, from <see cref="PlayerMail.Sent"/>.</summary>
public readonly struct SentMail
{
    public SentMail(string name, int receiverId, string receiverName, int itemId, int amount)
    {
        Name = name;
        ReceiverId = receiverId;
        ReceiverName = receiverName;
        ItemId = itemId;
        Amount = amount;
    }

    /// <summary>The mail's name, which the game's listeners match on.</summary>
    public string Name { get; }

    /// <summary>The character it was addressed to (the character database id), or -1.</summary>
    public int ReceiverId { get; }

    public string ReceiverName { get; }

    /// <summary>The item it carried, or 0 for a letter without one.</summary>
    public int ItemId { get; }

    public int Amount { get; }
}
