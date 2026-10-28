// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
using System;
using System.Collections.Generic;
using HarmonyLib;
using ObenFramework.Saves;
using ObenFramework.World;
using ObenFramework.World.Addressing;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ObenFramework.Economy;

/// <summary>A junk mail sender of the loaded scene (the game's <c>RandommailSender</c>).</summary>
public sealed class MailSenderInfo
{
    internal MailSenderInfo(RandommailSender sender, int min, int max)
    {
        Id = ObjectAddress.Of(sender).ToString();
        Entries = sender.randomMail != null ? sender.randomMail.Count : 0;
        var names = new List<string>();
        if (sender.randomMail != null)
        {
            foreach (var entry in sender.randomMail)
            {
                if (entry == null || entry.mailNames == null)
                    continue;
                foreach (string name in entry.mailNames)
                {
                    if (!string.IsNullOrEmpty(name) && !names.Contains(name))
                        names.Add(name);
                }
            }
        }
        MailNames = names.ToArray();
        PerDayMin = min;
        PerDayMax = max;
        FakeInvoiceChance = sender.randomInvoiceDailyChance;
        FakeInvoiceNames = (string[])(sender.randomInvoiceNames ?? new string[0]).Clone();
    }

    public string Id { get; }

    /// <summary>How many kinds of mail it picks from.</summary>
    public int Entries { get; }

    /// <summary>The names its mail goes by.</summary>
    public string[] MailNames { get; }

    /// <summary>The range of how many it sends a day (the upper end is exclusive, as in the game).</summary>
    public int PerDayMin { get; }

    public int PerDayMax { get; }

    /// <summary>The daily chance of a forged invoice (paying one is never required).</summary>
    public float FakeInvoiceChance { get; }

    public string[] FakeInvoiceNames { get; }
}

/// <summary>
/// The game's junk mail: every day except Sunday morning a sender in each world mails a few random letters,
/// recipes, notes and parcels, and sometimes a forged invoice. This lists the senders and makes one send now.
/// Call on the main thread.
/// </summary>
public static class MailSchedules
{
    /// <summary>Most letters one call may ask for.</summary>
    public const int MaxMails = 100;

    private static readonly AccessTools.FieldRef<RandommailSender, int> Min = AccessTools.FieldRefAccess<RandommailSender, int>("minAmount");
    private static readonly AccessTools.FieldRef<RandommailSender, int> Max = AccessTools.FieldRefAccess<RandommailSender, int>("maxAmount");

    /// <summary>Every junk mail sender of the loaded scenes.</summary>
    public static List<MailSenderInfo> Senders()
    {
        var list = new List<MailSenderInfo>();
        foreach (var sender in Object.FindObjectsByType<RandommailSender>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            list.Add(new MailSenderInfo(sender, Min(sender), Max(sender)));
        return list;
    }

    /// <summary>Makes a sender (by address or GUID; null for the first one) send a day's worth of junk mail now.</summary>
    public static bool SendNow(string key = null)
    {
        var sender = Find(key);
        return sender != null && Send(sender, Min(sender), Max(sender));
    }

    /// <summary>Makes a sender send between <paramref name="min"/> and <paramref name="max"/> (exclusive) letters now.</summary>
    public static bool SendRandom(string key, int min, int max)
    {
        if (min < 0 || max < min || max > MaxMails)
            throw new ArgumentOutOfRangeException(nameof(max), "0 <= min <= max <= " + MaxMails + ".");
        var sender = Find(key);
        return sender != null && Send(sender, min, max);
    }

    private static RandommailSender Find(string key)
    {
        if (SaveGate.IsBusy || MailController.instance == null)
            return null;
        if (!string.IsNullOrEmpty(key))
            return ObjectResolver.Find<RandommailSender>(key);
        var all = Object.FindObjectsByType<RandommailSender>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        return all.Length > 0 ? all[0] : null;
    }

    private static bool Send(RandommailSender sender, int min, int max)
    {
        if (sender.randomMail == null || sender.randomMail.Count == 0)
            return false;
        try
        {
            RandommailSender.SendRandomMail(sender.randomMail, min, max);
            return true;
        }
        catch (Exception e)
        {
            WorldLog.Failed("MailSchedules.Send", e);
            return false;
        }
    }
}
