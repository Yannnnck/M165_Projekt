using MongoDB.Bson;
using MongoDB.Driver;

namespace EventHub;

/// <summary>Fachlogik und MongoDB-Zugriffe. Geld wird als Decimal128 in CHF gespeichert.</summary>
public sealed class EventHubService
{
    public MongoClient Client { get; }
    public IMongoDatabase Database { get; }
    public IMongoCollection<BsonDocument> Collection(string name) => Database.GetCollection<BsonDocument>(name);
    private static readonly TransactionOptions TransactionSettings = new(
        readConcern: ReadConcern.Snapshot, readPreference: ReadPreference.Primary, writeConcern: WriteConcern.WMajority);

    public EventHubService()
    {
        var password = Environment.GetEnvironmentVariable("EVENTHUB_SALES_PASSWORD")
            ?? throw new InvalidOperationException("Bitte mit scripts/Run.ps1 starten (Zugangsdaten fehlen).");
        var hosts = Environment.GetEnvironmentVariable("EVENTHUB_HOSTS") ?? "localhost:27101,localhost:27102,localhost:27103";
        Client = new MongoClient($"mongodb://sales:{Uri.EscapeDataString(password)}@{hosts}/eventhub?authSource=admin&replicaSet=eventhub-rs&retryWrites=true&w=majority&serverSelectionTimeoutMS=30000");
        Database = Client.GetDatabase("eventhub");
    }

    /// <summary>AO5: Sortierung und Paginierung laufen auf dem Datenbankserver; _id löst Datumsgleichstände auf.</summary>
    public Task<List<BsonDocument>> Events(int page = 1, int size = 5)
    {
        if (page < 1 || page > 100000 || size < 1 || size > 50) throw new ArgumentException("Seite 1–100000, Seitengrösse 1–50.");
        return Collection("events").Find(FilterDefinition<BsonDocument>.Empty)
            .Sort(new BsonDocument { { "date", 1 }, { "_id", 1 } }).Skip((page - 1) * size).Limit(size).ToListAsync();
    }

    public Task<List<BsonDocument>> Customers() => Collection("customers").Find(FilterDefinition<BsonDocument>.Empty)
        .Sort(new BsonDocument("lastName", 1)).ToListAsync();

    public Task<List<BsonDocument>> CustomerBookings(ObjectId customerId) => Collection("bookings")
        .Find(new BsonDocument("customerId", customerId)).Sort(new BsonDocument("bookedAt", -1)).ToListAsync();

    /// <summary>A12: Bestandsänderung und Buchung bilden eine atomare Multi-Document-Transaktion.</summary>
    public async Task<ObjectId> Book(ObjectId customerId, ObjectId eventId, string category, int quantity,
        bool reserve = false, bool simulateFailure = false)
    {
        if (quantity is < 1 or > 10) throw new ArgumentException("Pro Buchung sind 1–10 Tickets erlaubt.");
        var bookingId = ObjectId.GenerateNewId(); // Bleibt bei einem Treiber-Retry gleich.
        using var session = await Client.StartSessionAsync();
        return await session.WithTransactionAsync(async (s, ct) =>
        {
            if (!await Collection("customers").Find(s, new BsonDocument("_id", customerId)).AnyAsync(ct))
                throw new InvalidOperationException("Kunde existiert nicht.");
            var filter = new BsonDocument {
                { "_id", eventId }, { "status", "geplant" }, { "date", new BsonDocument("$gt", DateTime.UtcNow) },
                { "categories", new BsonDocument("$elemMatch", new BsonDocument {
                    { "name", category }, { "available", new BsonDocument("$gte", quantity) } }) }
            };
            // Bedingtes Update verhindert negative Bestände auch bei konkurrierenden Verkäufen.
            var update = new BsonDocument("$inc", new BsonDocument {
                { "categories.$.available", -quantity }, { reserve ? "categories.$.reserved" : "categories.$.sold", quantity }
            });
            var ev = await Collection("events").FindOneAndUpdateAsync(s, filter, update,
                new FindOneAndUpdateOptions<BsonDocument> { ReturnDocument = ReturnDocument.After }, ct);
            if (ev is null) throw new InvalidOperationException("Event nicht buchbar oder zu wenig freie Tickets.");
            var ticket = ev["categories"].AsBsonArray.Select(x => x.AsBsonDocument).Single(x => x["name"] == category);
            await Collection("bookings").InsertOneAsync(s, new BsonDocument {
                { "_id", bookingId }, { "customerId", customerId }, { "eventId", eventId }, { "category", category },
                { "quantity", quantity }, { "unitPrice", ticket["price"] }, { "bookedAt", DateTime.UtcNow },
                { "status", reserve ? "reserviert" : "bezahlt" }
            }, cancellationToken: ct);
            if (simulateFailure) throw new InvalidOperationException("Absichtlicher Fehler: vollständiger Rollback.");
            if (ev["categories"].AsBsonArray.All(x => x["available"].AsInt32 == 0))
                await Collection("events").UpdateOneAsync(s, new BsonDocument("_id", eventId),
                    new BsonDocument("$set", new BsonDocument("status", "ausverkauft")), cancellationToken: ct);
            return bookingId;
        }, TransactionSettings);
    }

    /// <summary>Reservierung bezahlen oder aktive Buchung stornieren; Zähler ändern sich atomar mit.</summary>
    public async Task ChangeBooking(ObjectId bookingId, bool cancel)
    {
        using var session = await Client.StartSessionAsync();
        await session.WithTransactionAsync(async (s, ct) =>
        {
            var booking = await Collection("bookings").Find(s, new BsonDocument("_id", bookingId)).FirstOrDefaultAsync(ct)
                ?? throw new InvalidOperationException("Buchung existiert nicht.");
            var oldStatus = booking["status"].AsString;
            if (oldStatus == "storniert" || (!cancel && oldStatus != "reserviert"))
                throw new InvalidOperationException("Dieser Statuswechsel ist nicht erlaubt.");
            var eventId = booking["eventId"].AsObjectId;
            var ev = await Collection("events").Find(s, new BsonDocument("_id", eventId)).FirstOrDefaultAsync(ct)
                ?? throw new InvalidOperationException("Event existiert nicht.");
            if (ev["date"].ToUniversalTime() <= DateTime.UtcNow || ev["status"] == "durchgeführt")
                throw new InvalidOperationException("Vergangene Events können nicht mehr geändert werden.");
            if (!cancel && ev["status"] == "abgesagt") throw new InvalidOperationException("Event ist abgesagt.");
            var quantity = booking["quantity"].AsInt32;
            var source = oldStatus == "reserviert" ? "reserved" : "sold";
            var increment = new BsonDocument { { $"categories.$.{source}", -quantity },
                { cancel ? "categories.$.available" : "categories.$.sold", quantity } };
            var update = new BsonDocument("$inc", increment);
            if (cancel && ev["status"] == "ausverkauft") update.Add("$set", new BsonDocument("status", "geplant"));
            var result = await Collection("events").UpdateOneAsync(s, new BsonDocument {
                { "_id", eventId }, { "categories", new BsonDocument("$elemMatch", new BsonDocument {
                    { "name", booking["category"] }, { source, new BsonDocument("$gte", quantity) } }) }
            }, update, cancellationToken: ct);
            if (result.ModifiedCount != 1) throw new InvalidOperationException("Bestand passt nicht zur Buchung.");
            await Collection("bookings").UpdateOneAsync(s, new BsonDocument("_id", bookingId),
                new BsonDocument("$set", new BsonDocument("status", cancel ? "storniert" : "bezahlt")), cancellationToken: ct);
            return true;
        }, TransactionSettings);
    }

    public async Task<ObjectId> CreateEvent(string title, DateTime date, ObjectId locationId,
        ObjectId organizerId, ObjectId[] artistIds, int capacity = 100, decimal price = 50m)
    {
        if (string.IsNullOrWhiteSpace(title) || date.ToUniversalTime() <= DateTime.UtcNow || capacity < 1 || price < 0)
            throw new ArgumentException("Titel, zukünftiges Datum, positive Kapazität und gültiger Preis nötig.");
        var location = await Collection("locations").Find(new BsonDocument("_id", locationId)).FirstOrDefaultAsync()
            ?? throw new InvalidOperationException("Location existiert nicht.");
        if (capacity > location["capacity"].AsInt32) throw new ArgumentException("Location ist zu klein.");
        if (!await Collection("organizers").Find(new BsonDocument("_id", organizerId)).AnyAsync())
            throw new ArgumentException("Veranstalter fehlt.");
        if (artistIds.Length is < 1 or > 50 || artistIds.Distinct().Count() != artistIds.Length ||
            await Collection("artists").CountDocumentsAsync(new BsonDocument("_id", new BsonDocument("$in", new BsonArray(artistIds)))) != artistIds.Length)
            throw new ArgumentException("Künstler müssen existieren und eindeutig sein.");
        var id = ObjectId.GenerateNewId();
        await Collection("events").InsertOneAsync(new BsonDocument {
            { "_id", id }, { "title", title.Trim() }, { "description", $"Live-Event: {title.Trim()}" },
            { "date", date.ToUniversalTime() }, { "locationId", locationId }, { "organizerId", organizerId },
            { "artistIds", new BsonArray(artistIds) }, { "status", "geplant" },
            { "categories", new BsonArray { new BsonDocument {
                { "name", "Stehplatz" }, { "price", new Decimal128(price) }, { "capacity", capacity },
                { "available", capacity }, { "reserved", 0 }, { "sold", 0 }
            } } }
        });
        return id;
    }

    public async Task Review(ObjectId customerId, ObjectId eventId, int rating, string comment)
    {
        if (rating is < 1 or > 5 || comment.Length > 2000) throw new ArgumentException("1–5 Sterne, maximal 2000 Zeichen.");
        using var session = await Client.StartSessionAsync();
        await session.WithTransactionAsync(async (s, ct) =>
        {
            var ev = await Collection("events").Find(s, new BsonDocument {
                { "_id", eventId }, { "status", "durchgeführt" }, { "date", new BsonDocument("$lt", DateTime.UtcNow) }
            }).FirstOrDefaultAsync(ct);
            if (ev is null) throw new InvalidOperationException("Nur durchgeführte, vergangene Events sind bewertbar.");
            if (!await Collection("customers").Find(s, new BsonDocument("_id", customerId)).AnyAsync(ct) ||
                !await Collection("bookings").Find(s, new BsonDocument { { "customerId", customerId }, { "eventId", eventId }, { "status", "bezahlt" } }).AnyAsync(ct))
                throw new InvalidOperationException("Nur Kunden mit bezahltem Ticket dürfen bewerten.");
            await Collection("reviews").InsertOneAsync(s, new BsonDocument {
                { "customerId", customerId }, { "eventId", eventId }, { "rating", rating },
                { "comment", comment }, { "createdAt", DateTime.UtcNow }
            }, cancellationToken: ct);
            return true;
        }, TransactionSettings);
    }

    public Task<List<BsonDocument>> Revenue() => Collection("bookings").Aggregate<BsonDocument>(new[] {
        BsonDocument.Parse("{ $match: {status: 'bezahlt'} }"),
        BsonDocument.Parse("{ $group: {_id: '$eventId', revenue: {$sum: {$multiply: ['$quantity','$unitPrice']}}, tickets: {$sum:'$quantity'}} }"),
        BsonDocument.Parse("{ $lookup: {from:'events',localField:'_id',foreignField:'_id',as:'event'} }"),
        BsonDocument.Parse("{ $unwind: '$event' }"),
        BsonDocument.Parse("{ $project: {_id:0,title:'$event.title',revenue:1,tickets:1} }"),
        BsonDocument.Parse("{ $sort: {revenue:-1,title:1} }")
    }).ToListAsync();
}
