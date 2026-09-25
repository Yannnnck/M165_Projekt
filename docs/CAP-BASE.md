# Reflexion: CAP, BASE und EventHub

CAP beschreibt einen Zielkonflikt verteilter Systeme bei einer Netzwerkpartition:
**Consistency** meint eine Sicht, als gäbe es eine einzige aktuelle Kopie
(Linearizierbarkeit). **Availability** bedeutet, dass jede Anfrage an einen nicht
ausgefallenen Knoten erfolgreich beantwortet werden kann. Eine Fehlermeldung allein
erfüllt diesen Verfügbarkeitsbegriff nicht. **Partition tolerance** bedeutet,
Netzwerkunterbrechungen zwischen Teilen des Systems berücksichtigen zu müssen.
Während einer solchen Unterbrechung kann ein System nicht zugleich uneingeschränkt
verfügbar sein und diese strenge Konsistenz garantieren. Die Kurzform „zwei von drei“
ist ohne den Bezug auf eine Partition zu ungenau.

EventHub priorisiert beim Ticketverkauf konsistente Bestände. Im Replica Set mit drei
stimmberechtigten, datentragenden Knoten braucht eine Primary-Wahl eine Mehrheit,
also zwei Stimmen. Schreibzugriffe gehen an den Primary. Mit `writeConcern: majority`
wartet die Bestätigung auf die Mehrheit. Ist ein Knoten isoliert, darf er nicht
unabhängig bestätigte Verkäufe fortsetzen. Die Mehrheitsseite kann weiterarbeiten,
die Minderheitsseite kann keine solchen Schreibzugriffe erfolgreich bedienen.
Sind zwei Knoten weg, sind neue mehrheitsbestätigte Verkäufe nicht mehr möglich.
Diese Entscheidung ist für den Verkauf im Sinne von **CP** zu verstehen: Lieber
kurz nicht verkaufen als dasselbe letzte Ticket mehrfach bestätigen.

Das bedeutet nicht, dass jeder MongoDB-Lesezugriff automatisch linearizierbar ist.
Konsistenz hängt von Read Concern, Write Concern, Read Preference und der Operation ab.
EventHub nutzt für Buchungen `readConcern: snapshot`, `writeConcern: majority` und
den Primary. Das ergibt eine konsistente Transaktionssicht und eine atomare
Bestätigung der Änderungen. Es ist keine pauschale Behauptung, alle beliebigen
Abfragen auf allen Knoten hätten immer sofort denselben Stand. Die normalen Listen
lesen standardmässig vom Primary; es wird kein Secondary-Reporting konfiguriert.

Die Buchungstransaktion ändert mindestens zwei Dokumente: den Kategorie-Bestand im
Event und die Buchung in `bookings`. **Atomicity** bedeutet, dass entweder alle
Änderungen oder keine sichtbar werden. Fachliche **Consistency** bedeutet hier,
dass Bestände nicht negativ werden und zur Buchung passen; diese ACID-Konsistenz
ist ein anderer Begriff als CAP-Consistency. **Isolation** trennt laufende
Transaktionen; konkurrierende Bestandsänderungen können einen Write Conflict
auslösen und vom Treiber wiederholt werden. **Durability** wird durch die
mehrheitsbestätigte Speicherung unterstützt. Der Test mit zehn Käufen und drei
freien Plätzen prüft diesen Ablauf praktisch. Transaktionen beseitigen den
CAP-Zielkonflikt nicht und ersetzen weder Validatoren noch fachliche Prüfungen.

**BASE** steht für Basically Available, Soft State und Eventual Consistency.
Secondaries replizieren Änderungen asynchron und können vorübergehend zurückliegen.
Ohne weitere Änderungen und bei funktionierender Replikation nähern sich ihre
Datenstände an. Ein bewusst auf Secondaries lesendes Statistiksystem könnte diese
Verzögerung akzeptieren. Beim Ticketbestand ist ein lediglich irgendwann richtiger
Wert aber ungenügend. Deshalb verwendet EventHub für Verkäufe ACID-Transaktionen,
obwohl die Replikation im Hintergrund asynchron arbeitet. „NoSQL“ bedeutet somit
nicht automatisch „keine Transaktionen“ oder „nur BASE“.

Der echte Ausfalltest zeigt eine begrenzte Unterbrechung während der Neuwahl; danach
funktionieren bestätigte Schreibzugriffe und Transaktionen mit zwei Knoten weiter.
Das ist Hochverfügbarkeit mit Wiederanlauf, keine Garantie unterbrechungsfreier
Bedienung. Drei Prozesse auf demselben Rechner bilden nur einen lokalen Lernaufbau.
Stromausfall oder ein defekter Rechner betreffen alle drei. Für einen realen Betrieb
wären getrennte Ausfallbereiche und eine passende Backup-Strategie notwendig.

Quellen: [Kurs S2](https://github.com/INFEFZ/Modul165/tree/main/S2_CAP),
[MongoDB-Transaktionen](https://www.mongodb.com/docs/drivers/csharp/current/crud/transactions/),
[MongoDB-Replica-Set-Aufbau](https://www.mongodb.com/docs/manual/tutorial/deploy-replica-set-with-keyfile-access-control/).
