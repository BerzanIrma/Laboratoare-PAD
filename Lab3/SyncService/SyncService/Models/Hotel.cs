
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace SyncService.Models;

public class Hotel
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string Id { get; set; } = "";

    public string Name { get; set; } = "";

    public string City { get; set; } = "";

    public string Address { get; set; } = "";

    public int Stars { get; set; }

    public int TotalRooms { get; set; }

    public decimal PricePerNight { get; set; }

    public DateTime UpdatedAt { get; set; }
}
