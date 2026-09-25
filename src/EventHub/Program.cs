using System.Globalization;
using System.Text;
using EventHub;
using MongoDB.Bson;
using MongoDB.Driver;

Console.OutputEncoding = Encoding.UTF8;
CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("de-CH");
try
{
    var service = new EventHubService();
    if (args.Contains("--self-test")) { await IntegrationTests.Run(service); return; }
    if (args.Contains("--report")) { await Report(); return; }
    if (args.Contains("--events")) { await ShowEvents(1); return; }
    Console.WriteLine("\nEVENTHUB | Ticket- und Eventverwaltung | Modul 165");
    while (true)
    {
        Console.WriteLine("\n1 Events   2 Kunden   3 Tickets buchen   4 Meine Buchungen\n5 Bezahlen 6 Stornieren 7 Umsatz          8 Event anlegen\n9 Bewerten 0 Beenden");
        var choice = Read("Auswahl");
        if (choice == "0" || choice is null) break;
        try
        {
            switch (choice)
            {
                case "1": await ShowEvents(Number("Seite (1, 2, ...)")); break;
                case "2":
                    foreach (var c in await service.Customers()) Console.WriteLine($"{c["_id"]} | {c["firstName"]} {c["lastName"]} | {c["email"]}");
                    break;
                case "3":
                    var customer = Id("Kunden-ID (Menü 2)"); var ev = Id("Event-ID (Menü 1)");
                    var category = Required("Kategorie: Stehplatz / Sitzplatz / VIP"); var qty = Number("Anzahl (1–10)");
                    var reserve = Required("Reservieren (r) oder bezahlt buchen (b)");
                    if (reserve is not ("r" or "b")) throw new ArgumentException("Bitte r oder b eingeben.");
                    Console.WriteLine($"Buchung erfolgreich: {await service.Book(customer, ev, category, qty, reserve == "r")}");
                    break;
                case "4":
                    foreach (var b in await service.CustomerBookings(Id("Kunden-ID")))
                        Console.WriteLine($"{b["_id"]} | Event {b["eventId"]} | {b["quantity"]} × {b["category"]} | CHF {b["unitPrice"]} | {b["status"]}");
                    break;
                case "5": await service.ChangeBooking(Id("Buchungs-ID"), false); Console.WriteLine("Bezahlt."); break;
                case "6": await service.ChangeBooking(Id("Buchungs-ID"), true); Console.WriteLine("Storniert, Tickets wieder verfügbar."); break;
                case "7": await Report(); break;
                case "8":
                    foreach (var name in new[] { "locations", "organizers", "artists" })
                    {
                        Console.WriteLine(name + ":");
                        foreach (var d in await service.Collection(name).Find(FilterDefinition<BsonDocument>.Empty).ToListAsync())
                            Console.WriteLine($"{d["_id"]} | {d["name"]}");
                    }
                    var title = Required("Titel");
                    var date = DateTime.ParseExact(Required("Datum/Uhrzeit lokal (yyyy-MM-dd HH:mm)"), "yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);
                    var location = Id("Location-ID"); var organizer = Id("Veranstalter-ID");
                    var artists = Required("Künstler-IDs, mit Komma getrennt").Split(',').Select(x => ObjectId.Parse(x.Trim())).ToArray();
                    var capacity = Number("Stehplatz-Kapazität");
                    var price = decimal.Parse(Required("Preis CHF (z.B. 49.50)"), CultureInfo.InvariantCulture);
                    Console.WriteLine($"Event angelegt: {await service.CreateEvent(title, date, location, organizer, artists, capacity, price)}");
                    break;
                case "9":
                    await service.Review(Id("Kunden-ID"), Id("Event-ID"), Number("Sterne 1–5"), Read("Kommentar") ?? "");
                    Console.WriteLine("Bewertung gespeichert."); break;
                default: Console.WriteLine("Bitte 0–9 auswählen."); break;
            }
        }
        catch (MongoWriteException ex) when (ex.WriteError.Category == ServerErrorCategory.DuplicateKey)
        { Console.WriteLine("Dieser Eintrag existiert bereits (z.B. bereits bewertetes Event)."); }
        catch (Exception ex) { Console.WriteLine($"Fehler: {ex.Message}"); }
    }

    async Task ShowEvents(int page)
    {
        var events = await service.Events(page);
        Console.WriteLine($"\nEvents | Seite {page} | 5 pro Seite | Datum aufsteigend");
        foreach (var e in events)
        {
            Console.WriteLine($"\n{e["_id"]} | {e["title"]} | {e["date"].ToLocalTime():dd.MM.yyyy HH:mm} | {e["status"]}");
            foreach (var c in e["categories"].AsBsonArray)
                Console.WriteLine($"  {c["name"],-10} CHF {c["price"],7} | frei {c["available"],4} | reserviert {c["reserved"],3} | verkauft {c["sold"],3}");
        }
        if (events.Count == 0) Console.WriteLine("Keine weiteren Events.");
    }
    async Task Report()
    {
        Console.WriteLine("\nUmsatz pro Event | nur bezahlte Buchungen | CHF");
        foreach (var row in await service.Revenue()) Console.WriteLine($"{row["title"],-30} | Tickets {row["tickets"],4} | CHF {row["revenue"],10}");
    }
}
catch (Exception ex) { Console.Error.WriteLine($"EventHub: {ex.Message}"); Environment.ExitCode = 1; }

static string? Read(string label) { Console.Write(label + ": "); return Console.ReadLine()?.Trim(); }
static string Required(string label) => Read(label) is { Length: > 0 } s ? s : throw new ArgumentException("Eingabe fehlt.");
static int Number(string label) => int.TryParse(Required(label), out var n) ? n : throw new ArgumentException("Ganze Zahl erwartet.");
static ObjectId Id(string label) => ObjectId.TryParse(Required(label), out var id) ? id : throw new ArgumentException("ID muss 24 hexadezimale Zeichen enthalten.");
