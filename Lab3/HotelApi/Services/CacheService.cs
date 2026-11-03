
using StackExchange.Redis;
using System.Text.Json;

namespace HotelApi.Services;

public class CacheService
{
    private readonly IDatabase _database;
    private readonly IConnectionMultiplexer _redis;

    public CacheService(IConnectionMultiplexer redis)
    {
        _redis = redis;
        _database = redis.GetDatabase();
    }

    // Citește datele din Redis
    public async Task<T?> GetAsync<T>(string key)
    {
        var value = await _database.StringGetAsync(key);

        if (value.IsNullOrEmpty)
            return default;

        return JsonSerializer.Deserialize<T>(value.ToString());
    }

    // Salvează datele în Redis
    public async Task SetAsync<T>(
        string key,
        T value,
        int expirationSeconds = 60)
    {
        var json = JsonSerializer.Serialize(value);

        await _database.StringSetAsync(
            key,
            json,
            TimeSpan.FromSeconds(expirationSeconds));
    }

    // Șterge o cheie din Redis
    public async Task RemoveAsync(string key)
    {
        await _database.KeyDeleteAsync(key);
    }

    // Șterge toate cheile care corespund unui model
    public async Task RemoveByPatternAsync(string pattern)
    {
        foreach (var endpoint in _redis.GetEndPoints())
        {
            var server = _redis.GetServer(endpoint);

            if (!server.IsConnected)
                continue;

            var keys = server.Keys(
                database: _database.Database,
                pattern: pattern);

            foreach (var key in keys)
            {
                await _database.KeyDeleteAsync(key);
            }
        }
    }
}
