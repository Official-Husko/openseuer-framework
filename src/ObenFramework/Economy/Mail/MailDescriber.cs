using System.Collections.Generic;
using OS.Items;

namespace ObenFramework.Economy;

/// <summary>Reads the game's mail classes into <see cref="MailInfo"/>.</summary>
internal static class MailDescriber
{
    internal static MailInfo Describe(MailController.Mail mail, MailKind kind, int secondsLeft = 0)
    {
        var attachments = new List<MailAttachment>();
        string text = "";
        int oc = 0;
        float rm = 0f;
        List<string> contents = null;

        switch (mail)
        {
            case BundleMail bundle:
                foreach (var line in bundle.items)
                    Collect(line.item, line.amount, line.meta, attachments, ref text);
                oc = bundle.money.OC;
                rm = bundle.money.RM;
                contents = bundle.GetContentNames();
                break;
            case MailController.ItemMail itemMail:
                Collect(itemMail.item, itemMail.amount, itemMail.meta, attachments, ref text);
                break;
            case MailController.MoneyMail moneyMail:
                oc = moneyMail.money.OC;
                rm = moneyMail.money.RM;
                break;
            case MailController.NoteMail noteMail when noteMail.noteInfo != null:
                text = noteMail.noteInfo.content ?? "";
                contents = new List<string> { noteMail.noteInfo.title };
                break;
        }

        if (contents == null)
        {
            contents = new List<string>();
            foreach (var attachment in attachments)
                contents.Add(attachment.Item.Title);
            if (oc != 0 || rm != 0f)
                contents.Add(oc != 0 ? oc + " OC" : rm + " RM");
        }
        return new MailInfo(mail.mailName, kind, text, attachments, contents, oc, rm, secondsLeft);
    }

    // The letter text rides on the game's parcel card item (a paper with text meta); it is not an attachment.
    private static void Collect(ItemReference reference, int amount, object[] meta, List<MailAttachment> into, ref string text)
    {
        var item = reference != null ? reference.Item : null;
        if (item == null || amount <= 0)
            return;

        var controller = MailController.instance;
        var card = controller != null && controller.parcelCard != null ? controller.parcelCard.Item : null;
        if (card != null && item.ID == card.ID)
        {
            var data = ItemMetaUtilities.GetMetaOfType<ItemTextData>(meta);
            if (data != null)
            {
                text = data.content ?? "";
                return;
            }
        }
        into.Add(new MailAttachment(item, amount));
    }
}
