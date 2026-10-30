// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
using System;
using System.Collections.Generic;
using ObenFramework.Dialogue;
using ObenLoader;
using ObenLoader.Runtime;
using UnityEngine;

namespace ObenFramework.Npcs;

/// <summary>
/// The game's phone book. Mods add numbers that the in-game phones can dial; a call either starts one of the
/// game's conversations or just runs the mod's callback. Numbers are added to the game's
/// <c>PhoneNumberDatabase</c> and removed again when the mod unloads. They are not saved: register them
/// each time the mod loads.
/// </summary>
public static class PhoneNumbers
{
    private const string GameBookPath = "Dialogue/Phone";

    private static readonly List<PhoneLine> Lines = new List<PhoneLine>();

    /// <summary>
    /// Adds a number whose call only runs <paramref name="onCall"/>: the phone picks up, the callback runs, and
    /// the phone hangs up. Use it to show a message or trigger something. To have someone speak, use the
    /// overload with a conversation. Throws for a number that is not 1 to 10 characters of 0-9, * and #, or one
    /// the phone book already holds.
    /// </summary>
    public static PhoneLine Register(ObenMod owner, string number, string name, Action<PhoneCall> onCall,
        PhoneNumber.Network network = PhoneNumber.Network.Obenseuer, float answerChance = 1f)
    {
        if (onCall == null)
            throw new ArgumentNullException(nameof(onCall));
        return Add(owner, number, name, null, onCall, network, answerChance);
    }

    /// <summary>
    /// Adds a number whose call starts the conversation with this title (one from the game or a database a
    /// mod added; see <see cref="Conversations.Exists"/>). <paramref name="onCall"/>, when given, runs just
    /// before the conversation starts. A call to a title that does not exist hangs up at once.
    /// </summary>
    public static PhoneLine Register(ObenMod owner, string number, string name, string conversation,
        Action<PhoneCall> onCall = null, PhoneNumber.Network network = PhoneNumber.Network.Obenseuer, float answerChance = 1f)
    {
        if (string.IsNullOrEmpty(conversation))
            throw new ArgumentException("A conversation title is needed.", nameof(conversation));
        return Add(owner, number, name, conversation, onCall, network, answerChance);
    }

    /// <summary>
    /// Whether <paramref name="number"/> can be registered now, without throwing: false with the reason (it
    /// isn't a phone number, or the phone book already holds it and whose it is). <c>Register</c> throws
    /// with the same reasons.
    /// </summary>
    public static bool CanRegister(string number, out string reason)
    {
        reason = Refusal(number, out _);
        return reason == null;
    }

    /// <summary>Every number in the phone book, the game's and the mods', in the order the game holds them.</summary>
    public static List<PhoneNumberInfo> List()
    {
        var result = new List<PhoneNumberInfo>();
        EnsureGameBook();
        foreach (var asset in PhoneNumberDatabase.database)
        {
            if (asset != null)
                result.Add(new PhoneNumberInfo(asset, LineOf(asset)?.Owner));
        }
        return result;
    }

    /// <summary>The numbers mods have registered and not yet removed.</summary>
    public static List<PhoneLine> Registered() => new List<PhoneLine>(Lines);

    /// <summary>The phone book entry for a number typed as digits, or null.</summary>
    public static PhoneNumberInfo Find(string number)
    {
        if (!PhoneDigits.TryParse(number, out var keys))
            return null;
        var asset = FindAsset(keys);
        return asset == null ? null : new PhoneNumberInfo(asset, LineOf(asset)?.Owner);
    }

    internal static void Init(ObenMod framework) => framework.Track(Disposable.Create(RemoveAll));

    internal static void Remove(PhoneLine line)
    {
        if (!Lines.Remove(line))
            return;
        var asset = line.Asset;
        line.Asset = null;
        if (asset == null)
            return;
        PhoneNumberDatabase.database.Remove(asset);
        UnityEngine.Object.Destroy(asset);
    }

    internal static PhoneLine LineOf(PhoneNumber asset)
    {
        for (int i = 0; i < Lines.Count; i++)
        {
            if (Lines[i].Asset == asset)
                return Lines[i];
        }
        return null;
    }

    private static PhoneLine Add(ObenMod owner, string number, string name, string conversation,
        Action<PhoneCall> onCall, PhoneNumber.Network network, float answerChance)
    {
        if (owner == null)
            throw new ArgumentNullException(nameof(owner));
        if (answerChance < 0f || answerChance > 1f || float.IsNaN(answerChance))
            throw new ArgumentOutOfRangeException(nameof(answerChance), "The answer chance is between 0 and 1.");
        if ((network & ~(PhoneNumber.Network.Obenseuer | PhoneNumber.Network.Outside | PhoneNumber.Network.Wasteland)) != 0)
            throw new ArgumentException("Unknown network flags.", nameof(network));
        string refusal = Refusal(number, out var keys);
        if (refusal != null && keys == null)
            throw new ArgumentException(refusal, nameof(number));
        if (refusal != null)
            throw new InvalidOperationException(refusal);

        var line = new PhoneLine(owner.Info.Id, number, keys, string.IsNullOrEmpty(name) ? number : name, conversation, onCall, network, answerChance);
        var asset = ScriptableObject.CreateInstance<PhoneNumber>();
        // Kept out of the unused-asset sweeps a scene change triggers.
        asset.hideFlags = HideFlags.HideAndDontSave;
        asset.name = line.Name;
        asset.number = keys;
        asset.network = network;
        asset.notIndexed = false;
        asset.answerChance = answerChance;
        asset.conversation = conversation ?? string.Empty;
        line.Asset = asset;

        Lines.Add(line);
        PhoneNumberDatabase.database.Add(asset);
        owner.Track(line);
        return line;
    }

    // Why a number can't be registered, or null. Keys are null when it isn't a phone number at all.
    private static string Refusal(string number, out int[] keys)
    {
        if (!PhoneDigits.TryParse(number, out keys))
        {
            keys = null;
            return $"'{number}' is not a phone number: use 1 to {PhoneDigits.MaxKeys} of 0-9, * and #.";
        }
        EnsureGameBook();
        var taken = FindAsset(keys);
        return taken != null ? $"The number {number} is already in the phone book ({LineOf(taken)?.Owner ?? "the game"})." : null;
    }

    // The game fills its book from Resources in PhoneNumberDatabase.Awake, but only while the list is empty.
    // Adding ours first would stop that, so load the game's numbers now, the same way it does.
    private static void EnsureGameBook()
    {
        var book = PhoneNumberDatabase.database;
        if (book.Count == 0)
            book.AddRange(Resources.LoadAll<PhoneNumber>(GameBookPath));
    }

    private static PhoneNumber FindAsset(int[] keys)
    {
        foreach (var asset in PhoneNumberDatabase.database)
        {
            if (asset != null && !asset.notIndexed && PhoneDigits.Same(asset.number, keys))
                return asset;
        }
        return null;
    }

    private static void RemoveAll()
    {
        for (int i = Lines.Count - 1; i >= 0; i--)
            Remove(Lines[i]);
    }

    /// <summary>
    /// Runs the callback for a call to a mod line. Returns false when the call should not go on to a
    /// conversation, in which case the phone has already been hung up.
    /// </summary>
    internal static bool HandleCall(Phone phone, PhoneNumber number)
    {
        var line = number != null ? LineOf(number) : null;
        if (line == null)
            return true;

        if (line.OnCall != null)
        {
            try { line.OnCall(new PhoneCall(line, phone)); }
            catch (Exception e) { NpcsLog.Failed($"Phone callback of {line.Owner} for {line.Number}", e); }
        }

        if (line.Conversation != null && Conversations.Exists(line.Conversation))
            return true;
        if (line.Conversation != null)
            NpcsLog.Warn($"{line.Owner}: the conversation '{line.Conversation}' for {line.Number} does not exist, hanging up.");
        phone.OnCallEnded();
        return false;
    }
}
