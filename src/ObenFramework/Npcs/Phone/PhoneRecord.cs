using UnityEngine;

namespace ObenFramework.Npcs;

/// <summary>A phone in the scene you are in, copied when read (see <see cref="Phones"/>).</summary>
public sealed class PhoneRecord
{
    /// <summary>Stable address of the phone (an <c>ObjectAddress</c> string): what the other <see cref="Phones"/> calls take.</summary>
    public string Address { get; }

    /// <summary>The name the game shows for it ("Phone", "Payphone"...).</summary>
    public string Name { get; }

    public Vector3 Position { get; }

    /// <summary>The networks it can call.</summary>
    public PhoneNumber.Network Network { get; }

    /// <summary>A call is on.</summary>
    public bool InCall { get; }

    /// <summary>It is ringing.</summary>
    public bool Ringing { get; }

    /// <summary>The number calling it while it rings, else null.</summary>
    public string Caller { get; }

    /// <summary>Character id of the NPC that owns it (the player cannot use an owned phone), or 0.</summary>
    public int OwnerNpc { get; }

    internal PhoneRecord(Phone phone, string address, bool inCall, PhoneNumber incoming)
    {
        Address = address;
        Name = phone.title;
        Position = phone.transform.position;
        Network = phone.network;
        InCall = inCall;
        Ringing = incoming != null;
        Caller = incoming != null ? PhoneDigits.Format(incoming.number) : null;
        OwnerNpc = phone.owner != null ? phone.owner.ID : 0;
    }
}

/// <summary>A call on a phone (see <see cref="Phones.CallStarted"/>).</summary>
public sealed class PhoneCallEvent
{
    /// <summary>The phone's address.</summary>
    public string Phone { get; }

    /// <summary>The number on the other end.</summary>
    public string Number { get; }

    internal PhoneCallEvent(string phone, string number)
    {
        Phone = phone;
        Number = number;
    }
}
