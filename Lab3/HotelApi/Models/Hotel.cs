
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace HotelApi.Models;

public class Hotel
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string? Id { get; set; }

    [BsonElement("Name")]
    public string Name { get; set; } = string.Empty;

    [BsonElement("City")]
    public string City { get; set; } = string.Empty;

    [BsonElement("Address")]
    public string Address { get; set; } = string.Empty;

    [BsonElement("Stars")]
    public int Stars { get; set; }

    [BsonElement("TotalRooms")]
    public int TotalRooms { get; set; }

    [BsonElement("PricePerNight")]
    public decimal PricePerNight { get; set; }

    [BsonElement("UpdatedAt")]
    public DateTime UpdatedAt { get; set; }
}
