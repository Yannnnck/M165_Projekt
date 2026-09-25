# EventHub – Modul 165

Lauffähige C#-Konsolenanwendung mit MongoDB: Events anzeigen/anlegen, Tickets buchen,
reservieren, bezahlen und stornieren, Bewertungen erfassen und Umsätze auswerten.
Die lokale Datenbank besteht aus drei authentifizierten Replica-Set-Knoten.

## Start in PowerShell

PowerShell im Projektordner öffnen:

```powershell
.\scripts\Setup.ps1
.\scripts\Run.ps1
```

Falls Windows das Ausführen der Skripte blockiert, nur für dieses Terminal erlauben:

```powershell
Set-ExecutionPolicy -Scope Process -ExecutionPolicy Bypass
```

Voraussetzungen: Windows, .NET SDK 10, MongoDB Server 8.x, mongosh und MongoDB
Database Tools (`mongodump`, `mongorestore`). Die Programme müssen im PATH oder
unter `C:\Program Files\MongoDB` liegen. Die Ports 27101–27103 müssen frei sein.
Beim ersten .NET-Start benötigt NuGet Internetzugang. Docker ist nicht erforderlich.

Getestet am 25.09.2026 mit .NET SDK 10.0.101, MongoDB 8.3.8 und MongoDB.Driver 3.12.0.
Die NuGet-Versionen sind in `packages.lock.json` festgehalten.

`Setup.ps1` startet ausschliesslich die Projektknoten, erstellt zufällige lokale
Passwörter, Validatoren, Indexe und Testdaten. Erneutes Ausführen erhält bestehende
Daten. Ein vorhandener MongoDB-Dienst auf Port 27017 bleibt davon unabhängig.

## Erste Buchung

1. Im Menü **1**, dann Seite **1** wählen: Events und Bestände ansehen.
2. Menü **2**: Kunden anzeigen und eine ID kopieren.
3. Menü **3**: Kunden-ID, Event-ID, `VIP`, Anzahl `1`, danach `r` eingeben.
4. Die ausgegebene Buchungs-ID kopieren. Menü **5** bezahlt die Reservierung.
5. Menü **7** zeigt den aktualisierten Umsatz, Menü **4** die Kundenbuchungen.
6. Menü **6** storniert eine Buchung und gibt die Plätze wieder frei.

Feste Beispiel-IDs der Testdaten:

| Objekt | ID |
| --- | --- |
| Kunde Lara Meier | `000000000000000000000190` |
| Event Neon Pop Night | `0000000000000000000001f5` |
| Vergangenes Jazz-Event | `0000000000000000000001f4` |

Die Termine werden beim ersten Setup relativ zum aktuellen Datum erzeugt.
Wenn das Projekt erst Monate später präsentiert wird, ein neues zukünftiges Event
über Menü 8 anlegen. Bestehende Testdaten werden absichtlich nicht automatisch verschoben.

## Tests und Demos

Anwendung zuerst beenden, damit der Backup-Test in einem ruhigen Wartungsfenster läuft.

```powershell
# Gesamttest: Schema, Referenzen, Rollen, CRUD, C#, Abfragen, Indexe, Backup, Ausfall
.\scripts\Test.ps1

# Einzelne Demonstrationen
.\scripts\Mongo.ps1 mongodb/05-queries.js
.\scripts\Mongo.ps1 mongodb/06-aggregations.js
.\scripts\Mongo.ps1 mongodb/07-crud.js
.\scripts\Mongo.ps1 mongodb/08-explain.js
.\scripts\Run.ps1 --self-test
.\scripts\Backup-Restore.ps1
.\scripts\Failover.ps1

# Replica-Set-Zustand und Anwendungs-Kurzansichten
.\scripts\Mongo.ps1 mongodb/12-replica-status.js
.\scripts\Run.ps1 --events
.\scripts\Run.ps1 --report

# Projektknoten sauber beenden; später mit Setup.ps1 wieder starten
.\scripts\Stop.ps1
```

Der Failover-Test beendet tatsächlich den aktuellen Projekt-Primary. Er prüft
Datenbestand, Schreibzugriff und .NET-Transaktionen auf den verbleibenden Knoten und
startet den ausgefallenen Knoten im `finally`-Block wieder. Währenddessen keine
weiteren Administrationsskripte ausführen. Die Integrationstests entfernen ihre
eigenen Testdaten anschliessend wieder.

## Wo finde ich was?

| Pfad | Inhalt |
| --- | --- |
| [docs/PROJEKT.md](docs/PROJEKT.md) | Einfache Projektdokumentation nach IPERKA und A1–A15-Nachweise |
| [docs/DATENMODELL.md](docs/DATENMODELL.md) | Grafik, Collections, Beziehungen und Indexe |
| [docs/VERSTEHEN.md](docs/VERSTEHEN.md) | Erklärung des Codes und Übungen für das Fachgespräch |
| [docs/CAP-BASE.md](docs/CAP-BASE.md) | Reflexion zu CAP, BASE und Transactions |
| [docs/SHARDING.md](docs/SHARDING.md) | Optionale Anforderung AO2: Sharding-Konzept |
| [docs/VIDEO.md](docs/VIDEO.md) | Ablauf und Befehle für die ca. 20-minütige Präsentation |
| [docs/TESTS.md](docs/TESTS.md) | Testfälle, Ergebnisse und Grenzen |
| [docs/evidence/latest-test.txt](docs/evidence/latest-test.txt) | Tatsächliches Protokoll des letzten Gesamttests |
| `mongodb/` | Alle mongosh-Befehle als JavaScript-Dateien |
| `scripts/` | Windows-Setup, Start, Tests und Administration |
| `src/EventHub/` | C#-Konsolenanwendung und Integrationstests |

## Lokale Daten und Zugang

Die Anwendungsdatenbank heisst `eventhub`, das Replica Set `eventhub-rs`.
`admin` hat `root`, `sales` hat `readWrite` auf `eventhub`, `report` hat `read` auf
`eventhub`. Die Anwendung verwendet `sales`. Die Rollen sind mit echten abgewiesenen
Zugriffen getestet. Alle Knoten binden ausschliesslich an Loopback.

Passwörter, Keyfile, Datenverzeichnisse und Backups liegen in `.local/`; dieser
Ordner wird durch `.gitignore` vom Repository ausgeschlossen. Lokale Passwörter
nicht ins Video oder in Screenshots aufnehmen. Für Compass kann eine Verbindung
mit den drei Hosts und `replicaSet=eventhub-rs`, Authentifizierungsdatenbank `admin`
und einem Benutzer aus `.local/credentials.json` konfiguriert werden.

Das Backup-Skript erstellt ein komprimiertes Archiv sowie ein Prüfmanifest und
restauriert in eine neue Datenbank `eventhub_restore_<Zeitstempel>`. Es überschreibt
die Originaldatenbank nicht. Alte Restore-Datenbanken bleiben als Nachweis erhalten.
Bei sehr häufigen Testläufen können sie später bewusst mit Compass entfernt werden.

## Abgabe

Code, Skripte und Markdown-Dokumentation liegen im lokalen Git-Repository.
Der GitHub-Upload folgt später. Das Präsentationsvideo und das persönliche Fazit
nach dem eigenen Durcharbeiten erstellt der Lernende selbst. Die Lösung wurde mit
KI-Unterstützung umgesetzt; technische Aussagen und Tests sind im Repository
nachvollziehbar. Es werden keine persönlichen Lernerfahrungen oder Arbeitszeiten erfunden.

Grundlage: [Projektauftrag EventHub](https://github.com/INFEFZ/Modul165/blob/main/G4_Exams/projektauftrag-eventhub-mongodb.md)
und [Kursunterlagen Modul 165](https://github.com/INFEFZ/Modul165).
