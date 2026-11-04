
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using System.ComponentModel.DataAnnotations;

namespace HotelApi.Models;

public class Reservation
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string? Id { get; set; }

    [BsonElement("HotelId")]
    [BsonRepresentation(BsonType.ObjectId)]
    [Required]
    public string HotelId { get; set; } = string.Empty;

    [BsonElement("GuestName")]
    [Required]
    public string GuestName { get; set; } = string.Empty;

    [BsonElement("GuestEmail")]
    [Required]
    [EmailAddress]
    public string GuestEmail { get; set; } = string.Empty;

    [BsonElement("CheckIn")]
    public DateTime CheckIn { get; set; }

    [BsonElement("CheckOut")]
    public DateTime CheckOut { get; set; }

    [BsonElement("Rooms")]
    [Range(1, 10)]
    public int Rooms { get; set; }

    [BsonElement("Status")]
    [RegularExpression("^(confirmed|cancelled)$")]
    public string Status { get; set; } = "confirmed";

    [BsonElement("CreatedAt")]
    public DateTime CreatedAt { get; set; }
}
