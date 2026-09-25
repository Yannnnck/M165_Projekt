# Tests und Ergebnisse

Die Tests laufen auf einer echten lokalen MongoDB mit Authentifizierung und drei
datentragenden Knoten. Es werden keine Datenbank-Mocks verwendet.

```powershell
.\scripts\Setup.ps1
.\scripts\Test.ps1
```

Der letzte Gesamtlauf ist unter [evidence/latest-test.txt](evidence/latest-test.txt)
protokolliert. Er endet nur bei erfolgreichem Ablauf mit `GESAMTPRÜFUNG BESTANDEN`.
Neue Läufe ersetzen das Protokoll. Die folgenden Ergebnisse beziehen sich auf die
unveränderten Seed-Daten am 25.09.2026.

## Prüffälle

| Bereich | Prüffall | Ergebnis |
| --- | --- | --- |
| Build | .NET-Build mit gesperrten NuGet-Versionen | Erfolgreich, 0 Fehler und 0 Warnungen |
| Seed | Mindestmengen, Referenzen, Veranstaltungsorte/Kapazitäten | Erfüllt |
| Bestand | Verkauf/Reservierung je Event/Kategorie entspricht aktiven Buchungen | Konsistent |
| Schema | Fehlerhafte Events, Buchungen und Kunden | Abgewiesen mit MongoDB-Fehler 121 |
| CRUD | Je zwei Create/Read/Update/Delete-Beispiele | Erfolgreich, Demo-Dokumente entfernt |
| Rollen | Reporting liest, Schreibzugriff wird verweigert | Unauthorized (13) |
| Rollen | Verkauf schreibt, darf Benutzer nicht auflisten | Wie vorgesehen |
| Transaktion | Fehler nach Bestandsänderung und Buchungs-Insert | Beide Änderungen zurückgerollt |
| Konkurrenz | Zehn parallele Käufe auf drei freie Plätze | Genau drei erfolgreich, Bestand null |
| Status | Ausverkauft → Storno → wieder geplant | Korrekt |
| Reservierung | Reservieren → bezahlen | Zähler werden korrekt umgebucht |
| Fehlerfälle | Unbekannter Kunde/Kategorie, null Tickets, doppeltes Storno/Zahlung | Abgewiesen |
| Bewertungen | Zukunft abgewiesen, vergangenes bezahltes Event erlaubt, Duplikat abgewiesen | Korrekt |
| Unique-Index | Zweiter Kunde mit derselben E-Mail | Abgewiesen |
| Paginierung | Zwei Seiten zu je zwei Events | Vollständig und ohne Überschneidung |
| C#-Aggregation | Drei bezahlte Testtickets à CHF 50 | Exakt CHF 150 |
| Backup | `mongodump` erstellt komprimiertes Archiv | Erfolgreich |
| Restore | `mongorestore` in neue Datenbank, Vergleich von SHA256, Anzahl, Indexen, Validatoren | Alle sieben Collections stimmen überein |
| Failover | Aktuellen Primary-Prozess tatsächlich beenden | Neuer Primary, vorher bestätigte Daten vorhanden, neuer Write erfolgreich |
| Betrieb nach Ausfall | .NET-Tests mit nur zwei verfügbaren Knoten | Bestanden |
| Wiederanlauf | Ausgefallenen Knoten erneut starten | Drei gesunde Knoten |
| Erneutes Setup | Setup erneut ausführen | Bestehende Testdaten erhalten |

Die Failover-Dauer wird im Protokoll gemessen. Ein gemessener Lauf benötigte rund
13 Sekunden inklusive Prüfverbindung und Schreibnachweis. Das ist keine feste SLA
und keine reine Messung der Wahlzeit.

## Gemessene Indexwirkung

| Abfrage | Ohne Index: untersuchte Dokumente | Mit Index: untersuchte Dokumente |
| --- | ---: | ---: |
| Location und zukünftiges Datum | 6 | 2 |
| Bezahlte Buchungen für Neon Pop Night | 120 | 16 |
| Textsuche „Rock“ | Nicht ohne Textindex ausführbar | 1 |

`hint({$natural:1})` erzwingt den Collection Scan für einen kontrollierten Vergleich.
Der zweite Lauf erzwingt den benannten Index. Der Text-Plan enthält `TEXT_MATCH`
und den Index `event_text`. Millisekunden-Laufzeiten sind bei so wenig Daten nicht
als Leistungsversprechen geeignet.

## Backup und Restore genauer

Vor dem Dump wird ein Manifest geschrieben: Für jede Collection werden die nach
`_id` sortierten Extended-JSON-Dokumente per SHA256 zusammengefasst. Hinzu kommen
Anzahl, Collection-Optionen einschliesslich Validator und Indexdefinitionen.
Nach dem Restore werden dieselben Informationen neu berechnet und vollständig
verglichen. Es reicht somit nicht, dass lediglich gleich viele Dokumente existieren.

Das Archiv und das Manifest liegen unter `.local/backups/<Zeitstempel>/`.
Der Restore erfolgt nach `eventhub_restore_<Zeitstempel>`. Es wird kein `--drop`
gegen die Originaldatenbank ausgeführt. Benutzer liegen in `admin` und gehören nicht
zum anwendungsspezifischen Dump; sie werden reproduzierbar durch das Setup erstellt.

Dieser Dump ist für ein Wartungsfenster ohne parallele Schreibzugriffe gedacht.
Es gibt keine automatische Schreibsperre und keinen Point-in-Time-Restore via Oplog.
Der Prüfvergleich weist die tatsächlich gesicherte Lern-Datenbank nach. Eine
Produktionssicherung bei laufendem Verkehr braucht eine darauf abgestimmte Strategie.

## Grenzen des Nachweises

- Der Test simuliert einen Prozessausfall, keine physisch getrennten Server oder Netzwerkpartition.
- Unkontrollierte direkte Shell-Schreibzugriffe können fachliche Referenzen verletzen;
  Validatoren ersetzen keine Fremdschlüssel. Die App prüft ihre Anwendungsfälle.
- Reservierungen verfallen nicht automatisch. Es gibt keine echte Zahlungsabwicklung.
- Retry eines Transaktions-Callbacks ist abgedeckt. Ein manuell wiederholter Kauf
  nach einem unklaren Verbindungsfehler hat keinen übergreifenden Request-Key.
- Datenmengen und Laufzeiten sind für einen Unterrichtsnachweis gedacht.
- `Skip` ist für kleine Seitenmengen einfach. Bei sehr tiefen Seiten wäre Keyset-Paginierung effizienter.
- Die automatische Datumsvergabe findet nur beim ersten Seed statt. Für spätere
  Präsentationen kann ein neues zukünftiges Event über die Anwendung angelegt werden.

Ein zusätzlicher Stop-/Start-Lauf ist in [evidence/lifecycle.txt](evidence/lifecycle.txt)
protokolliert; er prüft das saubere Beenden aller Knoten und den anschliessenden
erneuten Start inklusive .NET-Umsatzabfrage.

## Gefundene und behobene Integrationsprobleme

Beim Aufbau wurden mongosh-spezifische Fehlerbehandlung und Benutzerprüfungen
korrigiert. Ausserdem wurde ein unnötiger Primary-Rückwechsel nach dem Wiederanlauf
durch gleiche Knotenprioritäten vermieden. Die abschliessenden Tests wurden danach
erneut ausgeführt. Das Protokoll dokumentiert den getesteten Endstand.
