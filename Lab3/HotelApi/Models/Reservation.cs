
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace HotelApi.Models;

public class Reservation
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string? Id { get; set; }

    [BsonElement("HotelId")]
    [BsonRepresentation(BsonType.ObjectId)]
    public string HotelId { get; set; } = string.Empty;

    [BsonElement("GuestName")]
    public string GuestName { get; set; } = string.Empty;

    [BsonElement("GuestEmail")]
    public string GuestEmail { get; set; } = string.Empty;

    [BsonElement("CheckIn")]
    public DateTime CheckIn { get; set; }

    [BsonElement("CheckOut")]
    public DateTime CheckOut { get; set; }

    [BsonElement("Rooms")]
    public int Rooms { get; set; }

    [BsonElement("Status")]
    public string Status { get; set; } = "confirmed";

    [BsonElement("CreatedAt")]
    public DateTime CreatedAt { get; set; }
}
