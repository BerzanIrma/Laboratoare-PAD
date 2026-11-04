using HotelApi.Data;
using HotelApi.Models;
using MongoDB.Driver;

namespace HotelApi.Services;

public class HotelService
{
    private readonly CacheService _cache;
    private readonly IMongoCollection<Hotel> _hotels;
    private readonly string _group;

    public HotelService(
        MongoDbContext context,
        CacheService cache,
        IConfiguration configuration)
    {
        _cache = cache;

        // Folosește grupul setat explicit.
        // Dacă API_GROUP lipsește, îl determină din numele containerului.
        var configuredGroup = configuration["API_GROUP"]?
            .ToUpperInvariant();

        _group = !string.IsNullOrWhiteSpace(configuredGroup)
            ? configuredGroup
            : StableHash(Environment.MachineName) % 2 == 0
                ? "A"
                : "B";

        // Verifică dacă grupul este valid
        if (_group != "A" && _group != "B")
        {
            throw new InvalidOperationException(
                "API_GROUP must be A or B.");
        }

        // Instanța API lucrează cu baza corespunzătoare grupului său
        _hotels = _group == "A"
            ? context.HotelsA
            : context.HotelsB;
    }

    // GET: Obține toate hotelurile
    public async Task<(List<Hotel> Hotels, string Source)> GetAllAsync(
        int offset = 0,
        int limit = 10,
        string? city = null)
    {
        string cacheKey =
            $"hotels:{_group}:{offset}:{limit}:{city ?? "all"}";

        var cachedHotels =
            await _cache.GetAsync<List<Hotel>>(cacheKey);

        if (cachedHotels != null)
        {
            return (cachedHotels, "cache");
        }

        var filter = string.IsNullOrWhiteSpace(city)
            ? Builders<Hotel>.Filter.Empty
            : Builders<Hotel>.Filter.Eq(h => h.City, city);

        var hotels = await _hotels
            .Find(filter)
            .SortBy(h => h.Name)
            .Skip(offset)
            .Limit(limit)
            .ToListAsync();

        // Salvează rezultatul în cache pentru 60 de secunde
        await _cache.SetAsync(cacheKey, hotels, 60);

        return (hotels, "database");
    }

    // GET: Obține un hotel după ID
    public async Task<(Hotel? Hotel, string Source)> GetByIdAsync(
        string id)
    {
        if (!MongoDB.Bson.ObjectId.TryParse(id, out _))
        {
            return (null, "database");
        }

        string cacheKey = $"hotel:{_group}:{id}";

        var cachedHotel =
            await _cache.GetAsync<Hotel>(cacheKey);

        if (cachedHotel != null)
        {
            return (cachedHotel, "cache");
        }

        var hotel = await _hotels
            .Find(h => h.Id == id)
            .FirstOrDefaultAsync();

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

        await _hotels.InsertOneAsync(hotel);

        // Datele s-au modificat, deci ștergem listele din cache
        await InvalidateHotelCacheAsync();

        return hotel.Id;
    }

    // PUT: Modifică un hotel
    public async Task<bool> UpdateAsync(
        string id,
        Hotel updatedHotel)
    {
        if (!MongoDB.Bson.ObjectId.TryParse(id, out _))
        {
            return false;
        }

        updatedHotel.UpdatedAt = DateTime.UtcNow;

        var update = Builders<Hotel>.Update
            .Set(h => h.Name, updatedHotel.Name)
            .Set(h => h.City, updatedHotel.City)
            .Set(h => h.Address, updatedHotel.Address)
            .Set(h => h.Stars, updatedHotel.Stars)
            .Set(h => h.TotalRooms, updatedHotel.TotalRooms)
            .Set(h => h.PricePerNight, updatedHotel.PricePerNight)
            .Set(h => h.UpdatedAt, updatedHotel.UpdatedAt);

        var result = await _hotels.UpdateOneAsync(
            h => h.Id == id,
            update);

        if (result.MatchedCount > 0)
        {
            // Șterge hotelul individual și listele din cache
            await InvalidateHotelCacheAsync(id);
        }

        return result.MatchedCount > 0;
    }

    // DELETE: Șterge un hotel
    public async Task<bool> DeleteAsync(string id)
    {
        if (!MongoDB.Bson.ObjectId.TryParse(id, out _))
        {
            return false;
        }

        var result = await _hotels.DeleteOneAsync(
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
        // Șterge cache-ul hotelului individual
        if (!string.IsNullOrWhiteSpace(hotelId))
        {
            await _cache.RemoveAsync(
                $"hotel:{_group}:{hotelId}");
        }

        // Șterge toate listele de hoteluri din grupul curent
        await _cache.RemoveByPatternAsync(
            $"hotels:{_group}:*");
    }

    // Generează un hash stabil pentru numele containerului.
    // Este folosit doar dacă API_GROUP nu este setat.
    private static int StableHash(string value)
    {
        int hash = 0;

        foreach (char c in value)
        {
            hash = (hash * 31 + c) & 0x7fffffff;
        }

        return hash;
    }
}