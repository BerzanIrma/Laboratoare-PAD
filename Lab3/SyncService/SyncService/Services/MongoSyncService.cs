using MongoDB.Driver;
using SyncService.Models;

namespace SyncService.Services;

public class MongoSyncService
{
    private readonly IMongoCollection<Hotel> _hotelsA;
    private readonly IMongoCollection<Hotel> _hotelsB;

    public MongoSyncService(string connectionA, string connectionB)
    {
        var clientA = new MongoClient(connectionA);
        var clientB = new MongoClient(connectionB);

        _hotelsA = clientA.GetDatabase("hotelsdb").GetCollection<Hotel>("hotels");
        _hotelsB = clientB.GetDatabase("hotelsdb").GetCollection<Hotel>("hotels");
    }

    public async Task RunAsync(CancellationToken token)
    {
        Console.WriteLine("Starting MongoDB synchronization...");

        // daca o directie crapa, o oprim si pe cealalta, ca sa reincercam totul de la capat
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(token);

        var aToB = MirrorAsync(_hotelsA, _hotelsB, cts.Token);
        var bToA = MirrorAsync(_hotelsB, _hotelsA, cts.Token);

        var first = await Task.WhenAny(aToB, bToA);
        cts.Cancel();
        await first;   // arunca mai departe eroarea, daca a fost una
    }

    private async Task MirrorAsync(
        IMongoCollection<Hotel> source,
        IMongoCollection<Hotel> destination,
        CancellationToken token)
    {
        // 1. copiem ce exista deja
        var hotels = await source.Find(FilterDefinition<Hotel>.Empty).ToListAsync(token);
        foreach (var hotel in hotels)
            await CopyIfNewerAsync(hotel, destination, token);

        Console.WriteLine("Initial synchronization completed.");

        // 2. urmarim modificarile noi (change stream - merge doar pe replica set)
        var options = new ChangeStreamOptions
        {
            FullDocument = ChangeStreamFullDocumentOption.UpdateLookup
        };

        using var cursor = await source.WatchAsync(options: options, cancellationToken: token);

        while (await cursor.MoveNextAsync(token))
        {
            foreach (var change in cursor.Current)
            {
                if (change.OperationType == ChangeStreamOperationType.Delete)
                {
                    var id = change.DocumentKey["_id"].AsObjectId.ToString();
                    await destination.DeleteOneAsync(x => x.Id == id, token);
                }
                else if (change.FullDocument != null)
                {
                    await CopyIfNewerAsync(change.FullDocument, destination, token);
                }
            }
        }
    }

    // copiem doar daca versiunea din sursa e mai noua (last-write-wins dupa UpdatedAt)
    private static async Task CopyIfNewerAsync(
        Hotel hotel,
        IMongoCollection<Hotel> destination,
        CancellationToken token)
    {
        var existing = await destination.Find(x => x.Id == hotel.Id).FirstOrDefaultAsync(token);

        if (existing == null || hotel.UpdatedAt > existing.UpdatedAt)
        {
            await destination.ReplaceOneAsync(
                x => x.Id == hotel.Id,
                hotel,
                new ReplaceOptions { IsUpsert = true },
                token);

            Console.WriteLine($"Synchronized hotel: {hotel.Name}");
        }
    }
}