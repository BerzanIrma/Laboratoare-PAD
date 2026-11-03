
using HotelApi.Data;
using HotelApi.Models;
using MongoDB.Driver;

namespace HotelApi.Services;

public class HotelService
{
    private readonly MongoDbContext _context;
    private readonly CacheService _cache;

    public HotelService(
        MongoDbContext context,
        CacheService cache)
    {
        _context = context;
        _cache = cache;
    }

    // GET: Obține toate hotelurile
    public async Task<(List<Hotel> Hotels, string Source)> GetAllAsync(
        int offset = 0,
        int limit = 10,
        string? city = null)
    {
        string cacheKey =
            $"hotels:{offset}:{limit}:{city ?? "all"}";

        // Verificăm Redis
        var cachedHotels =
            await _cache.GetAsync<List<Hotel>>(cacheKey);

        if (cachedHotels != null)
        {
            return (cachedHotels, "cache");
        }

        // Citim din MongoDB
        var filter = string.IsNullOrWhiteSpace(city)
            ? Builders<Hotel>.Filter.Empty
            : Builders<Hotel>.Filter.Eq(h => h.City, city);

        var hotels = await _context.HotelsA
            .Find(filter)
            .SortBy(h => h.Name)
            .Skip(offset)
            .Limit(limit)
            .ToListAsync();

        // Salvăm în Redis timp de 60 secunde
        await _cache.SetAsync(cacheKey, hotels, 60);

        return (hotels, "database");
    }

    // GET: Obține un hotel după ID
    public async Task<(Hotel? Hotel, string Source)> GetByIdAsync(
        string id)
    {
        if (!MongoDB.Bson.ObjectId.TryParse(id, out _))
            return (null, "database");

        string cacheKey = $"hotel:{id}";

        // Verificăm Redis
        var cachedHotel =
            await _cache.GetAsync<Hotel>(cacheKey);

        if (cachedHotel != null)
        {
            return (cachedHotel, "cache");
        }

        // Citim din MongoDB
        var hotel = await _context.HotelsA
            .Find(h => h.Id == id)
            .FirstOrDefaultAsync();

        // Salvăm în Redis dacă există
        if (hotel != null)
        {
            await _cache.SetAsync(cacheKey, hotel, 60);
        }

        return (hotel, "database");
    }

    // POST: Adaugă un hotel
    public async Task<string> CreateAsync(Hotel hotel)
    {
        hotel.Id =
            MongoDB.Bson.ObjectId.GenerateNewId().ToString();

        hotel.UpdatedAt = DateTime.UtcNow;

        await _context.HotelsA.InsertOneAsync(hotel);

        await InvalidateHotelCacheAsync();

        return hotel.Id;
    }

    // PUT: Modifică un hotel
    public async Task<bool> UpdateAsync(
        string id,
        Hotel updatedHotel)
    {
        if (!MongoDB.Bson.ObjectId.TryParse(id, out _))
            return false;

        updatedHotel.UpdatedAt = DateTime.UtcNow;

        var update = Builders<Hotel>.Update
            .Set(h => h.Name, updatedHotel.Name)
            .Set(h => h.City, updatedHotel.City)
            .Set(h => h.Address, updatedHotel.Address)
            .Set(h => h.Stars, updatedHotel.Stars)
            .Set(h => h.TotalRooms, updatedHotel.TotalRooms)
            .Set(h => h.PricePerNight, updatedHotel.PricePerNight)
            .Set(h => h.UpdatedAt, updatedHotel.UpdatedAt);

        var result = await _context.HotelsA.UpdateOneAsync(
            h => h.Id == id,
            update);

        if (result.MatchedCount > 0)
        {
            await InvalidateHotelCacheAsync(id);
        }

        return result.MatchedCount > 0;
    }

    // DELETE: Șterge un hotel
    public async Task<bool> DeleteAsync(string id)
    {
        if (!MongoDB.Bson.ObjectId.TryParse(id, out _))
            return false;

        var result = await _context.HotelsA.DeleteOneAsync(
            h => h.Id == id);

        if (result.DeletedCount > 0)
        {
            await InvalidateHotelCacheAsync(id);
        }

        return result.DeletedCount > 0;
    }

    // Invalidează cache-ul hotelurilor
    private async Task InvalidateHotelCacheAsync(
        string? hotelId = null)
    {
        if (!string.IsNullOrWhiteSpace(hotelId))
        {
            await _cache.RemoveAsync($"hotel:{hotelId}");
        }

        await _cache.RemoveByPatternAsync("hotels:*");
    }
}
