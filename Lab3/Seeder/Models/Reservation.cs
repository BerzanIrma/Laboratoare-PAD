using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Seeder.Models;

// aceleasi campuri ca Reservation din HotelApi
public class Reservation
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string Id { get; set; } = "";

    // in API, HotelId e salvat ca ObjectId, deci trebuie la fel aici
    [BsonRepresentation(BsonType.ObjectId)]
    public string HotelId { get; set; } = "";

    public string GuestName { get; set; } = "";
    public string GuestEmail { get; set; } = "";
    public DateTime CheckIn { get; set; }
    public DateTime CheckOut { get; set; }
    public int Rooms { get; set; }
    public string Status { get; set; } = "confirmed";
    public DateTime CreatedAt { get; set; }
}