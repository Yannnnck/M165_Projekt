# EventHub verstehen

## 1. Die Idee in zwei Minuten

Ein Event ist ein MongoDB-Dokument, etwa „Neon Pop Night“. Es enthält die drei
Ticketkategorien mit ihren Preisen und Beständen. Die Location und die Künstler
stehen in eigenen Collections; im Event stehen ihre IDs. Eine Buchung sagt:
„Kunde A hat zwei VIP-Tickets für Event B zum damaligen Preis gekauft.“

`Program.cs` fragt im Terminal nach Eingaben. `EventHubService.cs` prüft die Regeln
und ruft über `MongoDB.Driver` die Datenbank auf. `scripts/Run.ps1` lädt vorher die
lokalen Zugangsdaten. Deshalb muss kein Passwort im C#-Code stehen.

## 2. Die Dateien in sinnvoller Lesereihenfolge

1. `mongodb/02-seed.js`: Hier siehst du konkrete Dokumente und die Verbindungen.
2. `mongodb/01-schema.js`: Welche Felder und Datentypen sind erlaubt?
3. `EventHubService.Events`: Ein einfacher Lesezugriff mit Sortierung und Paginierung.
4. `EventHubService.Book`: Der zentrale Ablauf beim Ticketkauf.
5. `mongodb/06-aggregations.js`: Wie aus Buchungen eine Statistik wird.
6. `scripts/Setup.ps1` und `Failover.ps1`: Wie die drei Knoten zusammenarbeiten.

`BsonDocument` ist hier ein C#-Objekt für ein MongoDB-Dokument. Die Feldnamen entsprechen
direkt den Shell-Skripten. Das macht den Vergleich einfach. In einem grösseren Projekt
wären typisierte C#-Modellklassen eine mögliche Weiterentwicklung.

## 3. Einen Ticketkauf Schritt für Schritt erklären

Beispiel: ein VIP-Ticket, CHF 120, bisher 38 frei.

| Schritt | Bedeutung |
| --- | --- |
| Session starten | Die Operationen erhalten einen gemeinsamen Transaktionskontext. |
| Kunde prüfen | Ohne existierenden Kunden wird nichts gebucht. |
| Bedingtes Event-Update | Event muss geplant, zukünftig und mit genügend freien VIP-Plätzen vorhanden sein. |
| Bestand ändern | `available` sinkt auf 37, `sold` steigt um eins (bei Reservierung `reserved`). |
| Buchung einfügen | Kunden-ID, Event-ID, Menge und Preis werden gespeichert. |
| Commit | Beide Änderungen werden zusammen bestätigt. |
| Fehlerfall | Ein Abort nimmt alle Änderungen dieser Transaktion zurück. |

Entscheidend ist der Filter im Update. Nur „Bestand lesen, in C# minus eins rechnen,
zurückschreiben“ wäre unter Konkurrenz falsch. Zwei Käufer könnten denselben alten
Wert sehen. Unser Update prüft die verfügbare Menge beim Schreiben, und die
Transaktion behandelt konkurrierende Änderungen mit Write Conflicts und Retries.

Jeder Datenbankaufruf innerhalb des Callbacks erhält dieselbe Session `s`. Ein
versehentlich ohne Session ausgeführter Insert wäre nicht Teil dieser Transaktion.
`WithTransactionAsync` ist deshalb nicht einfach ein dekorativer Wrapper.

## 4. Was macht die Umsatz-Aggregation?

`$match` nimmt nur bezahlte Buchungen. `$group` fasst sie nach `eventId` zusammen
und addiert `quantity × unitPrice`. `$lookup` holt das Event dazu. `$unwind` macht
aus dem Event-Array ein einzelnes Dokument. `$project` wählt Titel und Kennzahlen,
`$sort` sortiert nach Umsatz.

Eine Reservierung ist noch kein Umsatz. Eine stornierte Buchung zählt ebenfalls
nicht. Decimal128 verhindert die typischen binären Rundungsprobleme von `double`
bei Geldbeträgen. Es ist dennoch kein echtes Zahlungs- oder Buchhaltungssystem.

## 5. Was leisten Validator und Index?

Ein Validator lehnt z.B. eine Buchung ohne `customerId`, mit falschem Status oder
mit Menge null ab. Die zusätzliche Event-Regel kontrolliert die Bestandssumme und
eindeutige Kategorienamen. Ein Validator verhindert aber keine Referenz auf einen
nicht existierenden Kunden in einer anderen Collection.

Ein Index ist eine zusätzliche Suchstruktur. Für „Location X, Termine ab heute“
kann die Datenbank direkt den passenden Teil des Compound-Index untersuchen.
`explain('executionStats')` zeigt bei den Seed-Daten 2 statt 6 untersuchte Events.
Der Unique-Index für Bewertungen ist zusätzlich eine Regel gegen Duplikate.

## 6. Warum drei Knoten?

Einer ist Primary, zwei sind Secondaries. Alle speichern Daten; es gibt keinen
datenlosen Arbiter. Zwei von drei bilden die Mehrheit. Fällt der Primary aus, können
die beiden anderen einen neuen wählen. Während der Wahl kann das System kurz warten.
Der Treiber kennt alle drei Hosts und kann den neuen Primary finden.

Ein Replica Set ist kein Backup: Ein versehentliches Löschen wird ebenfalls
repliziert. `mongodump` erzeugt dagegen eine separate Sicherung. Wir beweisen die
Wiederherstellbarkeit, indem wir diese Sicherung in einer anderen Datenbank einspielen.

## 7. Kleine Übungen vor dem Video

1. Zeige in den Seed-Daten eine Referenz und ein eingebettetes Objekt. Begründe beide.
2. Reserviere ein Ticket. Vergleiche Bestand und Umsatz vor und nach dem Bezahlen.
3. Storniere die Buchung. Erkläre, warum sie gespeichert bleibt, obwohl der Platz frei ist.
4. Führe `Run.ps1 --self-test` aus. Suche den absichtlichen Fehler im C#-Code und erkläre den Rollback.
5. Führe `08-explain.js` aus. Suche `COLLSCAN`, `IXSCAN`, `docsExamined` und `TEXT_MATCH`.
6. Führe `Failover.ps1` aus. Benenne alten und neuen Primary und erkläre die Mehrheit.
7. Öffne eine neue Eventseite im Menü. Zeige `Sort`, `Skip` und `Limit` im Code.

## 8. Typische Fragen im Fachgespräch

| Frage | Kerngedanke für deine Antwort |
| --- | --- |
| Warum keine Buchungen direkt im Event? | Unbegrenztes Wachstum, 16 MiB, gemeinsame Schreiblast; eigene Collection mit Referenzen. |
| Warum keine Buchungs-ID-Liste im Kunden? | Auch diese Liste würde unbegrenzt wachsen; Abfrage über `customerId` reicht. |
| Weshalb zusätzlich eine Transaktion? | Ein atomarer Update betrifft nur ein Dokument; der Verkauf betrifft Event und Buchung. |
| Was passiert bei einem Fehler zwischen Update und Insert? | Innerhalb der Transaktion wird alles zurückgerollt. |
| Was geschieht bei zehn Käufern für drei Plätze? | Bedingtes Update und Konfliktbehandlung lassen nur drei Käufe durch. |
| Was passiert, wenn zwei Knoten ausfallen? | Keine Mehrheit, keine neuen mehrheitsbestätigten Verkäufe. |
| Sind Replica Set und Sharding dasselbe? | Replikation kopiert; Sharding verteilt unterschiedliche Datenmengen. |
| Warum ist das lokale Setup nicht vollständig ausfallsicher? | Alle Prozesse hängen von demselben Rechner ab. |
| Ist MongoDB schemalos? | Flexibles Schema ist möglich; hier erzwingen wir bewusst Validatoren. |
| Warum Snapshot statt „alles ist immer gleich“? | Eine Transaktion braucht eine konsistente Sicht; Secondaries können trotzdem hinterher sein. |

Antworte im Video in eigenen Worten. Wenn du diese Abläufe selbst vorführen kannst,
kannst du auch erklären, was der Code tatsächlich leistet.
