
using HotelApi.Data;
using HotelApi.Models;
using MongoDB.Bson;
using MongoDB.Driver;

namespace HotelApi.Services;

public class ReservationService
{
    private readonly MongoDbContext _context;
    private readonly ShardingService _sharding;

    public ReservationService(
        MongoDbContext context,
        ShardingService sharding)
    {
        _context = context;
        _sharding = sharding;
    }

    // Selectează colecția în funcție de HotelId
    private IMongoCollection<Reservation> GetCollection(
        string hotelId)
    {
        string shard = _sharding.GetShard(hotelId);

        return shard == "A"
            ? _context.ReservationsA
            : _context.ReservationsB;
    }

    // GET: Toate rezervările unui hotel
    public async Task<List<Reservation>> GetAllAsync(
        string hotelId,
        int offset = 0,
        int limit = 10)
    {
        var collection = GetCollection(hotelId);

        return await collection
            .Find(r => r.HotelId == hotelId)
            .SortByDescending(r => r.CreatedAt)
            .Skip(offset)
            .Limit(limit)
            .ToListAsync();
    }

    // GET: O rezervare după ID
    public async Task<Reservation?> GetByIdAsync(
        string hotelId,
        string reservationId)
    {
        if (!ObjectId.TryParse(hotelId, out _) ||
            !ObjectId.TryParse(reservationId, out _))
            return null;

        var collection = GetCollection(hotelId);

        return await collection
            .Find(r => r.HotelId == hotelId &&
                       r.Id == reservationId)
            .FirstOrDefaultAsync();
    }

    // POST: Creează o rezervare
    public async Task<string> CreateAsync(
        Reservation reservation)
    {
        var collection = GetCollection(reservation.HotelId);

        reservation.Id = ObjectId.GenerateNewId().ToString();
        reservation.CreatedAt = DateTime.UtcNow;
        reservation.Status = "confirmed";

        await collection.InsertOneAsync(reservation);

        return reservation.Id;
    }

    // PUT: Modifică rezervarea
    public async Task<bool> UpdateAsync(
        string hotelId,
        string reservationId,
        Reservation updatedReservation)
    {
        if (!ObjectId.TryParse(hotelId, out _) ||
            !ObjectId.TryParse(reservationId, out _))
            return false;

        var collection = GetCollection(hotelId);

        var update = Builders<Reservation>.Update
            .Set(r => r.GuestName, updatedReservation.GuestName)
            .Set(r => r.GuestEmail, updatedReservation.GuestEmail)
            .Set(r => r.CheckIn, updatedReservation.CheckIn)
            .Set(r => r.CheckOut, updatedReservation.CheckOut)
            .Set(r => r.Rooms, updatedReservation.Rooms)
            .Set(r => r.Status, updatedReservation.Status);

        var result = await collection.UpdateOneAsync(
            r => r.Id == reservationId &&
                 r.HotelId == hotelId,
            update);

        return result.MatchedCount > 0;
    }

    // DELETE: Anulează rezervarea
    public async Task<bool> CancelAsync(
        string hotelId,
        string reservationId)
    {
        if (!ObjectId.TryParse(hotelId, out _) ||
            !ObjectId.TryParse(reservationId, out _))
            return false;

        var collection = GetCollection(hotelId);

        var update = Builders<Reservation>.Update
            .Set(r => r.Status, "cancelled");

        var result = await collection.UpdateOneAsync(
            r => r.Id == reservationId &&
                 r.HotelId == hotelId,
            update);

        return result.MatchedCount > 0;
    }
}
