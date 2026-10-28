using System;
using System.Linq;
using DevBridge.Server;
using Newtonsoft.Json.Linq;
using ObenFramework.Economy;
using ObenFramework.Items;

namespace DevBridge.Commands;

/// <summary>List shops and what they sell, and send the player mail.</summary>
internal static class ShopCommands
{
    [BridgeCommand("shops.list", "List the traders, checkouts, furniture shops and vending machines of the loaded scene.",
        Params = new[] { "kind?: trader, market, furniture, train or vending" })]
    private static object List(JObject p)
    {
        string kind = p.Str("kind", null);
        var shops = Shops.All().AsEnumerable();
        if (!string.IsNullOrEmpty(kind))
            shops = shops.Where(s => string.Equals(s.Kind.ToString(), kind, StringComparison.OrdinalIgnoreCase));
        return new JArray(shops.Select(s => new JObject
        {
            ["id"] = s.Id,
            ["name"] = s.Name,
            ["kind"] = s.Kind.ToString(),
        }).ToArray());
    }

    [BridgeCommand("shops.stock", "What a trader or vending machine has on offer, with current prices.",
        Params = new[] { "address: a shop id from shops.list, a GUID or a name" })]
    private static object Stock(JObject p)
    {
        string key = p.Str("address");
        var shop = Shops.Find(key) ?? throw new ArgumentException($"No shop '{key}' in this scene.");
        return new JObject
        {
            ["shop"] = shop.Name,
            ["kind"] = shop.Kind.ToString(),
            ["stock"] = new JArray(Shops.Stock(shop).Select(line => new JObject
            {
                ["id"] = line.Item.ID,
                ["item"] = line.Item.Title,
                ["amount"] = line.Amount,
                ["price"] = line.Price,
                ["currency"] = line.Currency.ToString(),
            }).ToArray()),
        };
    }

    [BridgeCommand("mail.send", "Send the player a letter, optionally with an item attached.",
        Params = new[]
        {
            "title", "text?", "item?: item id to attach", "amount?: default 1",
            "kind?: letter (mailbox, default) or parcel (locker)", "delay?: game minutes, default 0 = at once",
        })]
    private static object Send(JObject p)
    {
        var attachments = new System.Collections.Generic.List<MailAttachment>();
        int id = p.Int("item", -1);
        if (id >= 0)
        {
            var item = ItemLookup.ById(id) ?? throw new ArgumentException($"No item with id {id}.");
            attachments.Add(new MailAttachment(item, p.Int("amount", 1)));
        }

        var kind = string.Equals(p.Str("kind", "letter"), "parcel", StringComparison.OrdinalIgnoreCase) ? MailKind.Parcel : MailKind.Letter;
        bool sent = PlayerMail.Send(p.Str("title"), p.Str("text", null), attachments, kind, p.Int("delay", 0));
        if (!sent)
            throw new InvalidOperationException("Mail can only be sent while a world is loaded.");
        return new JObject
        {
            ["sent"] = true,
            ["waiting"] = PlayerMail.Inbox(MailKind.Letter).Count,
            ["parcels"] = PlayerMail.Inbox(MailKind.Parcel).Count,
            ["inTransit"] = PlayerMail.InTransit().Count,
        };
    }

    [BridgeCommand("mail.list", "Mail waiting in the mailbox or locker, or on its way.",
        Params = new[] { "kind?: letter (default), parcel or transit" })]
    private static object ListMail(JObject p)
    {
        string kind = p.Str("kind", "letter").ToLowerInvariant();
        var mail = kind == "transit" ? PlayerMail.InTransit()
            : PlayerMail.Inbox(kind == "parcel" ? MailKind.Parcel : MailKind.Letter);
        return new JArray(mail.Select(m => new JObject
        {
            ["title"] = m.Title,
            ["kind"] = m.Kind.ToString(),
            ["text"] = m.Text,
            ["contents"] = new JArray(m.Contents.ToArray()),
            ["oc"] = m.Oc,
            ["rm"] = m.Rm,
            ["secondsLeft"] = m.SecondsLeft,
        }).ToArray());
    }
}
