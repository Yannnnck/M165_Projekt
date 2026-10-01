# Projektdokumentation EventHub

## Ausgangslage und Ziel

EventHub verwaltet Musikveranstaltungen, Veranstaltungsorte, Künstler, Kunden,
Tickets und Bewertungen. Viele Buchungen müssen getrennt von den vergleichsweise
kleinen Eventdaten gespeichert werden. Ein Verkauf darf weder eine Buchung ohne
Bestandsänderung noch eine Bestandsänderung ohne Buchung hinterlassen. Ein einzelner
ausgefallener Datenbankprozess soll nach einer Neuwahl nicht zum dauerhaften Stillstand führen.

Ergebnis ist eine lokale Windows-Lernanwendung in C# mit sieben MongoDB-Collections,
Schema-Validierung, Testdaten, Abfragen, Auswertungen und Administrationsskripten.
Die Oberfläche ist bewusst ein einfaches Konsolenmenü entsprechend dem Auftrag.

## Vorgehen nach IPERKA

| Phase | Vorgehen und Ergebnis |
| --- | --- |
| Informieren | Vollständigen Projektauftrag und Kurskapitel M4, M7, M8 und S2 gelesen; Anforderungen A1–A15 sowie Zusatzpunkte geprüft. Installierte Werkzeuge ermittelt. |
| Planen | Datenmodell mit sieben Collections, Projektstruktur, drei lokalen Ports und Testfälle geplant. Abgabe in einfache Markdown-Dateien aufgeteilt. |
| Entscheiden | C#/.NET 10 mit offiziellem Treiber; natives Windows-Replica-Set; Kategorien eingebettet, Buchungen referenziert; AO2 und AO5 gewählt. |
| Realisieren | Reproduzierbares Setup, Validatoren, Seed-Daten, Indexe, Rollen, Shell-Demos und Konsolenanwendung implementiert. Transaktionen für Buchung, Zahlung und Storno ergänzt. |
| Kontrollieren | Auf echter MongoDB getestet: Rollback, parallele Käufe, Rollen, Datenintegrität, Restore und Prozessausfall. Gefundene Fehler behoben und erneut geprüft. Protokoll liegt unter `docs/evidence/`. |
| Auswerten | Testergebnisse ausgewertet und technisches Fazit sowie Grenzen der Lösung dokumentiert. |

Es werden keine rückwirkend geschätzten Arbeitsstunden als echte Zeiterfassung angegeben.
Lokale Git-Commits gruppieren die tatsächlich erstellten Arbeitsergebnisse.

## Aufbau

```text
PowerShell-Starter
  ├─ drei mongod-Prozesse: localhost:27101 / 27102 / 27103
  ├─ mongosh-Skripte: Schema, Daten, Abfragen, Administration
  └─ .NET-Konsole
       ├─ Program.cs: Menü und Eingaben
       ├─ EventHubService.cs: Fachlogik und Datenbankzugriffe
       └─ IntegrationTests.cs: Tests auf dem echten Replica Set
```

Das Datenmodell und die Entscheidungen pro Beziehung sind in
[DATENMODELL.md](DATENMODELL.md) dokumentiert.

## Anforderungsnachweis

| Nr. | Umsetzung | Nachweis |
| --- | --- | --- |
| A1 | Sieben Collections, grafisches Modell | `DATENMODELL.md`, `datenmodell.svg` |
| A2 | Embedding/Referencing für jede Beziehung; 16-MiB-Limit und Wachstum | `DATENMODELL.md` |
| A3 | Validatoren für alle sieben Collections, Pflichtfelder, Typen, enum und Bestandsregel | `mongodb/01-schema.js`, `09-verify.js` |
| A4 | 6 Events, 3 Locations, 10 Künstler, 30 Kunden, 120 Buchungen, 5 Bewertungen, 2 Veranstalter | `mongodb/02-seed.js`, Testprotokoll |
| A5 | Je zwei Create-, Read-, Update- und Delete-Beispiele | `mongodb/07-crud.js` |
| A6 | Fünf Abfragen mit Vergleichen, `$elemMatch` und Textsuche | `mongodb/05-queries.js` |
| A7 | Umsatz pro Event und gebuchte Kategorien pro Stadt; jeweils mehrstufig | `mongodb/06-aggregations.js`; C#-Umsatzanzeige |
| A8 | Sieben zusätzliche Indexe; Compound/Text; drei Explain-Nachweise | `mongodb/03-indexes.js`, `08-explain.js`, `TESTS.md` |
| A9 | Admin, Verkauf und Reporting mit aktivierter Authentifizierung | `bootstrap.js`, `04-users.js`, `10-role-test.js` |
| A10 | Tatsächlicher Dump und Restore mit Inhalts-/Metadatenvergleich | `scripts/Backup-Restore.ps1`, Testprotokoll |
| A11 | Drei Knoten, echter Primary-Prozessausfall, Neuwahl und Wiederanlauf | `scripts/Setup.ps1`, `Failover.ps1`, Testprotokoll |
| A12 | Multi-Document-Transaktionen, Rollback- und Konkurrenztest | `EventHubService.Book`, `IntegrationTests.cs` |
| A13 | .NET-Konsole mit neun Menüfunktionen und offiziellem MongoDB.Driver | `src/EventHub/`, `scripts/Run.ps1` |
| A14 | Ungefähr eine Seite CAP-/BASE-Reflexion | `CAP-BASE.md` |
| A15 | Ausgangslage, IPERKA, Modell, Ergebnisse und Fazit | Diese Datei und verlinkte Markdown-Dateien |
| AO2 | Begründetes Sharding-Konzept für Buchungen | `SHARDING.md` |
| AO5 | Serverseitiges Sort/Skip/Limit mit stabiler Reihenfolge | `EventHubService.Events`, Test zur Paginierung |

## Fachliche Regeln

- Ein Event kann nur vor seinem Termin und im Status `geplant` gebucht werden.
- Pro Buchung sind 1–10 Tickets einer Kategorie erlaubt. Der Preis wird beim Kauf
  in der Buchung gespeichert; spätere Preisänderungen verändern vergangene Umsätze nicht.
- `capacity = available + reserved + sold` gilt für jede Kategorie. Reserviert
  bedeutet noch nicht bezahlt. Stornierte Buchungen belegen keine Plätze.
- Sind alle Plätze vergeben, wird das Event `ausverkauft`. Ein Storno kann es wieder öffnen.
- Der Umsatz berücksichtigt nur bezahlte Buchungen und verwendet Decimal128.
- Bewertungen sind nur für durchgeführte, vergangene Events mit bezahltem Ticket
  erlaubt. Pro Kunde/Event ist eine Bewertung möglich.
- Referenzen sind ObjectIds. MongoDB erzwingt keine Fremdschlüssel: Die Anwendung
  prüft Referenzen, und das Kontrollskript prüft den gesamten Testdatenbestand.

## Ergebnisse und technisches Fazit

Das System wurde lokal gestartet, gebaut und getestet. Die wichtigsten Ergebnisse
stehen in [TESTS.md](TESTS.md), die Rohdaten im [Testprotokoll](evidence/latest-test.txt).
Zehn parallele Käufe konnten drei Tickets nicht überverkaufen. Bei einem Fehler nach
dem Buchungs-Insert blieb weder eine Buchung noch ein veränderter Bestand zurück.
Der Restore stimmt in Dokumentinhalt, Indexen und Validatoren mit dem Manifest überein.
Nach dem Ausfall eines Primary konnte die Anwendung auf zwei Knoten weiterarbeiten.

Der grösste technische Aufwand lag bei Authentifizierung und Replica-Set-Lebenszyklus
sowie der konsistenten Änderung mehrerer Dokumente. Gleiche Knotenprioritäten vermeiden
einen unnötigen erneuten Primary-Wechsel beim Wiederanlauf. Ein weiterer Lernpunkt ist,
dass mongosh bei unerlaubten Zugriffen eine Exception auslösen kann: Tests müssen genau
diesen Fehler behandeln und dürfen einen beliebigen Fehlschlag nicht als Erfolg werten.

Die Lernanwendung besitzt keine echte Zahlungsanbindung, automatische Reservierungsfrist
oder öffentliche Weboberfläche. Alle drei Knoten auf einem Laptop schützen gegen einen
Prozessausfall, nicht gegen den Ausfall des gesamten Computers. Die Verkaufsrolle
`readWrite` darf direkte Änderungen ausführen; eine Produktionslösung würde den
Zugriff über einen Dienst kapseln und stärker eingrenzen. Näheres zu den Grenzen
steht in `TESTS.md` und `CAP-BASE.md`.

## Quellen

- [Projektauftrag](https://github.com/INFEFZ/Modul165/blob/main/G4_Exams/projektauftrag-eventhub-mongodb.md)
- [M4: Modellierung](https://github.com/INFEFZ/Modul165/tree/main/M4_Modeling)
- [M7: Administration](https://github.com/INFEFZ/Modul165/tree/main/M7_Admin)
- [M8: .NET-Treiber](https://github.com/INFEFZ/Modul165/tree/main/M8_Drivers)
- [S2: CAP und BASE](https://github.com/INFEFZ/Modul165/tree/main/S2_CAP)
- [MongoDB: Transaktionen mit C#](https://www.mongodb.com/docs/drivers/csharp/current/crud/transactions/)
- [MongoDB: Replica Set mit Keyfile](https://www.mongodb.com/docs/manual/tutorial/deploy-replica-set-with-keyfile-access-control/)

KI-Unterstützung: Implementierung, Dokumentationsentwurf und Testdurchführung mit
Codex. Die persönliche Erklärung und Präsentation erfolgen durch den Lernenden.
