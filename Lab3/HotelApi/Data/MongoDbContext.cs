
using MongoDB.Driver;
using HotelApi.Models;

namespace HotelApi.Data;

public class MongoDbContext
{
    public IMongoDatabase DatabaseA { get; }
    public IMongoDatabase DatabaseB { get; }

    public IMongoCollection<Hotel> HotelsA { get; }
    public IMongoCollection<Hotel> HotelsB { get; }

    public IMongoCollection<Reservation> ReservationsA { get; }
    public IMongoCollection<Reservation> ReservationsB { get; }

    public MongoDbContext(IConfiguration configuration)
    {
        var connectionA = configuration["MONGO_A"]
            ?? throw new InvalidOperationException("MONGO_A is not configured.");

        var connectionB = configuration["MONGO_B"]
            ?? throw new InvalidOperationException("MONGO_B is not configured.");

        var clientA = new MongoClient(connectionA);
        var clientB = new MongoClient(connectionB);

        DatabaseA = clientA.GetDatabase("hotelsdb");
        DatabaseB = clientB.GetDatabase("hotelsdb");

        HotelsA = DatabaseA.GetCollection<Hotel>("hotels");
        HotelsB = DatabaseB.GetCollection<Hotel>("hotels");

        ReservationsA = DatabaseA.GetCollection<Reservation>("reservations");
        ReservationsB = DatabaseB.GetCollection<Reservation>("reservations");
    }
}
