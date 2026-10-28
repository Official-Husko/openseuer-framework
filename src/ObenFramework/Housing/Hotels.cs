// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
using System;
using System.Collections.Generic;
using HarmonyLib;
using ObenFramework.Items;
using ObenFramework.World;

namespace ObenFramework.Housing;

/// <summary>A snapshot of one hotel room.</summary>
public sealed class HotelRoomInfo
{
    internal HotelRoomInfo(string id, string hotelId, int index, string name, bool rentedByPlayer, int nightsLeft, bool free, RentAmount pricePerNight)
    {
        Id = id;
        HotelId = hotelId;
        Index = index;
        Name = name;
        RentedByPlayer = rentedByPlayer;
        NightsLeft = nightsLeft;
        Free = free;
        PricePerNight = pricePerNight;
    }

    public string Id { get; }

    public string HotelId { get; }

    /// <summary>The room's place in its hotel's list.</summary>
    public int Index { get; }

    public string Name { get; }

    public bool RentedByPlayer { get; }

    /// <summary>Nights left on the room's reservation (the player's or another guest's); 0 when free.</summary>
    public int NightsLeft { get; }

    /// <summary>Nobody has it booked.</summary>
    public bool Free { get; }

    /// <summary>Today's price for one night, after the hotel's daily price factor.</summary>
    public RentAmount PricePerNight { get; }
}

/// <summary>A hotel room the player rented or whose stay ran out, as raised by <see cref="Hotels.RoomChanged"/>.</summary>
public readonly struct HotelRoomChange
{
    public HotelRoomChange(HotelRoom room, bool rented, int nights, ChangeSource source)
    {
        Room = room;
        Rented = rented;
        Nights = nights;
        Source = source;
    }

    public HotelRoom Room { get; }

    /// <summary>True when the player rented it, false when the stay ran out.</summary>
    public bool Rented { get; }

    public int Nights { get; }

    public ChangeSource Source { get; }

    public string Id => ObjectIds.IdOf(Room);
}

/// <summary>
/// Hotels (the Bazaar inn and friends): rooms, prices and renting one for the player. The player can hold
/// one room per hotel at a time. Rooms are saved with the game.
/// </summary>
public static class Hotels
{
    private static readonly AccessTools.FieldRef<Hotel, float> PriceFactor = AccessTools.FieldRefAccess<Hotel, float>("dailyPricefactor");

    /// <summary>The player rented a room, or their stay ran out. Not raised while a save loads.</summary>
    public static readonly WorldEvent<HotelRoomChange> RoomChanged = new WorldEvent<HotelRoomChange>(nameof(RoomChanged));

    /// <summary>Every room of every hotel in the loaded scenes.</summary>
    public static List<HotelRoomInfo> Rooms()
    {
        var list = new List<HotelRoomInfo>();
        foreach (var hotel in ObjectIds.Active<Hotel>())
        {
            if (hotel.rooms == null)
                continue;
            string hotelId = ObjectIds.IdOf(hotel);
            for (int i = 0; i < hotel.rooms.Length; i++)
            {
                if (hotel.rooms[i] != null)
                    list.Add(Describe(hotel, hotelId, i));
            }
        }
        return list;
    }

    public static HotelRoomInfo Room(string roomId) => TryFind(roomId, out var hotel, out int index) ? Describe(hotel, ObjectIds.IdOf(hotel), index) : null;

    /// <summary>What a stay of <paramref name="nights"/> costs today: what <see cref="Rent"/> would charge.</summary>
    public static RentAmount Price(string roomId, int nights)
    {
        HousingRules.RequireNights(nights);
        if (!TryFind(roomId, out var hotel, out int index))
            throw new ArgumentException($"No hotel room '{roomId}'.", nameof(roomId));
        var price = hotel.rooms[index].rentPrice * PriceFactor(hotel) * nights;
        return new RentAmount(price.OC, price.RM);
    }

    /// <summary>
    /// Rents the room for the player for 1 to 7 nights, paying from their wallet, like the hotel desk. False
    /// when the room is booked, the player already has a room in that hotel, or can't pay.
    /// </summary>
    public static bool Rent(string roomId, int nights)
    {
        HousingRules.RequireNights(nights);
        if (!TryFind(roomId, out var hotel, out int index))
            throw new ArgumentException($"No hotel room '{roomId}'.", nameof(roomId));
        if (hotel.PlayerHasRoom() || Money.instance == null)
            return false;
        using (WorldChanges.EnterMod())
            return hotel.RentRoom(index, nights);
    }

    private static HotelRoomInfo Describe(Hotel hotel, string hotelId, int index)
    {
        var room = hotel.rooms[index];
        var night = room.rentPrice * PriceFactor(hotel);
        return new HotelRoomInfo(ObjectIds.IdOf(room), hotelId, index, room.roomName, room.rentedForPlayer, Math.Max(0, room.reservedDays),
            room.reservedDays == -1, new RentAmount(night.OC, night.RM));
    }

    private static bool TryFind(string roomId, out Hotel hotel, out int index)
    {
        hotel = null;
        index = -1;
        var room = ObjectIds.Find<HotelRoom>(roomId);
        if (room == null)
            return false;
        foreach (var candidate in ObjectIds.Active<Hotel>())
        {
            if (candidate.rooms == null)
                continue;
            int at = Array.IndexOf(candidate.rooms, room);
            if (at >= 0)
            {
                hotel = candidate;
                index = at;
                return true;
            }
        }
        return false;
    }
}
