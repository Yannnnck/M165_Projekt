# Präsentationsvideo – ungefähr 20 Minuten

Das Video nimmst du selbst auf. Diese Datei ist ein Ablaufplan mit Sprechhinweisen,
keine Behauptung, dass die Aufnahme bereits existiert. Zeige die echte Konsole und
die wichtigsten Codeausschnitte. Ein zusätzlicher Foliensatz ist nicht notwendig.

## Vorbereitung

1. Terminal im Projektordner öffnen, Schrift gut lesbar einstellen.
2. `Setup.ps1` und einmal `Test.ps1` ausführen, Ergebnis prüfen.
3. `DATENMODELL.md`, `EventHubService.cs`, `06-aggregations.js` und `CAP-BASE.md` öffnen.
4. Die drei Beispiel-IDs aus der README bereithalten; eine Testbuchungs-ID entsteht live.
5. Die Anwendung vor Backup und Failover beenden. Keine Zugangsdaten-Datei zeigen.
6. Erst eine kurze Probeaufnahme mit Ton machen und die Demo selbst durchspielen.

## Ablauf

| Zeit | Zeigen und erklären |
| --- | --- |
| 0:00–1:30 | Problem: Events, viele Buchungen, zuverlässiger Verkauf. Ziel und Projektumfang. |
| 1:30–4:00 | Datenmodell-Grafik. Warum Kategorien eingebettet sind und Buchungen separat liegen. Datentypen und historische Preise. |
| 4:00–5:30 | Validator einer Buchung und ein Testfehler. Kurz die Menge der Testdaten nennen. |
| 5:30–9:00 | C#-Menü: Events, Kunden, Reservierung, Bezahlung, Buchungshistorie und Umsatz. Zweite Eventseite zeigen. |
| 9:00–11:30 | `Book` im Code: Session, Filter, Bestands-Update, Insert, Commit. Integrationstest mit Rollback und parallelen Käufen. |
| 11:30–13:30 | Umsatz-Pipeline und zweite Auswertung ausführen. `$match`, `$group`, `$lookup` erklären. |
| 13:30–15:00 | Explain-Demo: COLLSCAN/IXSCAN, untersuchte Dokumente, Textindex. |
| 15:00–17:00 | Replica-Set-Zustand, tatsächlichen Primary-Ausfall auslösen. Neuwahl, erfolgreiche Writes, Wiederanlauf zeigen. |
| 17:00–18:00 | Rollen sowie bereits erzeugtes Backup-/Restore-Protokoll mit Inhaltsvergleich zeigen. |
| 18:00–19:00 | CAP/BASE und Sharding-Konzept: Mehrheit, Unterbrechung, Replikation versus Verteilung. |
| 19:00–20:00 | Technisches und eigenes Fazit, Grenzen und mögliche Verbesserungen. |

## Demo 1: Konsole und Verkauf

```powershell
.\scripts\Run.ps1
```

Menü 1 → Seite 1. Sage sinngemäss: „Hier sehe ich freie, reservierte und verkaufte
Plätze getrennt.“ Menü 1 → Seite 2 zeigt die serverseitige Paginierung.

Menü 3 mit diesen Eingaben:

```text
Kunde:    000000000000000000000190
Event:    0000000000000000000001f5
Kategorie: VIP
Anzahl:    1
Aktion:    r
```

Buchungs-ID aus der Ausgabe kopieren. Menü 7 zeigt zunächst unveränderten Umsatz.
Menü 5 mit der Buchungs-ID bezahlt; Menü 7 zeigt zusätzlich CHF 120 beim Neon-Event.
Menü 4 zeigt die Buchung. Optional Menü 6: stornieren und freien Platz erklären.
Mit Menü 0 beenden.

## Demo 2: Transaktionen

```powershell
.\scripts\Run.ps1 --self-test
```

Zeige insbesondere die Zeilen zu Rollback, drei erfolgreichen Parallelbuchungen
und dem verhinderten doppelten Storno. Der Test verwendet eigene Daten und räumt
sie wieder auf. Im Code wird der Fehler absichtlich **nach dem Insert** geworfen.
Damit prüft der Test, dass beide Änderungen zurückgenommen werden.

## Demo 3: Auswertungen und Indexe

```powershell
.\scripts\Mongo.ps1 mongodb/06-aggregations.js
.\scripts\Mongo.ps1 mongodb/08-explain.js
```

Bei der Umsatz-Pipeline erklären, warum der Statusfilter vor dem Gruppieren steht.
Beim Indexvergleich `docsExamined` und den Plan zeigen. Die kleinen Seed-Daten
belegen die Arbeitsweise des Index; sie sind kein realistischer Performance-Benchmark.

## Demo 4: Serverausfall

```powershell
.\scripts\Mongo.ps1 mongodb/12-replica-status.js
.\scripts\Failover.ps1
```

Während der Neuwahl erklären: „Zwei von drei Knoten bilden weiter eine Mehrheit.
Der neue Primary nimmt die Schreibzugriffe an. Der Test prüft ein vor dem Ausfall
bestätigtes Dokument und führt anschliessend C#-Transaktionen aus.“ Am Schluss sind
alle drei Knoten wieder gesund. Welcher Port Primary ist, darf sich ändern.

## Demo 5: Administration

```powershell
.\scripts\Mongo.ps1 mongodb/10-role-test.js -Role report
.\scripts\Backup-Restore.ps1
```

Falls die Aufnahme zeitlich knapp wird, den bereits ausgeführten Restore im
Testprotokoll zeigen. Wichtig sind die erfolgreiche Wiederherstellung und der
Vergleich von Dokument-Hashes, Indexen und Validatoren. Erkläre das Wartungsfenster:
Während dieses Datenbank-Dumps finden keine Anwendungs-Schreibzugriffe statt.

## Schluss in eigenen Worten

Ein mögliches technisches Fazit: „Getrennte Buchungen halten das Modell beherrschbar.
Die zusätzliche Komplexität steckt in Transaktionen und im sicheren Wiederanlauf
des Replica Sets. Tests mit absichtlichen Fehlern zeigen mehr als nur einen
erfolgreichen Normalfall.“

Ergänze deine tatsächliche Erfahrung aus den Übungen. Erläutere offen die
KI-Unterstützung und welche Teile du selbst nachvollzogen, ausprobiert oder angepasst hast.
