namespace ObenFramework.Npcs;

/// <summary>A phone book entry as read from the game, whoever added it.</summary>
public sealed class PhoneNumberInfo
{
    internal PhoneNumberInfo(PhoneNumber asset, string owner)
    {
        Number = PhoneDigits.Format(asset.number);
        Name = asset.name;
        Network = asset.network;
        AnswerChance = asset.answerChance;
        Conversation = asset.conversation;
        Dialable = !asset.notIndexed && asset.number != null && asset.number.Length > 0;
        Owner = owner;
    }

    /// <summary>The number as typed, or empty for the game's built-in "not reachable" style entries.</summary>
    public string Number { get; }

    /// <summary>The entry's name in the game's data.</summary>
    public string Name { get; }

    public PhoneNumber.Network Network { get; }

    public float AnswerChance { get; }

    /// <summary>Title of the conversation a call starts.</summary>
    public string Conversation { get; }

    /// <summary>The number can be dialed. False for the game's special entries and numbers only scripts call.</summary>
    public bool Dialable { get; }

    /// <summary>Id of the mod that added the number, or null for the game's own numbers.</summary>
    public string Owner { get; }
}
