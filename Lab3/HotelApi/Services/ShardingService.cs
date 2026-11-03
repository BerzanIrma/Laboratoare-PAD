
namespace HotelApi.Services;

public class ShardingService
{
    public string GetShard(string hotelId)
    {
        if (string.IsNullOrWhiteSpace(hotelId) ||
            hotelId.Length < 2)
        {
            throw new ArgumentException(
                "Invalid HotelId.");
        }

        // Ultimele două caractere hexazecimale
        // sunt utilizate pentru determinarea shard-ului.
        string lastByte = hotelId[^2..];

        int value = Convert.ToInt32(lastByte, 16);

        return value % 2 == 0 ? "A" : "B";
    }
}
