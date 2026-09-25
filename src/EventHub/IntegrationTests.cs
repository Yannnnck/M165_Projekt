using MongoDB.Bson;
using MongoDB.Driver;

namespace EventHub;

/// <summary>Integrationstests gegen das echte Replica Set, mit eigenen aufgeräumten Testdaten.</summary>
public static class IntegrationTests
{
    public static async Task Run(EventHubService app)
    {
        var eventIds = new List<ObjectId>();
        var customerId = ObjectId.GenerateNewId();
        var customer = new BsonDocument { { "_id", customerId }, { "firstName", "Integration" },
            { "lastName", "Test" }, { "email", $"test-{customerId}@example.com" } };
        var location = await app.Collection("locations").Find(FilterDefinition<BsonDocument>.Empty).FirstAsync();
        var organizer = await app.Collection("organizers").Find(FilterDefinition<BsonDocument>.Empty).FirstAsync();
        var artist = await app.Collection("artists").Find(FilterDefinition<BsonDocument>.Empty).FirstAsync();
        try
        {
            await app.Collection("customers").InsertOneAsync(customer);
            var id = await NewEvent(3);
            await MustFail(() => app.Book(customerId, id, "Stehplatz", 1, simulateFailure: true), "Rollback nach Insert");
            Check((await Event(id))["categories"][0]["available"].AsInt32 == 3, "Rollback: Bestand unverändert");
            Check(await app.Collection("bookings").CountDocumentsAsync(new BsonDocument("eventId", id)) == 0, "Rollback: keine Buchung gespeichert");
            await MustFail(() => app.Book(ObjectId.GenerateNewId(), id, "Stehplatz", 1), "Unbekannter Kunde abgewiesen");
            await MustFail(() => app.Book(customerId, id, "Stehplatz", 0), "Null Tickets abgewiesen");
            await MustFail(() => app.Book(customerId, id, "VIP", 1), "Unbekannte Kategorie abgewiesen");

            // Zehn verschiedene Sessions konkurrieren um drei Plätze.
            var attempts = await Task.WhenAll(Enumerable.Range(0, 10).Select(async _ =>
            {
                try { return (ObjectId?)await app.Book(customerId, id, "Stehplatz", 1); }
                catch (InvalidOperationException ex) when (ex.Message.Contains("nicht buchbar")) { return null; }
            }));
            Check(attempts.Count(x => x.HasValue) == 3, "Parallelbuchung: genau 3 von 10 erfolgreich");
            var ev = await Event(id);
            Check(ev["categories"][0]["available"].AsInt32 == 0 && ev["categories"][0]["sold"].AsInt32 == 3 && ev["status"] == "ausverkauft", "Kein Überverkauf, Status ausverkauft");
            var bookingId = attempts.First(x => x.HasValue)!.Value;
            await app.ChangeBooking(bookingId, true);
            ev = await Event(id);
            Check(ev["categories"][0]["available"].AsInt32 == 1 && ev["status"] == "geplant", "Storno gibt Platz frei und öffnet Event");
            await MustFail(() => app.ChangeBooking(bookingId, true), "Doppeltes Storno abgewiesen");
            var reserved = await app.Book(customerId, id, "Stehplatz", 1, reserve: true);
            Check((await Event(id))["categories"][0]["reserved"].AsInt32 == 1, "Reservierung zählt separat");
            await app.ChangeBooking(reserved, false);
            ev = await Event(id);
            Check(ev["categories"][0]["reserved"].AsInt32 == 0 && ev["categories"][0]["sold"].AsInt32 == 3, "Bezahlen verschiebt reserviert zu verkauft");
            await MustFail(() => app.ChangeBooking(reserved, false), "Doppelte Zahlung abgewiesen");
            await MustFail(() => app.Review(customerId, id, 5, "zu früh"), "Zukünftiges Event nicht bewertbar");
            await app.Collection("events").UpdateOneAsync(new BsonDocument("_id", id), new BsonDocument("$set", new BsonDocument {
                { "date", DateTime.UtcNow.AddDays(-1) }, { "status", "durchgeführt" }
            }));
            await app.Review(customerId, id, 5, "Integrationstest");
            Check(await app.Collection("reviews").CountDocumentsAsync(new BsonDocument("eventId", id)) == 1, "Bewertung nach Durchführung gespeichert");
            await MustFail(() => app.Review(customerId, id, 4, "doppelt"), "Doppelte Bewertung verhindert");
            await MustFail(() => app.Book(customerId, id, "Stehplatz", 1), "Vergangenes Event nicht buchbar");
            await MustFail(() => app.Collection("bookings").InsertOneAsync(new BsonDocument("status", "falsch")), "Schema weist ungültige Buchung ab");
            await MustFail(() => app.Collection("customers").InsertOneAsync(new BsonDocument {
                { "firstName", "Duplicate" }, { "lastName", "Test" }, { "email", customer["email"] }
            }), "E-Mail-Unique-Index geprüft");
            var page1 = await app.Events(1, 2); var page2 = await app.Events(2, 2);
            Check(page1.Count == 2 && page2.Count == 2 && !page1.Select(x => x["_id"]).Intersect(page2.Select(x => x["_id"])).Any(), "Paginierung ohne Überschneidung");
            var revenue = await app.Revenue();
            var row = revenue.Single(x => x["title"] == $"Integrationstest {id}");
            Check(Decimal128.ToDecimal(row["revenue"].AsDecimal128) == 150m, "Aggregation: exakt CHF 150 für drei bezahlte Tickets");
            Console.WriteLine("ALLE .NET-INTEGRATIONSTESTS BESTANDEN.");
        }
        finally
        {
            var eventFilter = new BsonDocument("eventId", new BsonDocument("$in", new BsonArray(eventIds)));
            await app.Collection("reviews").DeleteManyAsync(eventFilter);
            await app.Collection("bookings").DeleteManyAsync(eventFilter);
            await app.Collection("events").DeleteManyAsync(new BsonDocument("_id", new BsonDocument("$in", new BsonArray(eventIds))));
            await app.Collection("customers").DeleteOneAsync(new BsonDocument("_id", customerId));
        }
        async Task<ObjectId> NewEvent(int capacity)
        {
            var id = await app.CreateEvent("Integrationstest", DateTime.UtcNow.AddDays(3), location["_id"].AsObjectId,
                organizer["_id"].AsObjectId, [artist["_id"].AsObjectId], capacity);
            eventIds.Add(id);
            await app.Collection("events").UpdateOneAsync(new BsonDocument("_id", id),
                new BsonDocument("$set", new BsonDocument("title", $"Integrationstest {id}")));
            return id;
        }
        Task<BsonDocument> Event(ObjectId id) => app.Collection("events").Find(new BsonDocument("_id", id)).FirstAsync();
    }

    private static void Check(bool condition, string name)
    { if (!condition) throw new Exception($"TEST FEHLGESCHLAGEN: {name}"); Console.WriteLine($"PASS | {name}"); }

    private static async Task MustFail(Func<Task> action, string name)
    {
        try { await action(); }
        catch (Exception ex) when (ex is InvalidOperationException or ArgumentException or MongoWriteException or MongoCommandException)
        { Console.WriteLine($"PASS | {name}"); return; }
        throw new Exception($"TEST FEHLGESCHLAGEN: {name} wurde nicht abgewiesen.");
    }
}
