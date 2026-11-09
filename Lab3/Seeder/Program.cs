
using MongoDB.Bson;
using MongoDB.Driver;
using Seeder.Models;

string connectionA = Environment.GetEnvironmentVariable("MONGO_A")
    ?? throw new Exception("MONGO_A is not configured");

string connectionB = Environment.GetEnvironmentVariable("MONGO_B")
    ?? throw new Exception("MONGO_B is not configured");

var clientA = new MongoClient(connectionA);
var clientB = new MongoClient(connectionB);

var dbA = clientA.GetDatabase("hotelsdb");
var dbB = clientB.GetDatabase("hotelsdb");

var hotelsA = dbA.GetCollection<Hotel>("hotels");
var hotelsB = dbB.GetCollection<Hotel>("hotels");

var reservationsA = dbA.GetCollection<Reservation>("reservations");
var reservationsB = dbB.GetCollection<Reservation>("reservations");

var cities = new[]
{
    "Chisinau", "Bucharest", "Iasi", "Brasov",
    "Cluj-Napoca", "Timisoara", "Balti", "Oradea"
};

var hotels = new List<Hotel>();

// Generate 50 hotels
for (int i = 1; i <= 50; i++)
{
    hotels.Add(new Hotel
    {
        Id = i.ToString("x24"),
        Name = $"Hotel {i}",
        City = cities[(i - 1) % cities.Length],
        Address = $"Street {i}, Number {i}",
        Stars = (i % 5) + 1,
        TotalRooms = 50 + i,
        PricePerNight = 40 + i * 5,
        UpdatedAt = DateTime.UtcNow
    });
}

// Save all hotels in both clusters
foreach (var hotel in hotels)
{
    await hotelsA.ReplaceOneAsync(
        x => x.Id == hotel.Id,
        hotel,
        new ReplaceOptions { IsUpsert = true });

    await hotelsB.ReplaceOneAsync(
        x => x.Id == hotel.Id,
        hotel,
        new ReplaceOptions { IsUpsert = true });
}

Console.WriteLine("50 hotels inserted in both clusters.");

var random = new Random(42);

string[] guests =
{
    "Ion Popescu",
    "Maria Ionescu",
    "Ana Rusu",
    "John Smith",
    "Elena Brown"
};

// Generate 300 reservations
for (int i = 1; i <= 300; i++)
{
    var hotel = hotels[random.Next(hotels.Count)];

    var checkIn = DateTime.UtcNow.Date.AddDays(random.Next(1, 90));

    var reservation = new Reservation
    {
        Id = (1000 + i).ToString("x24"),
        HotelId = hotel.Id,
        GuestName = guests[random.Next(guests.Length)],
        GuestEmail = $"guest{i}@example.com",
        CheckIn = checkIn,
        CheckOut = checkIn.AddDays(random.Next(1, 8)),
        Rooms = random.Next(1, 4),
        Status = "confirmed",
        CreatedAt = DateTime.UtcNow
    };

    // Sharding: even last hexadecimal byte -> A,
    // odd last hexadecimal byte -> B
    int lastByte = Convert.ToInt32(hotel.Id[^2..], 16);

    if (lastByte % 2 == 0)
    {
        await reservationsA.ReplaceOneAsync(
            x => x.Id == reservation.Id,
            reservation,
            new ReplaceOptions { IsUpsert = true });
    }
    else
    {
        await reservationsB.ReplaceOneAsync(
            x => x.Id == reservation.Id,
            reservation,
            new ReplaceOptions { IsUpsert = true });
    }
}

Console.WriteLine("300 reservations distributed between clusters.");

Console.WriteLine(
    $"Hotels A: {await hotelsA.CountDocumentsAsync(FilterDefinition<Hotel>.Empty)}");

Console.WriteLine(
    $"Hotels B: {await hotelsB.CountDocumentsAsync(FilterDefinition<Hotel>.Empty)}");

Console.WriteLine("Seeder completed successfully!");
