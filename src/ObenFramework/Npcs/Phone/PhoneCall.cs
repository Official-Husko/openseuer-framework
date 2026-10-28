namespace ObenFramework.Npcs;

/// <summary>A call to one of a mod's numbers, passed to the callback given to <see cref="PhoneNumbers.Register(ObenLoader.ObenMod, string, string, System.Action{PhoneCall}, PhoneNumber.Network, float)"/>.</summary>
public sealed class PhoneCall
{
    internal PhoneCall(PhoneLine line, Phone phone)
    {
        Line = line;
        Phone = phone;
    }

    /// <summary>The line that was dialed.</summary>
    public PhoneLine Line { get; }

    /// <summary>The phone the player is using (a wall or desk phone in the scene).</summary>
    public Phone Phone { get; }
}
