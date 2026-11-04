
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using System.ComponentModel.DataAnnotations;

namespace HotelApi.Models;

public class Hotel
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string? Id { get; set; }

    [BsonElement("Name")]
    [Required]
    [StringLength(100)]
    public string Name { get; set; } = string.Empty;

    [BsonElement("City")]
    [Required]
    public string City { get; set; } = string.Empty;

    [BsonElement("Address")]
    public string Address { get; set; } = string.Empty;

    [BsonElement("Stars")]
    [Range(1, 5)]
    public int Stars { get; set; }

    [BsonElement("TotalRooms")]
    [Range(1, int.MaxValue)]
    public int TotalRooms { get; set; }

    [BsonElement("PricePerNight")]
    [Range(typeof(decimal), "0.01", "999999999")]
    public decimal PricePerNight { get; set; }

    [BsonElement("UpdatedAt")]
    public DateTime UpdatedAt { get; set; }
}
