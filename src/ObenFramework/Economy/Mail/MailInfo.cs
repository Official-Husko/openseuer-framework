using System;
using System.Collections.Generic;

namespace ObenFramework.Economy;

/// <summary>Where a piece of mail lands.</summary>
public enum MailKind
{
    /// <summary>The player's mailbox at home. Everything in it is taken out at once when the player opens it.</summary>
    Letter,

    /// <summary>The parcel locker. The game also sends a "Parcel Notification" letter when one arrives.</summary>
    Parcel,
}

/// <summary>Items that travel with a letter or parcel.</summary>
public readonly struct MailAttachment
{
    public MailAttachment(Item item, int amount = 1)
    {
        Item = item;
        Amount = amount;
    }

    public Item Item { get; }

    public int Amount { get; }
}

/// <summary>A piece of mail as the game holds it: delivered (in the mailbox or locker) or still on its way.</summary>
public sealed class MailInfo
{
    internal MailInfo(string title, MailKind kind, string text, IReadOnlyList<MailAttachment> attachments,
        IReadOnlyList<string> contents, int oc, float rm, int secondsLeft, bool isNote = false, string sender = null)
    {
        Title = title ?? "";
        IsNote = isNote;
        Sender = sender ?? "";
        Kind = kind;
        Text = text ?? "";
        Attachments = attachments ?? Array.Empty<MailAttachment>();
        Contents = contents ?? Array.Empty<string>();
        Oc = oc;
        Rm = rm;
        SecondsLeft = secondsLeft;
    }

    /// <summary>The name the game shows when the mail is taken.</summary>
    public string Title { get; }

    public MailKind Kind { get; }

    /// <summary>The written text, empty when the mail carries none.</summary>
    public string Text { get; }

    /// <summary>The items inside. Notes, recipes, furniture and task items are only in <see cref="Contents"/>.</summary>
    public IReadOnlyList<MailAttachment> Attachments { get; }

    /// <summary>Display names of everything inside, as the game lists them in its notification.</summary>
    public IReadOnlyList<string> Contents { get; }

    /// <summary>Open Sewer Coin inside.</summary>
    public int Oc { get; }

    /// <summary>Real Money inside.</summary>
    public float Rm { get; }

    /// <summary>Game seconds until delivery for mail on its way, 0 once delivered.</summary>
    public int SecondsLeft { get; }

    /// <summary>A written note or e-mail rather than goods; its text is <see cref="Text"/>, its title the one entry of <see cref="Contents"/>.</summary>
    public bool IsNote { get; }

    /// <summary>Who wrote it, for an e-mail sent as a note; empty otherwise.</summary>
    public string Sender { get; }

    public override string ToString() => Kind + " \"" + Title + "\"";
}
