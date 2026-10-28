using System;

namespace ObenFramework.Npcs;

/// <summary>
/// A phone number a mod added with <see cref="PhoneNumbers.Register(ObenLoader.ObenMod, string, string, Action{PhoneCall}, PhoneNumber.Network, float)"/>.
/// Dispose it to take the number out of the game again; the owning mod's unload does that too.
/// </summary>
public sealed class PhoneLine : IDisposable
{
    internal PhoneLine(string owner, string number, int[] keys, string name, string conversation,
        Action<PhoneCall> onCall, PhoneNumber.Network network, float answerChance)
    {
        Owner = owner;
        Number = number;
        Keys = keys;
        Name = name;
        Conversation = conversation;
        OnCall = onCall;
        Network = network;
        AnswerChance = answerChance;
    }

    /// <summary>Id of the mod that registered the number.</summary>
    public string Owner { get; }

    /// <summary>The number as typed on the phone: digits, with * and # allowed.</summary>
    public string Number { get; }

    /// <summary>What the line is called in lists and logs.</summary>
    public string Name { get; }

    /// <summary>Title of the conversation a call starts, or null for a line that only runs its callback.</summary>
    public string Conversation { get; }

    /// <summary>The networks a phone must be on to reach the number.</summary>
    public PhoneNumber.Network Network { get; }

    /// <summary>Chance, 0 to 1, that someone picks up. Below 1 the game plays its "not answering" line sometimes.</summary>
    public float AnswerChance { get; }

    /// <summary>The number is in the game's phone book.</summary>
    public bool IsRegistered => Asset != null;

    internal int[] Keys { get; }

    internal Action<PhoneCall> OnCall { get; }

    internal PhoneNumber Asset { get; set; }

    public void Dispose() => PhoneNumbers.Remove(this);
}
